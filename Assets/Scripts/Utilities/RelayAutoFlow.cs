using System.Threading.Tasks;
using MazeMates.Authentication;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class RelayAutoFlow : MonoBehaviour
{
    [Header("Auto")]
    [SerializeField] private bool autoLoadGameScene = true;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private int expectedPlayers = 2;

    [Header("Flow Control")]
    [Tooltip("If true: when all players connected, DO NOT load scene automatically. Host can show UI and later call HostLoadGameSceneNow().")]
    [SerializeField] private bool pauseAfterAllPlayersConnected = true;

    [Header("Dev / Safety")]
    [Tooltip("When using Unity Multiplayer Play Mode (Virtual Players), PlayerIndex=0 becomes Host and PlayerIndex>0 becomes Client.")]
    [SerializeField] private bool useEditorMultiplayerPlayModeRole = true;

    [Tooltip("If true, clears cached join/lock keys at the start of play (recommended for Editor/MPE).")]
    [SerializeField] private bool clearStoredKeysOnStart = true;

    [Header("Client Join Retry")]
    [SerializeField] private int clientJoinRetries = 3;
    [SerializeField] private int joinRetryDelayMs = 400;

    // Fallback keys (non-MPE editor cases)
    private const string JoinKey = "mm_join_code";
    private const string HostLockKey = "mm_host_lock";

#if UNITY_EDITOR
    // Unity Multiplayer Play Mode (Virtual Players) in Editor: shared across virtual players (same process)
    private static string s_editorJoinCode;
#endif

    public bool AllPlayersConnected { get; private set; }
    public event System.Action PlayersReadyOnHost;

    private bool _subscribedHostCallback;

    private async void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[RelayAutoFlow] NetworkManager missing");
            return;
        }

        if (clearStoredKeysOnStart)
        {
#if UNITY_EDITOR
            s_editorJoinCode = null;
#endif
            PlayerPrefs.DeleteKey(JoinKey);
            PlayerPrefs.DeleteKey(HostLockKey);
            PlayerPrefs.Save();
        }

        bool ready = await EnsureUnityServicesReadyAndRequireSignIn();
        if (!ready)
        {
            Debug.LogWarning("[RelayAutoFlow] Not signed in. Relay auto flow will not start.");
            return;
        }

        bool isHost = DecideIsHost();
        Debug.Log($"[RelayAutoFlow] role={(isHost ? "HOST" : "CLIENT")}");

        if (isHost)
            await StartHost();
        else
            await StartClient();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && _subscribedHostCallback)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= _ => OnAnyClientConnectedOnHost();
            _subscribedHostCallback = false;
        }
    }

    private bool DecideIsHost()
    {
#if UNITY_EDITOR
        if (useEditorMultiplayerPlayModeRole && IsMultiplayerPlayModeEnabled(out int playerIndex))
            return playerIndex == 0;
#endif
        var lockVal = PlayerPrefs.GetString(HostLockKey, null);
        if (!string.IsNullOrEmpty(lockVal))
            return false;

        PlayerPrefs.SetString(HostLockKey, Time.realtimeSinceStartup.ToString("0.000"));
        PlayerPrefs.Save();
        return true;
    }

    private async Task StartHost()
    {
        int maxClients = Mathf.Max(0, expectedPlayers - 1);
        Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxClients);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utp.SetRelayServerData(new RelayServerData(alloc, "wss"));
        utp.UseWebSockets = true;

#if UNITY_EDITOR
        if (useEditorMultiplayerPlayModeRole && IsMultiplayerPlayModeEnabled(out _))
            s_editorJoinCode = joinCode;
#endif

        PlayerPrefs.SetString(JoinKey, joinCode);
        PlayerPrefs.Save();

        Debug.Log($"[RelayAutoFlow] HOST join code: {joinCode}");

        bool started = NetworkManager.Singleton.StartHost();
        Debug.Log($"[RelayAutoFlow] StartHost={started}");

        // Listen for client connections to detect "players ready"
        if (!_subscribedHostCallback)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += _ => OnAnyClientConnectedOnHost();
            _subscribedHostCallback = true;
        }

        if (!pauseAfterAllPlayersConnected && autoLoadGameScene)
        {
            // legacy behavior placeholder (kept intentionally)
        }
    }

    private void OnAnyClientConnectedOnHost()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        int total = NetworkManager.Singleton.ConnectedClientsList.Count; // includes host
        Debug.Log($"[RelayAutoFlow] Connected clients: {total}/{expectedPlayers}");

        if (AllPlayersConnected) return;

        if (total >= expectedPlayers)
        {
            AllPlayersConnected = true;
            Debug.Log("[RelayAutoFlow] All players connected.");

            if (pauseAfterAllPlayersConnected)
            {
                PlayersReadyOnHost?.Invoke();
                return;
            }

            if (autoLoadGameScene)
            {
                HostLoadGameSceneNow();
            }
        }
    }

    public void HostLoadGameSceneNow()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (NetworkManager.Singleton.SceneManager == null) return;

        Debug.Log($"[RelayAutoFlow] Host loading {gameSceneName}");
        NetworkManager.Singleton.SceneManager.LoadScene(
            gameSceneName,
            UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private async Task StartClient()
    {
        int retries = Mathf.Max(1, clientJoinRetries);

        for (int attempt = 1; attempt <= retries; attempt++)
        {
            string code = await WaitForJoinCode(10f);
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogError("[RelayAutoFlow] No join code found (timeout).");
                return;
            }

            try
            {
                JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(code);

                var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
                utp.SetRelayServerData(new RelayServerData(joinAlloc, "wss"));
                utp.UseWebSockets = true;

                Debug.Log($"[RelayAutoFlow] CLIENT joining with code: {code}");

                bool started = NetworkManager.Singleton.StartClient();
                Debug.Log($"[RelayAutoFlow] StartClient={started}");
                return;
            }
            catch (RelayServiceException e) when (IsJoinCodeNotFound(e))
            {
                Debug.LogWarning($"[RelayAutoFlow] Join code not found/expired. Clearing cached code and retrying {attempt}/{retries}...");

#if UNITY_EDITOR
                s_editorJoinCode = null;
#endif
                PlayerPrefs.DeleteKey(JoinKey);
                PlayerPrefs.Save();

                await Task.Delay(joinRetryDelayMs);
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[RelayAutoFlow] Relay join failed: {e}");
                return;
            }
        }

        Debug.LogError("[RelayAutoFlow] Failed to join after retries.");
    }

    private async Task<string> WaitForJoinCode(float timeoutSeconds)
    {
#if UNITY_EDITOR
        if (useEditorMultiplayerPlayModeRole && IsMultiplayerPlayModeEnabled(out int playerIndex))
        {
            if (playerIndex > 0)
            {
                float t = 0f;
                while (t < timeoutSeconds)
                {
                    if (!string.IsNullOrEmpty(s_editorJoinCode))
                        return s_editorJoinCode;

                    await Task.Delay(100);
                    t += 0.1f;
                }
                return null;
            }
        }
#endif

        float elapsed = 0f;
        while (elapsed < timeoutSeconds)
        {
            var code = PlayerPrefs.GetString(JoinKey, null);
            if (!string.IsNullOrEmpty(code))
                return code;

            await Task.Delay(200);
            elapsed += 0.2f;
        }

        return null;
    }

    private static bool IsJoinCodeNotFound(System.Exception e)
    {
        string t = e.ToString();
        return t.Contains("404") || t.Contains("Not Found") || t.Contains("join code not found");
    }

    /// <summary>
    /// Initializes Unity Services, but does NOT auto sign-in.
    /// Requires the player to already be signed in (via your Auth UI).
    /// </summary>
    private static async Task<bool> EnsureUnityServicesReadyAndRequireSignIn()
    {
        // Prefer your Auth manager initialization (keeps same profile/options)
        if (UgsAuthManager.Instance != null)
        {
            await UgsAuthManager.Instance.InitializeAsync();
            return UgsAuthManager.Instance.IsSignedIn;
        }

        // Fallback: initialize services directly, but do not sign-in.
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        return AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;
    }

#if UNITY_EDITOR
    private static bool IsMultiplayerPlayModeEnabled(out int playerIndex)
    {
        playerIndex = 0;

        try
        {
            var mpeType = typeof(Editor).Assembly.GetType("UnityEditor.MPE.MultiplayerPlayMode");
            if (mpeType == null) return false;

            var isEnabled = mpeType.GetMethod("IsEnabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var getIndex = mpeType.GetMethod("GetCurrentPlayerIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (isEnabled == null || getIndex == null) return false;

            bool enabled = (bool)isEnabled.Invoke(null, null);
            if (!enabled) return false;

            playerIndex = (int)getIndex.Invoke(null, null);
            return true;
        }
        catch
        {
            return false;
        }
    }
#endif
}
