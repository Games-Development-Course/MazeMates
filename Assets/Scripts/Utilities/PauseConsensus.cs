// Assets/Scripts/Networking/PauseConsensus.cs
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;


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
    [SerializeField] private string gameSceneName = "GameScene";
    // replay snapshot (server)
    private bool _replayIsTutorial;
    private int _replayDiff;
    private int _replaySeed;
    private string _replaySceneName; 




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
            ExecuteLocalClientRpc(action);
            return;
        }

        if (action == PauseAction.ReplayLevel)
        {
            // שמור snapshot (לא חובה לרילואד עצמו, אבל נשאר לתאימות/לוגים)
            CaptureReplaySnapshot();

            // ✅ Restart אמיתי: Reload של אותה סצנה משחקית דרך NGO
            ReplayLevelInPlace(nm);
            return;
        }

        // GoToLevels נשאר כמו שהיה: חוזרים ל-StartScene
        _pendingAfterStartScene = true;
        _afterStartSceneAction = action;

        HookSceneEvents();
        nm.SceneManager.LoadScene(startSceneName, LoadSceneMode.Single);
    }

    private void ReplayLevelInPlace(NetworkManager nm)
    {
        // Server/Host בלבד
        if (!IsServer) return;

        // איזה סצנה אנחנו מרעננים?
        // אם אתה תמיד עובד עם GameScene (גם טיוטוריאל נטען ל-GameScene), זה יהיה GameScene.
        // אם לפעמים יש באמת TutorialScene, זה יתפוס גם את זה.
        _replaySceneName = SceneManager.GetActiveScene().name;

        // Safety: אם מסיבה כלשהי אנחנו לא בסצנה משחקית, תיפול ל-GameScene
        if (_replaySceneName != gameSceneName && _replaySceneName != tutorialSceneName)
            _replaySceneName = gameSceneName;

        // ✅ CLEANUP לפני LoadScene (רק אובייקטים של הרמה, בלי שחקנים ובלי DontDestroy)
        CleanupLevelNetworkObjects(_replaySceneName);

        // ✅ Reload דרך Netcode SceneManager כדי שכל הלקוחות יסתנכרנו
        nm.SceneManager.LoadScene(_replaySceneName, LoadSceneMode.Single);
    }

    private void CleanupLevelNetworkObjects(string activeSceneName)
    {
        if (!IsServer) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Snapshot כדי שאפשר יהיה Despawn בלי לשבור את האוסף בזמן איטרציה
        var spawned = nm.SpawnManager.SpawnedObjectsList;
        if (spawned == null) return;

        var snapshot = spawned.ToArray(); // עובד גם אם זה HashSet וגם אם זה List

        for (int i = snapshot.Length - 1; i >= 0; i--)
        {
            var no = snapshot[i];
            if (no == null) continue;

            // ❌ לא נוגעים בשחקנים
            if (no.IsPlayerObject) continue;

            // ❌ לא נוגעים באובייקטים שנמצאים ב-DontDestroyOnLoad
            var sceneName = no.gameObject.scene.name;
            if (sceneName == "DontDestroyOnLoad") continue;

            // ✅ מנקים רק אובייקטים ששייכים לסצנה הנוכחית של המשחק
            if (sceneName != activeSceneName) continue;

            // Despawn+Destroy
            if (no.IsSpawned)
                no.Despawn(destroy: true);
            else
                Destroy(no.gameObject);
        }

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
