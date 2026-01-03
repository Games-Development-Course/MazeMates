// Assets/Scripts/Player/PlayerCameraTopDown.cs
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
    [SerializeField, Range(1f, 30f)] private float followSmooth = 12f;

    [Header("Look")]
    [SerializeField, Range(10f, 85f)] private float pitch = 60f;
    [SerializeField, Range(1f, 30f)] private float yawSmooth = 10f;
    [SerializeField, Range(0f, 5f)] private float minSpeedToTurn = 0.2f;

    private Vector3 lastPlanarDir = Vector3.forward;

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
        float targetYaw = Quaternion.LookRotation(lastPlanarDir, Vector3.up).eulerAngles.y;
        float currentYaw = transform.rotation.eulerAngles.y;
        float newYaw = Mathf.LerpAngle(currentYaw, targetYaw, yawSmooth * Time.deltaTime);

        Quaternion rot = Quaternion.Euler(pitch, newYaw, 0f);

        Vector3 desiredPos = target.position + rot * followOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, followSmooth * Time.deltaTime);
        transform.rotation = rot;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // הגנה על ערכים גם בזמן עריכה
        followSmooth = Mathf.Max(0.1f, followSmooth);
        yawSmooth = Mathf.Max(0.1f, yawSmooth);
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
