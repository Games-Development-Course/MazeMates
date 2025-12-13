using Unity.Netcode;
using UnityEngine;

public class PlayerCamera1P : NetworkBehaviour
{
    public float mouseSensitivity = 200f;
    public Transform playerBody;

    float xRotation = 0f;

    private bool cameraFrozen = false;
    private float autoUnlockIn = -1f;

    private TutorialManager tutorial;
    private bool isTraveller;
    private bool isNavigator;

    // רכיבי מצלמה ושמע
    private Camera myCamera;
    private AudioListener myListener;

    private void Awake()
    {
        myCamera = GetComponentInChildren<Camera>(true);
        myListener = GetComponentInChildren<AudioListener>(true);

        if (myCamera != null)
        {
            // לוודא שאין TargetTexture – רנדר ישירות למסך
            myCamera.targetTexture = null;
        }
    }

    // 🔹 זה האירוע החשוב ברשת – כאן נחליט מי רואה איזו מצלמה
    public override void OnNetworkSpawn()
    {
        ulong localId = NetworkManager.Singleton ? NetworkManager.Singleton.LocalClientId : 9999;

        Debug.Log(
            $"[CAMERA][OnNetworkSpawn] '{gameObject.name}' " +
            $"OwnerClientId={OwnerClientId} LocalClientId={localId} IsOwner={IsOwner}"
        );

        if (!IsOwner)
        {
            if (myCamera != null)
            {
                myCamera.targetTexture = null;
                myCamera.enabled = false;
                myCamera.gameObject.SetActive(false);
            }
            if (myListener != null) myListener.enabled = false;

            Debug.Log($"[CAMERA] DISABLE non-owner camera '{gameObject.name}' on client {localId}");

            enabled = false;
            return;
        }

        // === OWNER (השחקן המקומי) ===
        if (myCamera != null)
        {
            myCamera.gameObject.SetActive(true);
            myCamera.enabled = true;
            myCamera.targetTexture = null;
            myCamera.targetDisplay = 0;   // 👈 חשוב: תמיד מסך ראשי
            Debug.Log($"[CAMERA] OWNER camera active '{myCamera.name}', targetDisplay={myCamera.targetDisplay}");
        }
        if (myListener != null) myListener.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log($"[CAMERA] ENABLE owner camera '{gameObject.name}' on client {localId}");
    }

    private void Start()
    {
        // לא בעלים? לא עושים כלום (OnNetworkSpawn כבר דאג לכיבוי)
        if (!IsOwner) return;

        tutorial = Object.FindFirstObjectByType<TutorialManager>();

        var rootMovement = GetComponentInParent<PlayerMovement1P>();
        if (rootMovement != null)
        {
            string rootName = rootMovement.gameObject.name;
            isTraveller = rootName.Contains("Trav");
            isNavigator = rootName.Contains("Nav");

            Debug.Log($"[CAMERA] rootName='{rootName}'  isTraveller={isTraveller}  isNavigator={isNavigator}");
        }
        else
        {
            isTraveller = name.Contains("Trav");
            isNavigator = name.Contains("Nav");

            Debug.Log($"[CAMERA] fallback name check '{name}'  isTraveller={isTraveller}  isNavigator={isNavigator}");
        }
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (GameManager.Instance == null) return;
        if (playerBody == null)
        {
            Debug.LogWarning($"[CAMERA] playerBody is NULL on '{gameObject.name}'");
            return;
        }
        if (GameManager.Instance.inPuzzle) return;

        // ====== FREEZE CAMERA ======
        if (cameraFrozen)
        {
            transform.localRotation = Quaternion.identity;

            if (autoUnlockIn > 0)
            {
                autoUnlockIn -= Time.deltaTime;
                if (autoUnlockIn <= 0)
                {
                    cameraFrozen = false;
                    Debug.Log($"[CAMERA] Auto-unfreeze camera on '{gameObject.name}'");
                }
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
                Debug.Log($"[CAMERA][LOOK] Traveller looked on client {NetworkManager.Singleton.LocalClientId}");
                SendLookServerRpc(true);
            }
            else if (isNavigator)
            {
                Debug.Log($"[CAMERA][LOOK] Navigator looked on client {NetworkManager.Singleton.LocalClientId}");
                SendLookServerRpc(false);
            }
            else
            {
                Debug.Log($"[CAMERA][LOOK] looked but role not detected on '{gameObject.name}'");
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

        Debug.Log(
            $"[CAMERA] SetCameraFrozen({freeze}) on '{gameObject.name}' " +
            $"(client {NetworkManager.Singleton.LocalClientId})"
        );

        // גם בקפאה וגם בשחרור – שומרים על עכבר נעול
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void LockCameraForSeconds(float sec)
    {
        cameraFrozen = true;
        autoUnlockIn = sec;

        Debug.Log(
            $"[CAMERA] LockCameraForSeconds({sec}) on '{gameObject.name}' " +
            $"(client {NetworkManager.Singleton.LocalClientId})"
        );
    }

    public bool IsFrozen => cameraFrozen;

    // ====== SERVER RPC ======

    [ServerRpc]
    private void SendLookServerRpc(bool traveller)
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null)
        {
            Debug.LogWarning("[CAMERA][ServerRpc] TutorialManager NOT FOUND on server");
            return;
        }

        if (traveller)
        {
            Debug.Log("[CAMERA][ServerRpc] NotifyTravellerLooked()");
            tm.NotifyTravellerLooked();
        }
        else
        {
            Debug.Log("[CAMERA][ServerRpc] NotifyNavigatorLooked()");
            tm.NotifyNavigatorLooked();
        }
    }
}
