using Unity.Netcode;
using UnityEngine;

public class PlayerCamera1P : NetworkBehaviour
{
    public float mouseSensitivity = 200f;
    public Transform playerBody;

    float xRotation = 0f;

    private bool cameraFrozen = true;
    private float autoUnlockIn = -1f;

    private TutorialManager tutorial;
    private bool isTraveller;
    private bool isNavigator;

    void Start()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;

        tutorial = FindFirstObjectByType<TutorialManager>();

        // 🔴 במקום name של המצלמה – לוקחים את השם של האב (השחקן)
        var rootMovement = GetComponentInParent<PlayerMovement1P>();
        if (rootMovement != null)
        {
            string rootName = rootMovement.gameObject.name;
            isTraveller = rootName.Contains("Trav");
            isNavigator = rootName.Contains("Nav");
        }
        else
        {
            // fallback – במקרה קצה
            isTraveller = name.Contains("Trav");
            isNavigator = name.Contains("Nav");
        }
    }

    void Update()
    {
        if (!IsOwner) return;
        if (GameManager.Instance == null) return;
        if (playerBody == null) return;
        if (GameManager.Instance.inPuzzle) return;

        // ====== FREEZE CAMERA ======
        if (cameraFrozen)
        {
            transform.localRotation = Quaternion.identity;

            if (autoUnlockIn > 0)
            {
                autoUnlockIn -= Time.deltaTime;
                if (autoUnlockIn <= 0)
                    cameraFrozen = false;
            }

            return;
        }

        // ====== RAW INPUT ======
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        bool looked = Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f;

        // ====== שליחת אירוע מבט לסרבר (טוטוריאל) ======
        if (looked && tutorial != null && tutorial.TutorialActive.Value)
        {
            if (isTraveller)
            {
                SendLookServerRpc(true);   // Traveller
            }
            else if (isNavigator)
            {
                SendLookServerRpc(false);  // Navigator
            }
        }

        // ====== CAMERA MOVEMENT ======
        mouseX *= mouseSensitivity * Time.deltaTime;
        mouseY *= mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }

    // ====== API ======

    public void SetCameraFrozen(bool freeze)
    {
        cameraFrozen = freeze;
        autoUnlockIn = -1f;
    }

    public void LockCameraForSeconds(float sec)
    {
        cameraFrozen = true;
        autoUnlockIn = sec;
    }

    public bool IsFrozen => cameraFrozen;

    // ====== SERVER RPC ======

    [ServerRpc]
    private void SendLookServerRpc(bool traveller)
    {
        var tm = FindFirstObjectByType<TutorialManager>();
        if (tm == null) return;

        if (traveller)
            tm.NotifyTravellerLooked();
        else
            tm.NotifyNavigatorLooked();
    }
}
