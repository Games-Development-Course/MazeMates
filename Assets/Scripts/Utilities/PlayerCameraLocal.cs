// Assets/Scripts/Sandbox/PlayerCameraLocal.cs
using UnityEngine;

public class PlayerCameraLocal : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Tooltip("Where the camera looks relative to the target (e.g. chest height).")]
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Follow (behind the player)")]
    [Tooltip("Local offset relative to the TARGET rotation. Z negative = behind.")]
    [SerializeField] private Vector3 followOffsetLocal = new Vector3(0f, 6f, -8f);

    [Tooltip("Position smoothing time (seconds). Smaller = snappier.")]
    [SerializeField] private float positionSmoothTime = 0.12f;

    [Header("Rotation")]
    [Tooltip("If true: camera yaw follows the target's yaw (stays behind).")]
    [SerializeField] private bool followTargetYaw = true;

    [Tooltip("Extra pitch (X rotation) you can tweak live. Positive = look down more.")]
    [SerializeField] private float pitch = 25f;

    [Tooltip("Extra yaw offset (degrees) around the target (0 = directly behind).")]
    [SerializeField] private float yawOffset = 0f;

    [Tooltip("Yaw smoothing time (seconds). Smaller = snappier.")]
    [SerializeField] private float yawSmoothTime = 0.10f;

    [Header("Live Tuning (Play Mode)")]
    [SerializeField] private bool enableHotkeys = true;
    [SerializeField] private float tuneSpeed = 1f;

    private Vector3 posVelRef;
    private float yawVelRef;

    private void LateUpdate()
    {
        if (target == null) return;

        if (enableHotkeys) HandleHotkeys();

        // 1) Compute desired yaw: either follow target yaw (stay behind) or keep current yaw
        float desiredYaw = transform.eulerAngles.y;
        if (followTargetYaw)
            desiredYaw = target.eulerAngles.y + yawOffset;

        float currentYaw = transform.eulerAngles.y;
        float smoothedYaw = Mathf.SmoothDampAngle(currentYaw, desiredYaw, ref yawVelRef, yawSmoothTime);

        // 2) Build camera rotation: yaw around Y + pitch around X
        Quaternion rot = Quaternion.Euler(pitch, smoothedYaw, 0f);

        // 3) Desired position: target position + rotated local offset (so it stays behind target)
        Vector3 targetLookPoint = target.position + lookOffset;
        Vector3 desiredPos = targetLookPoint + (rot * followOffsetLocal);

        // 4) Smooth position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVelRef, positionSmoothTime);

        // 5) Look at the target (optional but usually looks best)
        transform.rotation = rot;
        transform.LookAt(targetLookPoint);
    }

    private void HandleHotkeys()
    {
        float mult = Input.GetKey(KeyCode.LeftShift) ? 4f : 1f;
        float s = tuneSpeed * mult * Time.deltaTime;

        // Pitch (X rotation) : Keypad 8/2
        if (Input.GetKey(KeyCode.Keypad8)) pitch += s * 60f;
        if (Input.GetKey(KeyCode.Keypad2)) pitch -= s * 60f;

        // Yaw offset around player : Keypad 4/6
        if (Input.GetKey(KeyCode.Keypad4)) yawOffset -= s * 90f;
        if (Input.GetKey(KeyCode.Keypad6)) yawOffset += s * 90f;

        // Distance/Height (follow offset local)
        if (Input.GetKey(KeyCode.PageUp)) followOffsetLocal.y += s * 10f;
        if (Input.GetKey(KeyCode.PageDown)) followOffsetLocal.y -= s * 10f;

        if (Input.GetKey(KeyCode.Home)) followOffsetLocal.z -= s * 10f; // more behind
        if (Input.GetKey(KeyCode.End)) followOffsetLocal.z += s * 10f; // closer

        // Smoothness
        if (Input.GetKey(KeyCode.Minus)) positionSmoothTime += s * 0.2f;
        if (Input.GetKey(KeyCode.Equals)) positionSmoothTime -= s * 0.2f;

        if (Input.GetKey(KeyCode.LeftBracket)) yawSmoothTime += s * 0.2f;
        if (Input.GetKey(KeyCode.RightBracket)) yawSmoothTime -= s * 0.2f;

        // Look offset up/down
        if (Input.GetKey(KeyCode.Semicolon)) lookOffset.y -= s * 2f;
        if (Input.GetKey(KeyCode.Quote)) lookOffset.y += s * 2f;
    }
}
