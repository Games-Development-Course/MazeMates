// Assets/Scripts/Gameplay/GameManager.cs
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public event System.Action OnLevelStarted;
    public event System.Action OnLevelEnded;

    [HideInInspector] public GameObject traveller;
    [HideInInspector] public GameObject navigator;

    [HideInInspector] public PlayerMovement travellerMove;
    [HideInInspector] public PlayerMovement navigatorMove;

    [HideInInspector] public PlayerCamera1P travellerCam;
    [HideInInspector] public PlayerCamera1P navigatorCam;

    public int lives = 3;
    public bool inPuzzle = false;

    // hints == lifebuoys
    public int lifebuoys = 1;

    public int HeartPlacements = 1;
    public int BombRemovals = 1;

    public DoorController activePuzzleDoor;
    public int totalKeysToCollect = 0;

    // -----------------------------
    // ✅ Synced Keys (authoritative on server)
    // -----------------------------
    private readonly NetworkVariable<int> _keysNet =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Fallback for offline / before network spawn
    private int _localKeys = 0;

    /// <summary>
    /// Backward-friendly API: existing code like "gm.keys++" should keep compiling.
    /// On Server: sets immediately. On Client: sends to Server via RPC.
    /// </summary>
    public int keys
    {
        get => (IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            ? _keysNet.Value
            : _localKeys;

        set
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || !IsSpawned)
            {
                _localKeys = value;
                HUDManager.Instance?.UpdateHUD();
                return;
            }

            if (IsServer)
            {
                _keysNet.Value = value;
            }
            else
            {
                SetKeysServerRpc(value);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetKeysServerRpc(int newValue)
    {
        _keysNet.Value = newValue;
    }

    /// <summary>
    /// Preferred helper: adds keys safely.
    /// </summary>
    public void AddKeys(int amount = 1)
    {
        if (amount == 0) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || !IsSpawned)
        {
            _localKeys += amount;
            HUDManager.Instance?.UpdateHUD();
            return;
        }

        if (IsServer)
        {
            _keysNet.Value += amount;
        }
        else
        {
            AddKeysServerRpc(amount);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddKeysServerRpc(int amount)
    {
        _keysNet.Value += amount;
    }

    public bool AllKeysCollected() => keys >= totalKeysToCollect;

    // -----------------------------
    // Unity lifecycle
    // -----------------------------
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public override void OnNetworkSpawn()
    {
        // Keep HUD synced for everyone when keys changes
        _keysNet.OnValueChanged += OnKeysChanged;

        // If server spawned after local accumulation (rare), push it once
        if (IsServer && _localKeys != 0 && _keysNet.Value == 0)
            _keysNet.Value = _localKeys;

        // Apply config once network is alive (if available)
        ApplyConfigFromNetwork();
        BindConfigListeners();
        HUDManager.Instance?.UpdateHUD();
    }

    private void OnKeysChanged(int oldValue, int newValue)
    {
        HUDManager.Instance?.UpdateHUD();
    }

    private void Start()
    {
        Debug.Log($"[GM][TRAVELLER-ASSIGN] traveller='{traveller?.name}' pos={traveller?.transform.position} " +
                  $"netId={(traveller ? traveller.GetComponent<NetworkObject>()?.NetworkObjectId : 0)} " +
                  $"owner={(traveller ? traveller.GetComponent<NetworkObject>()?.OwnerClientId : 0)} " +
                  $"isSpawned={(traveller ? traveller.GetComponent<NetworkObject>()?.IsSpawned : false)}");

        HUDManager.Instance?.UpdateHUD();
        ApplyConfigFromNetwork();
        BindConfigListeners();
        HUDManager.Instance?.UpdateHUD();

        OnLevelStarted?.Invoke();
    }

    public void EndLevel()
    {
        OnLevelEnded?.Invoke();
    }

    // -----------------------------
    // Config binding
    // -----------------------------
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
        _keysNet.OnValueChanged -= OnKeysChanged;

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

    // -----------------------------
    // Net start helpers
    // -----------------------------
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
