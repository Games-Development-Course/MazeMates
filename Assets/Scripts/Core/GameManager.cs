// GameManager.cs
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [HideInInspector] public GameObject traveller;
    [HideInInspector] public GameObject navigator;

    [HideInInspector] public PlayerMovement1P travellerMove;
    [HideInInspector] public PlayerMovement1P navigatorMove;

    [HideInInspector] public PlayerCamera1P travellerCam;
    [HideInInspector] public PlayerCamera1P navigatorCam;

    [Header("Gameplay State")]
    public int lives = 3;
    public int keys;
    public bool inPuzzle = false;

    public int lifebuoys = 1;
    public int HeartPlacements = 1;
    public int BombRemovals = 1;

    [Header("Puzzle State")]
    public DoorController activePuzzleDoor;
    public int totalKeysInLevel = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // אם את רוצה שיישאר גם בין סצנות:
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        HUDManager.Instance?.UpdateHUD();
    }

    public bool AllKeysCollected()
    {
        return keys >= totalKeysInLevel;
    }

    // ============================================================
    // FUSION START HELPERS (עוטפים את NetworkButtons)
    // ============================================================

    /// <summary>
    /// נקרא מכפתורי UI / סקריפטים ישנים שעשו StartHost דרך GameManager.
    /// עכשיו מעביר את הבקשה ל-NetworkButtons שעובד עם Fusion.
    /// </summary>
    public void StartHost()
    {
        Debug.Log("[GameManager] Starting Host via Fusion…");

        NetworkButtons buttons = FindObjectOfType<NetworkButtons>();
        if (buttons == null)
        {
            Debug.LogError("[GameManager] No NetworkButtons found in scene – cannot StartHost");
            return;
        }

        buttons.modeChoice = NetworkButtons.NetworkModeChoice.HostClient;
        buttons.StartHost();
    }

    /// <summary>
    /// נקרא מכפתורי UI / סקריפטים ישנים שעשו StartClient דרך GameManager.
    /// עכשיו מעביר את הבקשה ל-NetworkButtons שעובד עם Fusion.
    /// </summary>
    public void StartClient()
    {
        Debug.Log("[GameManager] Starting Client via Fusion…");

        NetworkButtons buttons = FindObjectOfType<NetworkButtons>();
        if (buttons == null)
        {
            Debug.LogError("[GameManager] No NetworkButtons found in scene – cannot StartClient");
            return;
        }

        buttons.modeChoice = NetworkButtons.NetworkModeChoice.HostClient;
        buttons.StartClient();
    }
}
