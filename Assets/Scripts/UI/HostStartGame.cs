// =========================
// File: Assets/Scripts/UI/HostStartGame.cs
// =========================
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HostStartGame : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string tutorialSceneName = "TutorialScene";
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Tutorial")]
    [Tooltip("If true, tutorial will load the GameScene (recommended). If false, loads tutorialSceneName.")]
    [SerializeField] private bool tutorialLoadsGameScene = true;

    [Header("Host-only UI")]
    [SerializeField] private GameObject hostButtonsPanel;
    [SerializeField] private LobbyState lobbyState;

    [Header("Skin Select UI")]
    [SerializeField] private LobbySkinUI lobbySkinUI;

    // -------------------------
    // Difficulty configs (maze generation)
    // -------------------------
    [Header("Easy Config (maze generation)")]
    [SerializeField] private int easyMazeW = 13;
    [SerializeField] private int easyMazeH = 13;
    [SerializeField] private int easyHearts = 3;
    [SerializeField] private int easyBombs = 3;
    [SerializeField] private int easyKeys = 3;
    [SerializeField] private int easyNormalDoors = 3;
    [SerializeField] private int easyPuzzleDoors = 2;

    [Header("Medium Config (maze generation)")]
    [SerializeField] private int medMazeW = 25;
    [SerializeField] private int medMazeH = 25;
    [SerializeField] private int medHearts = 4;
    [SerializeField] private int medBombs = 2;
    [SerializeField] private int medKeys = 2;
    [SerializeField] private int medNormalDoors = 4;
    [SerializeField] private int medPuzzleDoors = 3;

    [Header("Hard Config (maze generation)")]
    [SerializeField] private int hardMazeW = 31;
    [SerializeField] private int hardMazeH = 31;
    [SerializeField] private int hardHearts = 3;
    [SerializeField] private int hardBombs = 3;
    [SerializeField] private int hardKeys = 1;
    [SerializeField] private int hardNormalDoors = 5;
    [SerializeField] private int hardPuzzleDoors = 4;

    // -------------------------
    // HostStartGame rules (NOT seed)
    // -------------------------
    [Header("HostStartGame Rules")]
    [SerializeField] private int easyLives = 3;
    [SerializeField] private int medLives = 2;
    [SerializeField] private int hardLives = 1;

    [SerializeField] private int easyKeysToCollect = 3;
    [SerializeField] private int medKeysToCollect = 2;
    [SerializeField] private int hardKeysToCollect = 1;

    [SerializeField] private int easyBombRemovals = 1;
    [SerializeField] private int medBombRemovals = 0;
    [SerializeField] private int hardBombRemovals = 0;

    [SerializeField] private int easyHints = 2;
    [SerializeField] private int medHints = 2;
    [SerializeField] private int hardHints = 4;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private bool _nmBound;
    private bool _sceneBound;

    private void Awake()
    {
        DLog("Awake()");
        if (hostButtonsPanel != null)
            hostButtonsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        DLog("OnEnable()");
        BindSceneCallbacks();
        TryBind();
        BindNetworkCallbacks();
        ApplyVisibility("OnEnable");
        DumpState("OnEnable");
    }

    private void OnDisable()
    {
        DLog("OnDisable()");

        if (lobbyState != null)
            lobbyState.SessionFull.OnValueChanged -= OnSessionFullChanged;

        UnbindNetworkCallbacks();
        UnbindSceneCallbacks();
    }

    private void TryBind()
    {
        if (lobbyState == null)
            lobbyState = FindFirstObjectByType<LobbyState>();

        if (lobbySkinUI == null)
            lobbySkinUI = FindFirstObjectByType<LobbySkinUI>();

        if (lobbyState != null)
        {
            lobbyState.SessionFull.OnValueChanged -= OnSessionFullChanged;
            lobbyState.SessionFull.OnValueChanged += OnSessionFullChanged;

            DLog($"TryBind() lobbyState={(lobbyState ? lobbyState.name : "NULL")} isSpawned={(lobbyState != null && lobbyState.IsSpawned)}");
        }
        else
        {
            DLog("TryBind() lobbyState=NULL");
        }

        DLog($"TryBind() lobbySkinUI={(lobbySkinUI ? lobbySkinUI.name : "NULL")}");
    }

    private void OnSessionFullChanged(bool _, bool __)
    {
        DLog("OnSessionFullChanged()");
        ApplyVisibility("LobbyState.SessionFull changed");
        DumpState("OnSessionFullChanged");
    }

    private void BindSceneCallbacks()
    {
        if (_sceneBound) return;
        _sceneBound = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void UnbindSceneCallbacks()
    {
        if (!_sceneBound) return;
        _sceneBound = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DLog($"OnSceneLoaded(scene={scene.name}, mode={mode})");
        TryBind();
        BindNetworkCallbacks();
        ApplyVisibility($"SceneLoaded:{scene.name}");
        DumpState($"SceneLoaded:{scene.name}");
    }

    private void BindNetworkCallbacks()
    {
        if (_nmBound) return;

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            DLog("BindNetworkCallbacks() -> NetworkManager.Singleton=NULL (not ready?)");
            return;
        }

        _nmBound = true;

        nm.OnClientConnectedCallback -= OnClientConnChanged;
        nm.OnClientConnectedCallback += OnClientConnChanged;

        nm.OnClientDisconnectCallback -= OnClientConnChanged;
        nm.OnClientDisconnectCallback += OnClientConnChanged;

        nm.OnServerStarted -= OnServerStarted;
        nm.OnServerStarted += OnServerStarted;

        DLog("BindNetworkCallbacks() -> bound OK");
    }

    private void UnbindNetworkCallbacks()
    {
        if (!_nmBound) return;
        _nmBound = false;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnClientConnectedCallback -= OnClientConnChanged;
        nm.OnClientDisconnectCallback -= OnClientConnChanged;
        nm.OnServerStarted -= OnServerStarted;

        DLog("UnbindNetworkCallbacks() -> unbound");
    }

    private void OnServerStarted()
    {
        DLog("OnServerStarted()");
        ApplyVisibility("OnServerStarted");
        DumpState("OnServerStarted");
    }

    private void OnClientConnChanged(ulong clientId)
    {
        DLog($"OnClientConnChanged(clientId={clientId})");
        ApplyVisibility("ClientConnChanged");
        DumpState("ClientConnChanged");
    }

    private void ApplyVisibility(string reason)
    {
        if (hostButtonsPanel == null) return;

        var nm = NetworkManager.Singleton;

        bool nmOk = nm != null;
        bool isListening = nmOk && nm.IsListening;
        bool isHost = nmOk && nm.IsHost;

        int clientCount = nmOk ? nm.ConnectedClientsList.Count : -1;
        bool hasRealClient = nmOk && isListening && clientCount >= 2;

        bool visible = isHost && hasRealClient;
        hostButtonsPanel.SetActive(visible);

        DLog($"ApplyVisibility({reason}) -> visible={visible} | nmOk={nmOk} listening={isListening} host={isHost} clients={clientCount}");
    }

    public void StartTutorial()
    {
        DumpState("StartTutorial:BEFORE");

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
        {
            DLog("StartTutorial() ABORT: nm null or not server");
            return;
        }

        if (!nm.IsListening)
        {
            DLog("StartTutorial() ABORT: nm not listening");
            return;
        }

        if (nm.ConnectedClientsList.Count < 2)
        {
            DLog("StartTutorial() ABORT: less than 2 clients");
            return;
        }

        var cfg = GameConfigNet.Instance;
        if (cfg == null)
        {
            DLog("StartTutorial() ABORT: GameConfigNet.Instance is NULL");
            return;
        }

        cfg.SetTutorialConfigServerRpc();
        DLog("StartTutorial() -> SetTutorialConfigServerRpc() sent");

        hostButtonsPanel?.SetActive(false);

        string targetScene = tutorialLoadsGameScene ? gameSceneName : tutorialSceneName;
        nm.SceneManager.LoadScene(targetScene, LoadSceneMode.Single);

        DumpState("StartTutorial:AFTER");
    }

    public void StartGameEasy() => StartGameWithDifficulty(0);
    public void StartGameMedium() => StartGameWithDifficulty(1);
    public void StartGameHard() => StartGameWithDifficulty(2);

    private void StartGameWithDifficulty(int diff)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        if (!nm.IsListening) return;
        if (nm.ConnectedClientsList.Count < 2) return;

        int seed = Random.Range(1, int.MaxValue);
        StartGameWithDifficultyAndSeed(diff, seed);
    }

    /// <summary>
    /// Start a normal (non-tutorial) game with explicit difficulty and seed.
    /// Expected by PauseConsensus.
    /// </summary>
    public void StartGameWithDifficultyAndSeed(int diff, int seed)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        if (!nm.IsListening) return;
        if (nm.ConnectedClientsList.Count < 2) return;

        var cfg = GameConfigNet.Instance;
        if (cfg == null) return;

        // Ensure normal game.
        cfg.SetTutorialModeServerRpc(false);

        ApplyConfig(diff, seed);

        hostButtonsPanel?.SetActive(false);

        // Open skin selection
        cfg.SetSkinSelectOpenServerRpc(true);
        if (lobbySkinUI != null)
            lobbySkinUI.OpenSkinMenu();
    }

    private void ApplyConfig(int diff, int seed)
    {
        var cfg = GameConfigNet.Instance;
        if (cfg == null) return;

        if (diff == 0)
        {
            cfg.SetConfigServerRpc(
                easyMazeW, easyMazeH,
                easyHearts, easyBombs, easyKeys,
                easyNormalDoors, easyPuzzleDoors,
                0, seed,
                easyKeysToCollect,
                easyLives,
                easyBombRemovals,
                easyHints
            );
        }
        else if (diff == 1)
        {
            cfg.SetConfigServerRpc(
                medMazeW, medMazeH,
                medHearts, medBombs, medKeys,
                medNormalDoors, medPuzzleDoors,
                1, seed,
                medKeysToCollect,
                medLives,
                medBombRemovals,
                medHints
            );
        }
        else
        {
            cfg.SetConfigServerRpc(
                hardMazeW, hardMazeH,
                hardHearts, hardBombs, hardKeys,
                hardNormalDoors, hardPuzzleDoors,
                2, seed,
                hardKeysToCollect,
                hardLives,
                hardBombRemovals,
                hardHints
            );
        }

        // Redundant-safe: SetConfigServerRpc already resets runtime,
        // but keeping this is harmless and makes intent super explicit.
        cfg.ResetRuntimeToBaseServerRpc();
    }

    private void DumpState(string tag)
    {
        if (!verboseLogs) return;

        var nm = NetworkManager.Singleton;
        var scene = SceneManager.GetActiveScene();

        var sb = new StringBuilder();
        sb.AppendLine($"[HostStartGame][DUMP] tag={tag}");
        sb.AppendLine($"  scene={scene.name} loaded={scene.isLoaded}");

        sb.AppendLine($"  nm={(nm ? "OK" : "NULL")}");
        if (nm != null)
        {
            sb.AppendLine($"  nm.IsListening={nm.IsListening} IsHost={nm.IsHost} IsServer={nm.IsServer}");
            sb.AppendLine($"  ConnectedClientsList.Count={nm.ConnectedClientsList.Count}");
        }

        var cfg = GameConfigNet.Instance;
        sb.AppendLine($"  GameConfigNet.Instance={(cfg ? cfg.name : "NULL")} cfgIsSpawned={(cfg != null && cfg.IsSpawned)}");
        if (cfg != null)
        {
            sb.AppendLine($"  cfg.IsTutorial={cfg.IsTutorial.Value} size={cfg.MazeWidth.Value}x{cfg.MazeHeight.Value} seed={cfg.Seed.Value}");
            sb.AppendLine($"  base: lives={cfg.Lives.Value} hints={cfg.Hints.Value} bombRem={cfg.BombRemovals.Value} keysToCollect={cfg.KeysToCollect.Value}");
            sb.AppendLine($"  run : livesR={cfg.LivesRuntime.Value} hintsR={cfg.HintsRuntime.Value} bombRemR={cfg.BombRemovalsRuntime.Value}");
        }

        Debug.Log(sb.ToString());
    }

    private void DLog(string msg)
    {
        if (!verboseLogs) return;
        Debug.Log($"[HostStartGame] {msg} (scene={SceneManager.GetActiveScene().name}, obj={name})");
    }
}
