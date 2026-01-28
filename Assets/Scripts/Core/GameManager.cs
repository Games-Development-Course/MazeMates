// Assets/Scripts/Gameplay/GameManager.cs
using Unity.Netcode;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

#if ENABLE_INPUT_SYSTEM
    [HideInInspector] public PlayerInput travellerInput;
    [HideInInspector] public PlayerInput navigatorInput;
#endif

    [Header("Runtime")]
    public int lives = 3;             // navigator lives (runtime)
    public bool inPuzzle = false;

    // hints == lifebuoys
    public int lifebuoys = 1;

    public int HeartPlacements = 1;
    public int BombRemovals = 1;

    public DoorController activePuzzleDoor;
    public int totalKeysToCollect = 0;

    [Header("Input Lock")]
    [Tooltip("Locks keyboard/movement when game over fires.")]
    [SerializeField] private bool lockInputOnGameOver = true;

    [Tooltip("Disable PlayerMovement scripts on game over.")]
    [SerializeField] private bool disablePlayerMovementOnGameOver = true;

    [Tooltip("Disable PlayerCamera scripts on game over.")]
    [SerializeField] private bool disablePlayerCamerasOnGameOver = true;

#if ENABLE_INPUT_SYSTEM
    [Tooltip("Disable PlayerInput components (New Input System) on game over.")]
    [SerializeField] private bool disablePlayerInputsOnGameOver = true;
#endif

    private bool _gameOverTriggered = false;

    // -----------------------------
    // ✅ Synced Keys (authoritative on server)
    // -----------------------------
    private readonly NetworkVariable<int> _keysNet =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private int _localKeys = 0;

    // -----------------------------
    // ✅ Player NetIds (unique, authoritative on server)
    // -----------------------------
    private readonly NetworkVariable<ulong> _travellerNetId =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> _navigatorNetId =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

            if (IsServer) _keysNet.Value = value;
            else SetKeysServerRpc(value);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetKeysServerRpc(int newValue) => _keysNet.Value = newValue;

    public void AddKeys(int amount = 1)
    {
        if (amount == 0) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || !IsSpawned)
        {
            _localKeys += amount;
            HUDManager.Instance?.UpdateHUD();
            return;
        }

        if (IsServer) _keysNet.Value += amount;
        else AddKeysServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddKeysServerRpc(int amount) => _keysNet.Value += amount;

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
        _keysNet.OnValueChanged += OnKeysChanged;

        _travellerNetId.OnValueChanged += OnPlayerNetIdChanged;
        _navigatorNetId.OnValueChanged += OnPlayerNetIdChanged;

        // If server had local keys before spawn (rare), migrate once
        if (IsServer && _localKeys != 0 && _keysNet.Value == 0)
            _keysNet.Value = _localKeys;

        ApplyConfigFromNetwork();
        BindConfigListeners();

        if (IsServer)
            ResetRuntimeStateServer();

        // Try resolve players right away (may succeed if spawn already happened)
        ResolvePlayersByNetId();

        HUDManager.Instance?.UpdateHUD();
    }

    public override void OnNetworkDespawn()
    {
        _keysNet.OnValueChanged -= OnKeysChanged;
        _travellerNetId.OnValueChanged -= OnPlayerNetIdChanged;
        _navigatorNetId.OnValueChanged -= OnPlayerNetIdChanged;
        base.OnNetworkDespawn();
    }

    private void OnPlayerNetIdChanged(ulong oldValue, ulong newValue)
    {
        ResolvePlayersByNetId();
    }

    private void OnKeysChanged(int oldValue, int newValue)
    {
        HUDManager.Instance?.UpdateHUD();
    }

    private void Start()
    {
        HUDManager.Instance?.UpdateHUD();
        ApplyConfigFromNetwork();
        BindConfigListeners();
        HUDManager.Instance?.UpdateHUD();
    }

    private void Update()
    {
        // ✅ Server-authoritative: when lives hits 0 => game over for both players
        if (IsServer && !_gameOverTriggered && lives <= 0)
        {
            TriggerGameOverServer();
        }
    }

    public void EndLevel()
    {
        OnLevelEnded?.Invoke();
    }

    // =============================
    // Level lifecycle (authoritative)
    // =============================
    public void BeginLevelServer()
    {
        if (!IsServer) return;

        ResetRuntimeStateServer();
        BeginLevelClientRpc();
    }

    [ClientRpc]
    private void BeginLevelClientRpc()
    {
        HUDManager.Instance?.UpdateHUD();
        OnLevelStarted?.Invoke();
    }

    private void ResetRuntimeStateServer()
    {
        inPuzzle = false;
        activePuzzleDoor = null;

        _localKeys = 0;
        _keysNet.Value = 0;

        _gameOverTriggered = false;
    }

    // -----------------------------
    // ✅ Player NetId API (called by PlayerSpawnManager on SERVER)
    // -----------------------------
    public void SetPlayersNetIdsServer(ulong travellerNetObjectId, ulong navigatorNetObjectId)
    {
        if (!IsServer) return;

        _travellerNetId.Value = travellerNetObjectId;
        _navigatorNetId.Value = navigatorNetObjectId;

        // Resolve immediately on server too
        ResolvePlayersByNetId();
    }

    private void ResolvePlayersByNetId()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SpawnManager == null) return;

        if (_travellerNetId.Value != 0 &&
            nm.SpawnManager.SpawnedObjects.TryGetValue(_travellerNetId.Value, out var tNo) &&
            tNo != null)
        {
            traveller = tNo.gameObject;
            travellerMove = traveller.GetComponentInChildren<PlayerMovement>(true);
            travellerCam = traveller.GetComponentInChildren<PlayerCamera1P>(true);
#if ENABLE_INPUT_SYSTEM
            travellerInput = traveller.GetComponentInChildren<PlayerInput>(true);
#endif
        }

        if (_navigatorNetId.Value != 0 &&
            nm.SpawnManager.SpawnedObjects.TryGetValue(_navigatorNetId.Value, out var nNo) &&
            nNo != null)
        {
            navigator = nNo.gameObject;
            navigatorMove = navigator.GetComponentInChildren<PlayerMovement>(true);
            navigatorCam = navigator.GetComponentInChildren<PlayerCamera1P>(true);
#if ENABLE_INPUT_SYSTEM
            navigatorInput = navigator.GetComponentInChildren<PlayerInput>(true);
#endif
        }
    }

    // -----------------------------
    // Game Over (navigator lives == 0)
    // -----------------------------
    private void TriggerGameOverServer()
    {
        if (_gameOverTriggered) return;
        _gameOverTriggered = true;

        ShowLoseAndLockClientRpc();
    }

    [ClientRpc]
    private void ShowLoseAndLockClientRpc()
    {
        // ✅ Don’t rely on inspector refs — open for both via existing UI aggregator
        CornerUIButtons.SetLoseScreenForBothPlayers(true);

        // Ensure we have player references even on clients
        ResolvePlayersByNetId();

        if (lockInputOnGameOver)
        {
#if ENABLE_INPUT_SYSTEM
            if (disablePlayerInputsOnGameOver)
            {
                if (travellerInput) travellerInput.enabled = false;
                if (navigatorInput) navigatorInput.enabled = false;
            }
#endif

            if (disablePlayerMovementOnGameOver)
            {
                if (travellerMove) travellerMove.enabled = false;
                if (navigatorMove) navigatorMove.enabled = false;
            }

            if (disablePlayerCamerasOnGameOver)
            {
                if (travellerCam) travellerCam.enabled = false;
                if (navigatorCam) navigatorCam.enabled = false;
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Optional helper: call this from bombs if you want strict API.
    /// Works even if called by a client (server authoritative).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void DamageNavigatorLivesServerRpc(int amount)
    {
        if (amount <= 0) return;
        lives = Mathf.Max(0, lives - amount);
        HUDManager.Instance?.UpdateHUD();
        // Game over detected by Update() on server
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
        if (IsSpawned)
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
        BombRemovals = cfg.BombRemovalsRuntime.Value;
        lifebuoys = cfg.HintsRuntime.Value;

        HUDManager.Instance?.UpdateHUD();
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
