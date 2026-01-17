// Assets/Scripts/Utilities/RelayAutoFlow.cs
// Stable AutoFlow (restored):
// - Waits for MPE readiness before deciding role
// - Client waits & retries for join code (~1000ms cadence), no fast-fail
// - Prefer MPE Tags HOST/CLIENT
// - Cmdline is fallback only and normalized ("Player 1" -> "Player1")

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

    [Header("Client retry (stable)")]
    [SerializeField] private int clientJoinRetries = 60;     // ~60 seconds
    [SerializeField] private int joinRetryDelayMs = 1000;    // ~1000ms cadence

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
        await WaitForNetworkManagerReady();

        // 🔒 Restore stability: wait for MPE to be ready before deciding role
        await WaitForMpeReady();

        Role role = DecideRoleDeterministic(out string reason);
        Debug.Log($"[RelayAutoFlow] role={role} ({reason}) | platform={Application.platform}");

        // Client does NOTHING before host published join code
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

    // ---------------------------
    // Readiness gates
    // ---------------------------

    private async Task WaitForNetworkManagerReady()
    {
        float t = 0f;
        while (NetworkManager.Singleton == null)
        {
            await Task.Delay(50);
            t += 0.05f;
            if (t > 10f)
            {
                Debug.LogWarning("[RelayAutoFlow] Waiting for NetworkManager.Singleton...");
                t = 0f;
            }
        }
        await Task.Yield();
    }

    private async Task WaitForMpeReady()
    {
#if UNITY_EDITOR
        // ~1–2 seconds patience prevents falling to cmdline too early
        for (int i = 0; i < 120; i++)
        {
            if (TryGetMpeCurrentPlayerTags(out var tags) && tags != null && tags.Count > 0)
                return;

            if (TryGetMpeIsMainEditor(out _))
                return;

            await Task.Delay(20);
        }
#endif
    }

    // ---------------------------
    // Role decision (stable order)
    // ---------------------------

    private Role DecideRoleDeterministic(out string reason)
    {
#if UNITY_EDITOR
        // 1) Prefer MPE Tags
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

        // 2) Prefer IsMainEditor
        if (TryGetMpeIsMainEditor(out bool isMain))
        {
            reason = $"IsMainEditor={isMain}";
            return isMain ? Role.Host : Role.Client;
        }

        // 3) Fallback: cmdline -name Player1/Player 1...
        if (TryGetCmdlinePlayerName(out string pname))
        {
            string norm = new string(pname.Where(char.IsLetterOrDigit).ToArray()); // Player 1 -> Player1
            if (string.Equals(norm, "Player1", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"-name {pname}";
                return Role.Host;
            }
            if (norm.StartsWith("Player", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"-name {pname}";
                return Role.Client;
            }
        }

        // 4) Fallback: player index
        if (TryGetMpePlayerIndex(out int idx))
        {
            reason = $"playerIndex={idx}";
            return idx == 0 ? Role.Host : Role.Client;
        }

        // 5) Regular editor play
        reason = "regular editor";
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
        catch { return false; }
    }
#endif

    // ---------------------------
    // Host / Client
    // ---------------------------

    private async Task<bool> WaitForHostJoinCodeSignal(float timeoutSeconds)
    {
        float elapsed = 0f;
        while (elapsed < timeoutSeconds)
        {
#if UNITY_EDITOR
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

            await Task.Delay(250);
            elapsed += 0.25f;
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

            NetworkManager.Singleton.StartHost();
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
                Debug.Log($"[RelayAutoFlow] Client waiting for join code... ({attempt}/{retries})");
                await Task.Delay(joinRetryDelayMs);
                continue;
            }

            try
            {
                JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(code);

                var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
                utp.SetRelayServerData(new RelayServerData(joinAlloc, "wss"));
                utp.UseWebSockets = true;

                Debug.Log($"[RelayAutoFlow] CLIENT joining with code: {code}");
                NetworkManager.Singleton.StartClient();
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RelayAutoFlow] Client join failed (attempt {attempt}): {e}");
                await Task.Delay(joinRetryDelayMs);
            }
        }

        Debug.LogError("[RelayAutoFlow] Client failed to join after retries.");
    }

    private void OnAnyClientConnectedOnHost()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;

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
            if (!File.Exists(JoinCodeFile)) return false;

            string text = File.ReadAllText(JoinCodeFile);
            if (string.IsNullOrWhiteSpace(text)) return false;

            int sep = text.IndexOf('|');
            if (sep <= 0 || sep >= text.Length - 1) return false;

            if (!long.TryParse(text.Substring(0, sep), out long unix)) return false;
            if ((DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unix) > joinCodeMaxAgeSeconds) return false;

            code = text.Substring(sep + 1).Trim();
            return !string.IsNullOrEmpty(code);
        }
        catch { return false; }
    }

    // ---------------------------
    // Unity Services
    // ---------------------------

    private static async Task EnsureUnityServicesSignedIn_WithRetries()
    {
        for (int attempt = 1; attempt <= 3; attempt++)
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
    }
}
