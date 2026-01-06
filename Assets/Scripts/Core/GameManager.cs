// Assets/Scripts/Gameplay/GameManager.cs
using Unity.Netcode;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [HideInInspector] public GameObject traveller;
    [HideInInspector] public GameObject navigator;

    [HideInInspector] public PlayerMovement travellerMove;
    [HideInInspector] public PlayerMovement navigatorMove;

    [HideInInspector] public PlayerCamera1P travellerCam;
    [HideInInspector] public PlayerCamera1P navigatorCam;

    public int lives = 3;
    public int keys = 0;
    public bool inPuzzle = false;

    // hints == lifebuoys
    public int lifebuoys = 1;

    public int HeartPlacements = 1;
    public int BombRemovals = 1;

    public DoorController activePuzzleDoor;
    public int totalKeysToCollect = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

   private void Start()
    {
        HUDManager.Instance?.UpdateHUD();
        ApplyConfigFromNetwork();
        BindConfigListeners();   // <-- הוסף
        HUDManager.Instance?.UpdateHUD();
    }
    private bool _cfgBound = false;

    private void BindConfigListeners()
    {
        if (_cfgBound) return;

        var cfg = GameConfigNet.Instance;
        if (cfg == null) return;

        cfg.BombRemovals.OnValueChanged += OnConfigChanged;
        cfg.Hints.OnValueChanged += OnConfigChanged;
        cfg.Lives.OnValueChanged += OnConfigChanged;
        cfg.KeysToCollect.OnValueChanged += OnConfigChanged;

        _cfgBound = true;
    }

    private void OnDestroy()
    {
        var cfg = GameConfigNet.Instance;
        if (cfg == null) return;

        cfg.BombRemovals.OnValueChanged -= OnConfigChanged;
        cfg.Hints.OnValueChanged -= OnConfigChanged;
        cfg.Lives.OnValueChanged -= OnConfigChanged;
        cfg.KeysToCollect.OnValueChanged -= OnConfigChanged;
    }

    private void OnConfigChanged(int _, int __)
    {
        ApplyConfigFromNetwork();
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

        lives = cfg.Lives.Value;
        BombRemovals = cfg.BombRemovals.Value;
        lifebuoys = cfg.Hints.Value;
        HUDManager.Instance?.UpdateHUD();


        Debug.Log($"[GameManager] Applied config: keysToCollect={totalKeysToCollect}, lives={lives}, bombRemovals={BombRemovals}, hints(lifebuoys)={lifebuoys}");
    }


    public bool AllKeysCollected() => keys >= totalKeysToCollect;

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
