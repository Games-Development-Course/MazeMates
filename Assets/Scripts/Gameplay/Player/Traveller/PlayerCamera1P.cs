// =======================================================
// File: Assets/Scripts/Player/PlayerCamera1P.cs
// Mouse-look disabled: camera stays fixed relative to player.
// Player rotation comes from PlayerMovement1P turning.
// =======================================================
using Unity.Netcode;
using UnityEngine;

public class PlayerCamera1P : NetworkBehaviour
{
    [Header("Mouse Look (Disabled)")]
    [SerializeField] private bool mouseLookEnabled = false;

    [Header("Cursor")]
    [SerializeField] private bool lockCursor = false;

    public float mouseSensitivity = 200f; // kept for compatibility; unused when mouseLookEnabled=false
    public Transform playerBody;

    private float xRotation;

    private bool cameraFrozen;
    private float autoUnlockIn = -1f;

    private TutorialManager tutorial;
    private bool isTraveller;
    private bool isNavigator;

    private Camera myCamera;
    private AudioListener myListener;

    private void Awake()
    {
        myCamera = GetComponentInChildren<Camera>(true);
        myListener = GetComponentInChildren<AudioListener>(true);

        if (myCamera != null)
            myCamera.targetTexture = null;
    }

    public override void OnNetworkSpawn()
    {
        ulong localId = NetworkManager.Singleton ? NetworkManager.Singleton.LocalClientId : 9999;

        Debug.Log(
            $"[CAMERA][OnNetworkSpawn] '{gameObject.name}' OwnerClientId={OwnerClientId} LocalClientId={localId} IsOwner={IsOwner}"
        );

        if (!IsOwner)
        {
            if (myCamera != null)
            {
                myCamera.targetTexture = null;
                myCamera.enabled = false;
                myCamera.gameObject.SetActive(false);
            }

            if (myListener != null)
                myListener.enabled = false;

            Debug.Log($"[CAMERA] DISABLE non-owner camera '{gameObject.name}' on client {localId}");
            enabled = false;
            return;
        }

        if (myCamera != null)
        {
            myCamera.gameObject.SetActive(true);
            myCamera.enabled = true;
            myCamera.targetTexture = null;
            myCamera.targetDisplay = 0;
            Debug.Log($"[CAMERA] OWNER camera active '{myCamera.name}', targetDisplay={myCamera.targetDisplay}");
        }

        if (myListener != null)
            myListener.enabled = true;

        ApplyCursorState();
        Debug.Log($"[CAMERA] ENABLE owner camera '{gameObject.name}' on client {localId}");
    }

    private void Start()
    {
        if (!IsOwner)
            return;

        tutorial = Object.FindFirstObjectByType<TutorialManager>();

        var rootMovement = GetComponentInParent<PlayerMovement1P>();
        if (rootMovement != null)
        {
            string rootName = rootMovement.gameObject.name;
            isTraveller = rootName.Contains("Trav");
            isNavigator = rootName.Contains("Nav");
        }
        else
        {
            isTraveller = name.Contains("Trav");
            isNavigator = name.Contains("Nav");
        }
    }

    private void Update()
    {
        if (!IsOwner)
            return;
        if (GameManager.Instance == null)
            return;
        if (playerBody == null)
            return;
        if (GameManager.Instance.inPuzzle)
            return;

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

        // Mouse look disabled: keep pitch fixed, body yaw comes from PlayerMovement1P
        if (!mouseLookEnabled)
        {
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            return;
        }

        // If you ever re-enable mouse look later, keep your old code here (optional).
    }

    public void SetCameraFrozen(bool freeze)
    {
        cameraFrozen = freeze;
        autoUnlockIn = -1f;

        Debug.Log($"[CAMERA] SetCameraFrozen({freeze}) on '{gameObject.name}' (client {NetworkManager.Singleton.LocalClientId})");
        ApplyCursorState();
    }

    public void LockCameraForSeconds(float sec)
    {
        cameraFrozen = true;
        autoUnlockIn = sec;

        Debug.Log($"[CAMERA] LockCameraForSeconds({sec}) on '{gameObject.name}' (client {NetworkManager.Singleton.LocalClientId})");
    }

    public bool IsFrozen => cameraFrozen;

    private void ApplyCursorState()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
