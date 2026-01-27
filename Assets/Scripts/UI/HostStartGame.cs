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

    [Header("Easy Config")]
    [SerializeField] private int easyMazeW = 13;
    [SerializeField] private int easyMazeH = 13;
    [SerializeField] private int easyHearts = 3;
    [SerializeField] private int easyBombs = 3;
    [SerializeField] private int easyKeys = 3;
    [SerializeField] private int easyNormalDoors = 3;
    [SerializeField] private int easyPuzzleDoors = 2;

    [Header("Medium Config")]
    [SerializeField] private int medMazeW = 25;
    [SerializeField] private int medMazeH = 25;
    [SerializeField] private int medHearts = 4;
    [SerializeField] private int medBombs = 2;
    [SerializeField] private int medKeys = 2;
    [SerializeField] private int medNormalDoors = 4;
    [SerializeField] private int medPuzzleDoors = 3;

    [Header("Hard Config")]
    [SerializeField] private int hardMazeW = 31;
    [SerializeField] private int hardMazeH = 31;
    [SerializeField] private int hardHearts = 3;
    [SerializeField] private int hardBombs = 3;
    [SerializeField] private int hardKeys = 1;
    [SerializeField] private int hardNormalDoors = 5;
    [SerializeField] private int hardPuzzleDoors = 4;

    [SerializeField] private int easyLives = 3;
    [SerializeField] private int medLives = 2;
    [SerializeField] private int hardLives = 1;

    private const int EASY_HINTS = 2;
    private const int MED_HINTS = 2;
    private const int HARD_HINTS = 4;

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

        bool lobbySpawned = lobbyState != null && lobbyState.IsSpawned;
        bool lobbyFull = lobbySpawned && lobbyState.SessionFull.Value;

        bool visible = isHost && hasRealClient;

        hostButtonsPanel.SetActive(visible);

        DLog($"ApplyVisibility({reason}) -> visible={visible} | " +
             $"nmOk={nmOk} listening={isListening} host={isHost} clients={clientCount} hasRealClient={hasRealClient} | " +
             $"lobbySpawned={lobbySpawned} lobbyFull={lobbyFull}");
    }

    // -------------------- Public API --------------------
    public void StartTutorial()
    {
        DumpState("StartTutorial:BEFORE");

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
        {
            DLog("StartTutorial() ABORT: nm null or not server");
            DumpState("StartTutorial:ABORT");
            return;
        }

        if (!nm.IsListening)
        {
            DLog("StartTutorial() ABORT: nm not listening");
            DumpState("StartTutorial:ABORT_NOT_LISTENING");
            return;
        }

        if (nm.ConnectedClientsList.Count < 2)
        {
            DLog("StartTutorial() ABORT: less than 2 clients (other player missing?)");
            DumpState("StartTutorial:ABORT_CLIENTS<2");
            return;
        }

        var cfg = GameConfigNet.Instance;
        if (cfg == null)
        {
            DLog("StartTutorial() ABORT: GameConfigNet.Instance is NULL");
            DumpState("StartTutorial:ABORT_CFG_NULL");
            return;
        }

        // ✅ push fixed tutorial config (5x5 T-shape)
        cfg.SetTutorialConfigServerRpc();
        DLog("StartTutorial() -> SetTutorialConfigServerRpc() sent");

        hostButtonsPanel?.SetActive(false);

        string targetScene = tutorialLoadsGameScene ? gameSceneName : tutorialSceneName;
        DLog($"StartTutorial() -> Loading scene '{targetScene}' via Netcode SceneManager");
        nm.SceneManager.LoadScene(targetScene, LoadSceneMode.Single);

        DumpState("StartTutorial:AFTER");
    }

    public void StartGameEasy() => StartGameWithDifficulty(0);
    public void StartGameMedium() => StartGameWithDifficulty(1);
    public void StartGameHard() => StartGameWithDifficulty(2);

    public void StartGameWithDifficultyAndSeed(int diff, int seed)
    {
        DumpState($"StartGameWithDifficultyAndSeed:BEFORE diff={diff} seed={seed}");

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
        {
            DLog("StartGameWithDifficultyAndSeed() ABORT: nm null or not server");
            DumpState("StartGameWithDifficultyAndSeed:ABORT_NOT_SERVER");
            return;
        }

        if (!nm.IsListening)
        {
            DLog("StartGameWithDifficultyAndSeed() ABORT: nm not listening (DISCONNECTED?)");
            DumpState("StartGameWithDifficultyAndSeed:ABORT_NOT_LISTENING");
            return;
        }

        if (nm.ConnectedClientsList.Count < 2)
        {
            DLog("StartGameWithDifficultyAndSeed() ABORT: less than 2 clients (other player missing?)");
            DumpState("StartGameWithDifficultyAndSeed:ABORT_CLIENTS<2");
            return;
        }

        var cfg = GameConfigNet.Instance;
        if (cfg == null)
        {
            DLog("StartGameWithDifficultyAndSeed() ABORT: GameConfigNet.Instance is NULL (not in scene? not spawned? destroyed?)");
            DumpState("StartGameWithDifficultyAndSeed:ABORT_CFG_NULL");
            return;
        }

        // Normal game => ensure tutorial off.
        cfg.SetTutorialModeServerRpc(false);

        DLog($"StartGameWithDifficultyAndSeed() -> cfg={(cfg ? cfg.name : "NULL")} cfgIsSpawned={(cfg != null && cfg.IsSpawned)}");

        ApplyConfig(diff, seed);

        hostButtonsPanel?.SetActive(false);

        cfg.SetSkinSelectOpenServerRpc(true);
        DLog("StartGameWithDifficultyAndSeed() -> SetSkinSelectOpenServerRpc(true) sent");

        if (lobbySkinUI != null)
        {
            lobbySkinUI.OpenSkinMenu();
            DLog("StartGameWithDifficultyAndSeed() -> lobbySkinUI.OpenSkinMenu()");
        }
        else
        {
            DLog("StartGameWithDifficultyAndSeed() -> lobbySkinUI is NULL (won't open locally)");
        }

        DumpState("StartGameWithDifficultyAndSeed:AFTER");
    }

    private void StartGameWithDifficulty(int diff)
    {
        DumpState($"StartGameWithDifficulty:BEFORE diff={diff}");

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
        {
            DLog("StartGameWithDifficulty() ABORT: nm null or not server");
            DumpState("StartGameWithDifficulty:ABORT_NOT_SERVER");
            return;
        }

        if (!nm.IsListening)
        {
            DLog("StartGameWithDifficulty() ABORT: nm not listening (DISCONNECTED?)");
            DumpState("StartGameWithDifficulty:ABORT_NOT_LISTENING");
            return;
        }

        if (nm.ConnectedClientsList.Count < 2)
        {
            DLog("StartGameWithDifficulty() ABORT: less than 2 clients (other player missing?)");
            DumpState("StartGameWithDifficulty:ABORT_CLIENTS<2");
            return;
        }

        var cfg = GameConfigNet.Instance;
        if (cfg == null)
        {
            DLog("StartGameWithDifficulty() ABORT: GameConfigNet.Instance is NULL");
            DumpState("StartGameWithDifficulty:ABORT_CFG_NULL");
            return;
        }

        // Normal game => ensure tutorial off.
        cfg.SetTutorialModeServerRpc(false);

        int seed = Random.Range(1, int.MaxValue);
        DLog($"StartGameWithDifficulty() -> generated seed={seed}");

        ApplyConfig(diff, seed);

        hostButtonsPanel?.SetActive(false);

        cfg.SetSkinSelectOpenServerRpc(true);
        DLog("StartGameWithDifficulty() -> SetSkinSelectOpenServerRpc(true) sent");

        if (lobbySkinUI != null)
        {
            lobbySkinUI.OpenSkinMenu();
            DLog("StartGameWithDifficulty() -> lobbySkinUI.OpenSkinMenu()");
        }
        else
        {
            DLog("StartGameWithDifficulty() -> lobbySkinUI is NULL (won't open locally)");
        }

        DumpState("StartGameWithDifficulty:AFTER");
    }

    private void ApplyConfig(int diff, int seed)
    {
        var cfg = GameConfigNet.Instance;
        if (cfg == null)
        {
            DLog("ApplyConfig() -> cfg NULL (should not happen here)");
            return;
        }

        DLog($"ApplyConfig(diff={diff}, seed={seed}) BEGIN");

        if (diff == 0)
        {
            cfg.SetConfigServerRpc(
                easyMazeW, easyMazeH,
                easyHearts, easyBombs, easyKeys,
                easyNormalDoors, easyPuzzleDoors,
                0, seed,
                easyLives,
                easyBombs,
                EASY_HINTS
            );
        }
        else if (diff == 1)
        {
            cfg.SetConfigServerRpc(
                medMazeW, medMazeH,
                medHearts, medBombs, medKeys,
                medNormalDoors, medPuzzleDoors,
                1, seed,
                medLives,
                0,
                MED_HINTS
            );
        }
        else
        {
            cfg.SetConfigServerRpc(
                hardMazeW, hardMazeH,
                hardHearts, hardBombs, hardKeys,
                hardNormalDoors, hardPuzzleDoors,
                2, seed,
                hardLives,
                0,
                HARD_HINTS
            );
        }

        DLog("ApplyConfig() -> SetConfigServerRpc SENT");
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
            sb.AppendLine($"  nm.IsListening={nm.IsListening} IsHost={nm.IsHost} IsServer={nm.IsServer} IsClient={nm.IsClient}");
            sb.AppendLine($"  LocalClientId={nm.LocalClientId} ServerClientId={NetworkManager.ServerClientId}");
            sb.AppendLine($"  ConnectedClientsList.Count={nm.ConnectedClientsList.Count}");

            sb.Append("  ConnectedClientIds=[");
            for (int i = 0; i < nm.ConnectedClientsList.Count; i++)
            {
                sb.Append(nm.ConnectedClientsList[i].ClientId);
                if (i < nm.ConnectedClientsList.Count - 1) sb.Append(", ");
            }
            sb.AppendLine("]");

            sb.AppendLine($"sceneMgr={(nm.SceneManager != null ? "OK" : "NULL")}");
        }

        var cfg = GameConfigNet.Instance;
        sb.AppendLine($"  GameConfigNet.Instance={(cfg ? cfg.name : "NULL")} cfgIsSpawned={(cfg != null && cfg.IsSpawned)}");
        if (cfg != null)
            sb.AppendLine($"  cfg.IsTutorial={cfg.IsTutorial.Value} size={cfg.MazeWidth.Value}x{cfg.MazeHeight.Value}");

        sb.AppendLine($"  lobbyState={(lobbyState ? lobbyState.name : "NULL")} lobbyIsSpawned={(lobbyState != null && lobbyState.IsSpawned)}");
        if (lobbyState != null && lobbyState.IsSpawned)
            sb.AppendLine($"  lobbyState.SessionFull={lobbyState.SessionFull.Value}");

        sb.AppendLine($"  lobbySkinUI={(lobbySkinUI ? lobbySkinUI.name : "NULL")}");
        sb.AppendLine($"  hostButtonsPanel={(hostButtonsPanel ? hostButtonsPanel.name : "NULL")} active={(hostButtonsPanel ? hostButtonsPanel.activeSelf : false)}");

        Debug.Log(sb.ToString());
    }

    private void DLog(string msg)
    {
        if (!verboseLogs) return;
        Debug.Log($"[HostStartGame] {msg} (scene={SceneManager.GetActiveScene().name}, obj={name})");
    }
}