// PlayerCamera1P.cs  (Fusion 2)
using Fusion;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PlayerCamera1P : NetworkBehaviour
{
    [Header("Mouse Look")]
    public float sensitivityX = 180f;
    public float sensitivityY = 180f;
    public float minY = -80f;
    public float maxY = 80f;

    [Header("Links")]
    [SerializeField] private Transform bodyRoot; // אם ריק – ניקח parent
    [SerializeField] private AudioListener audioListener;

    private Camera cam;
    private float rotX;
    private bool cameraFrozen;

    public bool IsTraveller => CompareTag("Traveller");
    public bool IsNavigator => CompareTag("Navigator");

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (bodyRoot == null && transform.parent != null)
            bodyRoot = transform.parent;

        if (audioListener == null)
            audioListener = GetComponent<AudioListener>();
    }

    public override void Spawned()
    {
        base.Spawned();

        bool isMine = Object.HasInputAuthority;

        if (cam != null)
            cam.enabled = isMine;

        if (audioListener != null)
            audioListener.enabled = isMine;

        // לא מגלגלים מצלמה של שחקנים אחרים
        if (!isMine)
            enabled = false;
    }

    // ============================================================
    // API מבחוץ (TutorialManager / PickupObject משתמשים בזה)
    // ============================================================

    public void SetCameraFrozen(bool freeze)
    {
        cameraFrozen = freeze;
    }

    public void LockCameraForSeconds(float seconds)
    {
        if (!Object.HasInputAuthority)
            return;

        StopAllCoroutines();
        StartCoroutine(LockRoutine(seconds));
    }

    private System.Collections.IEnumerator LockRoutine(float seconds)
    {
        cameraFrozen = true;
        yield return new WaitForSeconds(seconds);
        cameraFrozen = false;
    }

    // ============================================================
    // UPDATE – תנועת מצלמה לוקאלית
    // ============================================================

    private void Update()
    {
        if (!Object.HasInputAuthority)
            return;

        if (cameraFrozen)
            return;

        float mouseX = Input.GetAxis("Mouse X") * sensitivityX * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY * Time.deltaTime;

        rotX -= mouseY;
        rotX = Mathf.Clamp(rotX, minY, maxY);

        // סיבוב גוף על ציר Y
        if (bodyRoot != null)
        {
            bodyRoot.Rotate(Vector3.up * mouseX);
        }
        else
        {
            transform.parent?.Rotate(Vector3.up * mouseX);
        }

        // סיבוב המצלמה על ציר X
        Vector3 euler = transform.localEulerAngles;
        euler.x = rotX;
        euler.y = 0f;
        euler.z = 0f;
        transform.localEulerAngles = euler;
    }
}
