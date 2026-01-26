// Assets/Scripts/Networking/PauseConsensus.cs
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

    private ulong _requesterClientId;
    private PauseAction _pendingAction;
    private bool _hasPending;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // נקרא מה-UI של המבקש (אחרי "כן" באישור המקומי)
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
            // אין שחקן שני -> מבצעים מיד
            ExecuteActionForAll(action);
            _hasPending = false;
            return;
        }

        // מציגים חלון אישור אצל השחקן השני בלבד
        ShowPeerRequestClientRpc(action, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { other } }
        });
    }

    // נקרא מה-UI של השחקן השני (כן/לא)
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

        // רק השחקן השני רשאי לענות
        if (other == ulong.MaxValue || responder != other) return;

        if (accept)
        {
            ExecuteActionForAll(_pendingAction);
        }
        else
        {
            // מודיעים למבקש שהשני לא אישר
            NotifyRequesterDeniedClientRpc(_pendingAction, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { _requesterClientId } }
            });
        }

        _hasPending = false;
    }

    [ClientRpc]
    private void ShowPeerRequestClientRpc(PauseAction action, ClientRpcParams clientRpcParams = default)
    {
        var ui = FindFirstObjectByType<CornerUIButtons>();
        if (ui != null) ui.ShowPeerRequest(action);
    }

    [ClientRpc]
    private void NotifyRequesterDeniedClientRpc(PauseAction action, ClientRpcParams clientRpcParams = default)
    {
        var ui = FindFirstObjectByType<CornerUIButtons>();
        if (ui != null) ui.ShowDeniedMessage(action);
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

    private void ExecuteActionForAll(PauseAction action)
    {
        // ✅ חובה שהשרת יוביל Scene Load כדי ששניהם יהיו מסונכרנים
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
        {
            if (action == PauseAction.ReplayLevel)
            {
                // נטען מחדש את הסצנה הפעילה (GameScene / TutorialScene וכו')
                string current = SceneManager.GetActiveScene().name;
                nm.SceneManager.LoadScene(current, LoadSceneMode.Single);
            }
            else if (action == PauseAction.GoToLevels)
            {
                // ✅ ניקוי UI state כדי שה-flow יתחיל נקי ב-StartScene
                var cfg = GameConfigNet.Instance;
                if (cfg != null)
                    cfg.SetSkinSelectOpenServerRpc(false);

                nm.SceneManager.LoadScene(startSceneName, LoadSceneMode.Single);
            }
            return;
        }

        // fallback (אם אין Netcode SceneManager)
        ExecuteLocalClientRpc(action);
    }

    [ClientRpc]
    private void ExecuteLocalClientRpc(PauseAction action)
    {
        if (action == PauseAction.ReplayLevel)
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        else if (action == PauseAction.GoToLevels)
            SceneManager.LoadScene(startSceneName);
    }
}
