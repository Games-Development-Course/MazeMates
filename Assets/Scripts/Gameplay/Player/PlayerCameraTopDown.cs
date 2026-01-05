// Assets/Scripts/Player/PlayerCameraTopDown.cs
// (Kept your networking/refs/API; swapped follow/yaw smoothing to "nicer" damped motion.)
using Unity.Netcode;
using UnityEngine;

public class PlayerCameraTopDown : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform target;
    [SerializeField] private Camera cam;
    [SerializeField] private AudioListener listener;

    [Header("Follow")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 10f, -6f);

    [Tooltip("Smoothing time (seconds). Smaller = snappier, bigger = smoother.")]
    [SerializeField, Range(0.02f, 0.6f)] private float followSmoothTime = 0.18f;

    [Header("Look")]
    [SerializeField, Range(10f, 85f)] private float pitch = 60f;

    [Tooltip("Smoothing time (seconds) for yaw follow.")]
    [SerializeField, Range(0.02f, 0.6f)] private float yawSmoothTime = 0.12f;

    [SerializeField, Range(0f, 5f)] private float minSpeedToTurn = 0.2f;

    [Header("Optional")]
    [Tooltip("If true, yaw will follow movement direction; if false, camera keeps last yaw.")]
    [SerializeField] private bool yawFollowsMovement = true;

    private Vector3 lastPlanarDir = Vector3.forward;

    // SmoothDamp refs
    private Vector3 posVelRef;
    private float yawVelRef; // for SmoothDampAngle

    private void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>(true);
        if (listener == null) listener = GetComponentInChildren<AudioListener>(true);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (cam != null) cam.gameObject.SetActive(false);
            if (listener != null) listener.enabled = false;
            enabled = false;
            return;
        }

        if (cam != null) cam.gameObject.SetActive(true);
        if (listener != null) listener.enabled = true;

        if (target == null)
            target = transform.root;
    }

    private void LateUpdate()
    {
        if (!IsOwner || target == null) return;

        if (yawFollowsMovement)
            UpdateYawFromMovement();

        UpdateCameraTransform();
    }

    private void UpdateYawFromMovement()
    {
        var cc = target.GetComponent<CharacterController>();
        if (cc == null) return;

        Vector3 planarVel = new Vector3(cc.velocity.x, 0f, cc.velocity.z);
        if (planarVel.magnitude > minSpeedToTurn)
            lastPlanarDir = planarVel.normalized;
    }

    private void UpdateCameraTransform()
    {
        float currentYaw = transform.eulerAngles.y;

        // If we still don't really have a direction (e.g., at start), keep current yaw.
        float targetYaw = currentYaw;
        if (lastPlanarDir.sqrMagnitude > 0.0001f)
            targetYaw = Quaternion.LookRotation(lastPlanarDir, Vector3.up).eulerAngles.y;

        float newYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelRef, yawSmoothTime);

        Quaternion rot = Quaternion.Euler(pitch, newYaw, 0f);

        Vector3 desiredPos = target.position + rot * followOffset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVelRef, followSmoothTime);
        transform.rotation = rot;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        followSmoothTime = Mathf.Clamp(followSmoothTime, 0.02f, 0.6f);
        yawSmoothTime = Mathf.Clamp(yawSmoothTime, 0.02f, 0.6f);
        minSpeedToTurn = Mathf.Max(0f, minSpeedToTurn);
    }
#endif

    // -------- OPTIONAL API (לשליטה מקוד אחר / UI) --------

    public void SetHeight(float height)
    {
        followOffset.y = height;
    }

    public void SetDistance(float distance)
    {
        followOffset.z = -Mathf.Abs(distance);
    }

    public void SetPitch(float newPitch)
    {
        pitch = Mathf.Clamp(newPitch, 10f, 85f);
    }
}
