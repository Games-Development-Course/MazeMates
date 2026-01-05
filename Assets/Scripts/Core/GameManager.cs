using Unity.Netcode; // חשוב!
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [HideInInspector]
    public GameObject traveller;

    [HideInInspector]
    public GameObject navigator;

    // השדות האלה כבר לא קריטיים ללוגיקה, אבל נשאיר למקרה שתשתמש בהם בעתיד
    [HideInInspector]
    public PlayerMovement travellerMove;

    [HideInInspector]
    public PlayerMovement navigatorMove;

    [HideInInspector]
    public PlayerCamera1P travellerCam;

    [HideInInspector]
    public PlayerCamera1P navigatorCam;

    public int lives = 3;
    public int keys = 0;
    public bool inPuzzle = false;

    public int lifebuoys = 1;
    public int HeartPlacements = 1;
    public int BombRemovals = 1;

    public DoorController activePuzzleDoor;
    public int totalKeysToCollect = 0;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        HUDManager.Instance?.UpdateHUD();
        ApplyConfigFromNetwork();
        HUDManager.Instance?.UpdateHUD();
    }
private void ApplyConfigFromNetwork()
    {
        var cfg = GameConfigNet.Instance;
        if (cfg == null)
        {
            Debug.LogWarning("[GameManager] GameConfigNet.Instance is null (did it persist into GameScene?)");
            return;
        }

        totalKeysToCollect = cfg.KeysToCollect.Value;
        Debug.Log($"[GameManager] totalKeysToCollect set to {totalKeysToCollect}");
    }
    public bool AllKeysCollected()
    {
        return keys >= totalKeysToCollect;
    }

    // ============================
    // NETWORK START HELPERS
    // ============================

    public void StartHost()
    {
        Debug.Log("[GameManager] Starting Host…");
        NetworkCleanup.Cleanup();
        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        Debug.Log("[GameManager] Starting Client…");
        NetworkCleanup.Cleanup();
        NetworkManager.Singleton.StartClient();
    }
}
