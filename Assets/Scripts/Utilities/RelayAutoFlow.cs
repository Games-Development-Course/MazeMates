// Assets/Scripts/Utilities/RelayAutoFlow.cs
// Deterministic MPE Host/Client selection (Editor hosts, Player2 joins) + hard gating:
// - Prefer MPE Tags: HOST / CLIENT
// - Else prefer CurrentPlayer.IsMainEditor
// - Else prefer cmdline -name Player1/Player2...
// - Else fallback to UnityEditor.MPE playerIndex (0 host)
// Client does NOTHING (even no Unity Services sign-in) until Host published join code.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
    private enum Role { Host, Client }

    [Header("Players")]
    [SerializeField] private int expectedPlayers = 2;

    [Header("Scene (optional)")]
    [SerializeField] private bool autoLoadGameScene = false;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private bool pauseAfterAllPlayersConnected = true;

    [Header("Join code gate")]
    [SerializeField] private float hostSignalWaitSeconds = 120f;
    [SerializeField] private float joinCodeMaxAgeSeconds = 600f; // 10 min

    [Header("Client retry")]
    [SerializeField] private int clientJoinRetries = 6;
    [SerializeField] private int joinRetryDelayMs = 500;

    // Shared Join Code file (works across MPE virtual player processes)
    // Format: "<unixSeconds>|<joinCode>"
    private static string SharedDir => Path.Combine(Path.GetTempPath(), "MazeMates");
    private static string JoinCodeFile => Path.Combine(SharedDir, "relay_joincode.txt");

#if UNITY_EDITOR
    // same-process convenience (not relied upon)
    private static string s_editorJoinCode;
#endif

    private string _prefetchedJoinCode;

    public bool AllPlayersConnected { get; private set; }
    public event Action PlayersReadyOnHost;

    private async void Start()
    {
        // Wait until NetworkManager exists (fixes early OnEnable order issues in some scenes)
        await WaitForNetworkManagerReady();

        Role role = DecideRoleDeterministic(out string reason);
        Debug.Log($"[RelayAutoFlow] role={role} ({reason}) | platform={Application.platform} | buildTargetWebGL={(IsBuildTargetWebGL() ? "YES" : "NO")}");

        // ✅ CLIENT does NOTHING before host published join code
        if (role == Role.Client)
        {
            bool ok = await WaitForHostJoinCodeSignal(hostSignalWaitSeconds);
            if (!ok)
            {
                Debug.LogError("[RelayAutoFlow] Timeout waiting for host join code.");
                return;
            }
        }

        await EnsureUnityServicesSignedIn_WithRetries();

        if (role == Role.Host) await StartHost();
        else await StartClient();
    }

    private async Task WaitForNetworkManagerReady()
    {
        float t = 0f;
        while (NetworkManager.Singleton == null)
        {
            await Task.Delay(50);
            t += 0.05f;
            if (t > 10f)
            {
                Debug.LogWarning("[RelayAutoFlow] Still waiting for NetworkManager.Singleton...");
                t = 0f;
            }
        }

        // wait one frame so components settle
        await Task.Yield();

        if (NetworkManager.Singleton.GetComponent<UnityTransport>() == null)
            Debug.LogWarning("[RelayAutoFlow] UnityTransport missing on NetworkManager.");
    }

    // ---------------------------
    // Role decision (deterministic)
    // ---------------------------

    private Role DecideRoleDeterministic(out string reason)
    {
#if UNITY_EDITOR
        // 1) Prefer Tags: HOST/CLIENT
        if (TryGetMpeCurrentPlayerTags(out var tags))
        {
            if (tags.Contains("HOST"))
            {
                reason = "tag=HOST";
                return Role.Host;
            }
            if (tags.Contains("CLIENT"))
            {
                reason = "tag=CLIENT";
                return Role.Client;
            }
        }

        // 2) Prefer IsMainEditor (most stable when available)
        if (TryGetMpeIsMainEditor(out bool isMain) && isMain)
        {
            reason = "IsMainEditor=true";
            return Role.Host;
        }
        if (TryGetMpeIsMainEditor(out isMain) && !isMain)
        {
            reason = "IsMainEditor=false";
            return Role.Client;
        }

        // 3) Prefer command line -name Player1/Player2...
        if (TryGetCmdlinePlayerName(out string pname))
        {
            if (string.Equals(pname, "Player1", StringComparison.OrdinalIgnoreCase))
            {
                reason = "-name Player1";
                return Role.Host;
            }
            if (pname.StartsWith("Player", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"-name {pname}";
                return Role.Client;
            }
        }

        // 4) Fallback: UnityEditor.MPE player index
        if (TryGetMpePlayerIndex(out int idx))
        {
            reason = $"playerIndex={idx}";
            return (idx == 0) ? Role.Host : Role.Client;
        }

        // 5) Not MPE (regular editor play)
        reason = "regular editor play";
        return Role.Host;
#else
        reason = "build";
        return Role.Client;
#endif
    }

#if UNITY_EDITOR
    private static bool TryGetCmdlinePlayerName(out string name)
    {
        name = null;
        try
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-name")
                {
                    name = args[i + 1];
                    return !string.IsNullOrWhiteSpace(name);
                }
            }
        }
        catch { }
        return false;
    }

    private static bool TryGetMpeIsMainEditor(out bool isMainEditor)
    {
        isMainEditor = false;
        try
        {
            // Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor (package dependent)
            var t = FindTypeInLoadedAssemblies("Unity.Multiplayer.PlayMode.CurrentPlayer");
            if (t == null) return false;

            var p = t.GetProperty("IsMainEditor", BindingFlags.Public | BindingFlags.Static);
            if (p == null) return false;

            object v = p.GetValue(null);
            if (v is bool b)
            {
                isMainEditor = b;
                return true;
            }
        }
        catch { }
        return false;
    }

    private static bool TryGetMpeCurrentPlayerTags(out HashSet<string> tags)
    {
        tags = null;
        try
        {
            var t = FindTypeInLoadedAssemblies("Unity.Multiplayer.PlayMode.CurrentPlayer");
            if (t == null) return false;

            var p = t.GetProperty("Tags", BindingFlags.Public | BindingFlags.Static);
            if (p == null) return false;

            object v = p.GetValue(null);
            if (v is IEnumerable<string> es)
            {
                tags = new HashSet<string>(
                    es.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()),
                    StringComparer.OrdinalIgnoreCase);
                return tags.Count > 0;
            }
        }
        catch { }
        return false;
    }

    private static Type FindTypeInLoadedAssemblies(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            catch { }
        }
        return null;
    }

    private static bool TryGetMpePlayerIndex(out int playerIndex)
    {
        playerIndex = 0;
        try
        {
            var mpeType = typeof(Editor).Assembly.GetType("UnityEditor.MPE.MultiplayerPlayMode");
            if (mpeType == null) return false;

            var isEnabled = mpeType.GetMethod("IsEnabled", BindingFlags.Public | BindingFlags.Static);
            var getIndex = mpeType.GetMethod("GetCurrentPlayerIndex", BindingFlags.Public | BindingFlags.Static);
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

    private bool IsBuildTargetWebGL()
    {
#if UNITY_EDITOR
        try { return EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL; }
        catch { return false; }
#else
        return false;
#endif
    }

    // ---------------------------
    // Host / Client
    // ---------------------------

    private async Task<bool> WaitForHostJoinCodeSignal(float timeoutSeconds)
    {
        float elapsed = 0f;
        while (elapsed < timeoutSeconds)
        {
#if UNITY_EDITOR
            // optional convenience
            if (!string.IsNullOrEmpty(s_editorJoinCode))
            {
                _prefetchedJoinCode = s_editorJoinCode;
                return true;
            }
#endif
            if (TryReadJoinCodeFromFile(out string code))
            {
                _prefetchedJoinCode = code;
                return true;
            }

            await Task.Delay(100);
            elapsed += 0.1f;
        }

        return false;
    }

    private async Task StartHost()
    {
        try
        {
            PrepareSharedJoinCodeFileForHost();

            int maxClients = Mathf.Max(0, expectedPlayers - 1);
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxClients);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetRelayServerData(new RelayServerData(alloc, "wss"));
            utp.UseWebSockets = true;

#if UNITY_EDITOR
            s_editorJoinCode = joinCode;
#endif
            PublishJoinCode(joinCode);

            Debug.Log($"[RelayAutoFlow] HOST join code: {joinCode}");

            bool started = NetworkManager.Singleton.StartHost();
            Debug.Log($"[RelayAutoFlow] StartHost={started}");

            NetworkManager.Singleton.OnClientConnectedCallback += _ => OnAnyClientConnectedOnHost();
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayAutoFlow] HOST failed: {e}");
        }
    }

    private async Task StartClient()
    {
        int retries = Mathf.Max(1, clientJoinRetries);

        for (int attempt = 1; attempt <= retries; attempt++)
        {
            string code = _prefetchedJoinCode;

            if (string.IsNullOrEmpty(code) && TryReadJoinCodeFromFile(out var fileCode))
                code = fileCode;

#if UNITY_EDITOR
            if (string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(s_editorJoinCode))
                code = s_editorJoinCode;
#endif

            if (string.IsNullOrEmpty(code))
            {
                Debug.LogError("[RelayAutoFlow] No join code available for client.");
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
                int backoff = Mathf.Clamp(joinRetryDelayMs * attempt, 300, 5000);
                Debug.LogWarning($"[RelayAutoFlow] Join code not found/expired. Retrying {attempt}/{retries} after {backoff}ms...");

                _prefetchedJoinCode = null;
#if UNITY_EDITOR
                s_editorJoinCode = null;
#endif
                await Task.Delay(backoff);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RelayAutoFlow] Client failed: {e}");
                return;
            }
        }

        Debug.LogError("[RelayAutoFlow] Failed to join after retries.");
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

            if (autoLoadGameScene && NetworkManager.Singleton.SceneManager != null)
            {
                Debug.Log($"[RelayAutoFlow] Host loading {gameSceneName}");
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
    }

    // ---------------------------
    // Join code file helpers
    // ---------------------------

    private void PrepareSharedJoinCodeFileForHost()
    {
        try
        {
            Directory.CreateDirectory(SharedDir);
            if (File.Exists(JoinCodeFile))
                File.Delete(JoinCodeFile);
        }
        catch { }
    }

    private void PublishJoinCode(string joinCode)
    {
        try
        {
            Directory.CreateDirectory(SharedDir);
            long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            File.WriteAllText(JoinCodeFile, $"{unix}|{joinCode}");
        }
        catch { }
    }

    private bool TryReadJoinCodeFromFile(out string code)
    {
        code = null;

        try
        {
            if (!File.Exists(JoinCodeFile))
                return false;

            string text = File.ReadAllText(JoinCodeFile);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            int sep = text.IndexOf('|');
            if (sep <= 0 || sep >= text.Length - 1)
                return false;

            string tsStr = text.Substring(0, sep).Trim();
            string joinCode = text.Substring(sep + 1).Trim();

            if (string.IsNullOrEmpty(joinCode))
                return false;

            if (!long.TryParse(tsStr, out long unix))
                return false;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long age = now - unix;
            if (age < 0) age = 0;

            if (age > (long)joinCodeMaxAgeSeconds)
                return false;

            code = joinCode;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsJoinCodeNotFound(Exception e)
    {
        string t = e.ToString();
        return t.Contains("404") || t.Contains("Not Found") || t.Contains("join code not found");
    }

    // ---------------------------
    // Unity Services
    // ---------------------------

    private static async Task EnsureUnityServicesSignedIn_WithRetries()
    {
        const int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                return;
            }
            catch
            {
                await Task.Delay(400 * attempt);
            }
        }

        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
