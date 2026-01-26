// Assets/Scripts/Networking/PauseConsensus.cs
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseConsensus : NetworkBehaviour
{
    public enum PauseAction : byte
    {
        ReplayLevel = 1,
        GoToLevels = 2
    }

    public static PauseConsensus Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private string tutorialSceneName = "TutorialScene";

    // replay snapshot (server)
    private bool _replayIsTutorial;
    private int _replayDiff;
    private int _replaySeed;

    private ulong _requesterClientId;
    private PauseAction _pendingAction;
    private bool _hasPending;

    // post-load action
    private bool _pendingAfterStartScene;
    private PauseAction _afterStartSceneAction;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        UnhookSceneEvents();
    }

    private void HookSceneEvents()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
        {
            nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        }
    }

    private void UnhookSceneEvents()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
    }

    // =========================================================
    // Request / Respond
    // =========================================================

    public void RequestAction(PauseAction action)
    {
        if (!NetworkManager.Singleton) return;
        RequestActionServerRpc(action);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestActionServerRpc(PauseAction action, ServerRpcParams rpcParams = default)
    {
        if (_hasPending) return;

        _hasPending = true;
        _pendingAction = action;
        _requesterClientId = rpcParams.Receive.SenderClientId;

        ulong other = GetOtherClientId(_requesterClientId);
        if (other == ulong.MaxValue)
        {
            // no 2nd player -> execute immediately
            ExecuteActionForAll(action);
            _hasPending = false;
            return;
        }

        ShowPeerRequestClientRpc(action, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { other } }
        });
    }

    public void RespondToPeerRequest(bool accept)
    {
        RespondServerRpc(accept);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RespondServerRpc(bool accept, ServerRpcParams rpcParams = default)
    {
        if (!_hasPending) return;

        ulong responder = rpcParams.Receive.SenderClientId;
        ulong other = GetOtherClientId(_requesterClientId);

        // only the other player may respond
        if (other == ulong.MaxValue || responder != other) return;

        if (accept)
        {
            ExecuteActionForAll(_pendingAction);
        }
        else
        {
            // notify requester denied
            NotifyRequesterDeniedClientRpc(_pendingAction, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { _requesterClientId } }
            });
        }

        _hasPending = false;
    }

    // =========================================================
    // UI callbacks
    // =========================================================

    private static CornerUIButtons FindAnyCornerUI()
    {
        // scene-local (simple)
        return Object.FindFirstObjectByType<CornerUIButtons>();
    }

    [ClientRpc]
    private void ShowPeerRequestClientRpc(PauseAction action, ClientRpcParams clientRpcParams = default)
    {
        var ui = FindAnyCornerUI();
        if (ui != null) ui.ShowPeerRequest(action);
    }

    [ClientRpc]
    private void NotifyRequesterDeniedClientRpc(PauseAction action, ClientRpcParams clientRpcParams = default)
    {
        var ui = FindAnyCornerUI();
        if (ui != null) ui.OnLocalRequestDenied(action);
    }

    // =========================================================
    // Execute
    // =========================================================

    private void ExecuteActionForAll(PauseAction action)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SceneManager == null)
        {
            // fallback (shouldn’t happen in your project)
            ExecuteLocalClientRpc(action);
            return;
        }

        if (action == PauseAction.ReplayLevel)
            CaptureReplaySnapshot();

        // Always return to StartScene, then continue flow from there
        _pendingAfterStartScene = true;
        _afterStartSceneAction = action;

        HookSceneEvents();
        nm.SceneManager.LoadScene(startSceneName, LoadSceneMode.Single);
    }

    private void CaptureReplaySnapshot()
    {
        string active = SceneManager.GetActiveScene().name;
        _replayIsTutorial = (active == tutorialSceneName);

        // default safe
        _replayDiff = 0;
        _replaySeed = 1;

        // prefer reading from GameConfigNet if exists
        var cfg = GameConfigNet.Instance;
        if (cfg != null)
        {
            // try common member names (field/property) without compile coupling
            TryGetFirstInt(cfg, out _replayDiff,
                "difficulty", "Difficulty", "diff", "Diff", "SelectedDifficulty", "CurrentDifficulty");

            TryGetFirstInt(cfg, out _replaySeed,
                "seed", "Seed", "mazeSeed", "MazeSeed", "CurrentSeed");
        }
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!_pendingAfterStartScene) return;
        if (sceneName != startSceneName) return;

        // do only on HOST (server with UI)
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsHost) return;

        _pendingAfterStartScene = false;
        UnhookSceneEvents();

        if (_afterStartSceneAction == PauseAction.GoToLevels)
        {
            // open level select UI exactly like normal lobby state
            var relay = Object.FindFirstObjectByType<RelayUIController>();
            if (relay != null) relay.ForceOpenLevelSelectFromGame();
            return;
        }

        if (_afterStartSceneAction == PauseAction.ReplayLevel)
        {
            var starter = Object.FindFirstObjectByType<HostStartGame>();
            if (starter == null)
            {
                Debug.LogError("[PauseConsensus] HostStartGame not found in StartScene.");
                return;
            }

            if (_replayIsTutorial)
            {
                starter.StartTutorial();
            }
            else
            {
                starter.StartGameWithDifficultyAndSeed(_replayDiff, _replaySeed);
            }
        }
    }

    [ClientRpc]
    private void ExecuteLocalClientRpc(PauseAction action)
    {
        // fallback (local)
        if (action == PauseAction.ReplayLevel || action == PauseAction.GoToLevels)
            SceneManager.LoadScene(startSceneName);
    }

    private ulong GetOtherClientId(ulong requester)
    {
        if (!NetworkManager.Singleton) return ulong.MaxValue;

        foreach (var kv in NetworkManager.Singleton.ConnectedClients)
        {
            ulong id = kv.Key;
            if (id != requester) return id;
        }
        return ulong.MaxValue;
    }

    // =========================================================
    // Reflection helpers
    // =========================================================
    private static bool TryGetFirstInt(object obj, out int value, params string[] names)
    {
        value = default;
        foreach (var n in names)
        {
            if (TryGetMember(obj, n, out int v))
            {
                value = v;
                return true;
            }
        }
        return false;
    }

    private static bool TryGetMember<T>(object obj, string name, out T value)
    {
        value = default;
        if (obj == null || string.IsNullOrEmpty(name)) return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var t = obj.GetType();

        var f = t.GetField(name, flags);
        if (f != null)
        {
            object raw = f.GetValue(obj);
            if (raw is T cast)
            {
                value = cast;
                return true;
            }
            try
            {
                value = (T)System.Convert.ChangeType(raw, typeof(T));
                return true;
            }
            catch { return false; }
        }

        var p = t.GetProperty(name, flags);
        if (p != null && p.CanRead)
        {
            object raw = p.GetValue(obj);
            if (raw is T cast)
            {
                value = cast;
                return true;
            }
            try
            {
                value = (T)System.Convert.ChangeType(raw, typeof(T));
                return true;
            }
            catch { return false; }
        }

        return false;
    }
}
