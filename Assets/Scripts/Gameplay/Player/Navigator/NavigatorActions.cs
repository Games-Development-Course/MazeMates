// File: Assets/Scripts/Gameplay/Player/Navigator/NavigatorActions.cs
// שינוי: איחוד OpenDoor + ShowPuzzle + VictoryDoor לכפתור אחד (UI_OpenDoor)
// הלוגיקה רצה בשרת (ServerRpc) כדי להסתמך על PadTrigger בצורה authoritative.

using Unity.Netcode;
using UnityEngine;

public class NavigatorActions : NetworkBehaviour
{
    public static NavigatorActions Instance { get; private set; }

    private TutorialManager tutorial;

    private void Awake()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NavigatorActions][Awake] enabled={enabled} active={gameObject.activeSelf} hierarchyActive={gameObject.activeInHierarchy}"
        );
    }

    private void Start()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NavigatorActions][Start] enabled={enabled} active={gameObject.activeSelf} hierarchyActive={gameObject.activeInHierarchy}"
        );
    }

    public override void OnNetworkSpawn()
    {
        // owner instance (navigator client)
        if (IsOwner)
            Instance = this;

        tutorial = Object.FindFirstObjectByType<TutorialManager>();

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NavigatorActions][OnNetworkSpawn] IsOwner={IsOwner} IsHost={IsHost} IsServer={IsServer} IsSpawned={IsSpawned} LocalClientId={(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 9999)}"
        );
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[NavigatorActions][OnDestroy] Instance cleared"
        );
    }

    // =====================================================================
    // UI — BUTTON EVENTS
    // =====================================================================

    public void UI_OpenDoor()
    {
        // 1) Always try to route to the real local navigator instance (owner+spawned).
        if (TryRouteToLocalOwnerInstance(nameof(UI_OpenDoor)))
            return;

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NAV-ACT] UI_OpenDoor pressed | IsOwner={IsOwner} IsHost={IsHost} IsServer={IsServer} IsSpawned={IsSpawned}"
        );

        if (!IsLocalNavigator())
        {
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "[NAV-ACT] UI_OpenDoor aborted: not local navigator (not owner)"
            );
            return;
        }

        if (!IsSpawned)
        {
            Debug.LogWarning("[NAV-ACT] UI_OpenDoor aborted: NetworkObject not spawned yet");
            HUDManager.Instance?.ShowMessageForNavigator("הנווט עדיין נטען… נסה שוב בעוד רגע");
            return;
        }

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[NAV-ACT] Sending UseDoorOnTravellerPadServerRpc()");
        UseDoorOnTravellerPadServerRpc();
    }

    // Backward compatibility
    public void UI_ShowPuzzle() => UI_OpenDoor();
    public void UI_VictoryDoor() => UI_OpenDoor();

    private bool IsLocalNavigator() => IsOwner;

    // =====================================================================
    // SERVER AUTHORITATIVE DOOR ACTIONS (unified)
    // =====================================================================

    [ServerRpc(RequireOwnership = false)]
    private void UseDoorOnTravellerPadServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        Debug.Log($"[NAV-ACT][ServerRpc] UseDoorOnTravellerPadServerRpc from clientId={sender}");

        var travellerObj = FindTravellerNetworkObject();
        if (travellerObj == null)
        {
            Debug.LogWarning("[NAV-ACT][ServerRpc] Traveller NetworkObject not found.");
            SendNavigatorMessageTargetClientRpc("לא נמצא מטייל בסצנה", MakeTargetParams(sender));
            return;
        }

        DoorController door = FindDoorTravellerIsStandingOnServer(null);

        Debug.Log($"[NAV-ACT][ServerRpc] FindDoorTravellerIsStandingOnServer -> {(door == null ? "NULL" : door.name)}");

        if (door == null)
        {
            SendNavigatorMessageTargetClientRpc("אין דלת כאן", MakeTargetParams(sender));
            return;
        }

        Debug.Log($"[NAV-ACT][ServerRpc] Door type={door.doorType} open={door.IsOpen()} name={door.name}");

        if (door.doorType == DoorType.Puzzle)
        {
            Debug.Log($"[NAV-ACT][ServerRpc] Puzzle door -> RequestOpenPuzzleDoorRpc() for {door.name}");
            door.RequestOpenPuzzleDoorServerRpc();
            tutorial?.NotifyNavigatorOpenedPuzzleDoor();
            return;
        }

        if (door.doorType == DoorType.Exit)
        {
            if (GameManager.Instance != null && !GameManager.Instance.AllKeysCollected())
            {
                SendNavigatorMessageTargetClientRpc("עליך לאסוף את כל המפתחות", MakeTargetParams(sender));
                return;
            }

            Debug.Log($"[NAV-ACT][ServerRpc] Exit door -> Interact() for {door.name}");
            door.Interact();
            return;
        }

        Debug.Log($"[NAV-ACT][ServerRpc] Normal door -> Interact() for {door.name}");
        door.Interact();
        tutorial?.NotifyNavigatorOpenedNormalDoor();
    }

    private static DoorController FindDoorTravellerIsStandingOnServer(DoorType? filter)
    {
        var pads = Object.FindObjectsOfType<PadTrigger>(true);

        for (int i = 0; i < pads.Length; i++)
        {
            var pad = pads[i];
            if (pad == null) continue;

            if (!pad.IsPlayerOnPad())
                continue;

            var door = pad.GetComponentInParent<DoorController>();
            if (door == null) continue;

            if (filter.HasValue && door.doorType != filter.Value)
                continue;

            return door;
        }

        return null;
    }

    // =====================================================================
    // Existing features (kept)
    // =====================================================================

    public void UI_RemoveBomb()
    {
        // ✅ ignore on traveller client (fallback: host)
        if (IsLocalTravellerClient())
        {
            Debug.Log("[NAV] UI_RemoveBomb ignored on Traveller/Host client");
            return;
        }

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        Debug.Log("[NAV] UI_RemoveBomb -> TryRemoveBomb()");
        ResourceManager.Instance.TryRemoveBomb();
    }

    public void UI_UseLifebuoy()
    {
        // ✅ ignore on traveller client (fallback: host)
        if (IsLocalTravellerClient())
        {
            Debug.Log("[NAV] UI_UseLifebuoy ignored on Traveller/Host client");
            return;
        }

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        Debug.Log("[NAV] UI_UseLifebuoy -> TryUseLifebuoy()");
        ResourceManager.Instance.TryUseLifebuoy();
    }

    private bool IsLocalTravellerClient()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;

        ulong localId = nm.LocalClientId;

        // Prefer GameManager assignment if available
        var gm = GameManager.Instance;
        if (gm != null && gm.traveller != null)
        {
            var tNo = gm.traveller.GetComponent<NetworkObject>();
            if (tNo != null && tNo.IsSpawned)
                return localId == tNo.OwnerClientId;
        }

        // fallback: old behavior (host)
        return nm.IsHost;
    }

    // =====================================================================
    // Routing Helpers
    // =====================================================================

    private bool TryRouteToLocalOwnerInstance(string uiMethodName)
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                $"[NAV-ACT] {uiMethodName} routed -> static Instance (thisOwner={IsOwner}, thisSpawned={IsSpawned}, instanceOwner={Instance.IsOwner}, instanceSpawned={Instance.IsSpawned})");

            InvokeOn(Instance, uiMethodName);
            return true;
        }

        var resolved = ResolveLocalOwnerInstance();
        if (resolved != null && resolved != this)
        {
            Instance = resolved;
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                $"[NAV-ACT] {uiMethodName} resolved owner instance -> {resolved.name} (owner={resolved.IsOwner}, spawned={resolved.IsSpawned})");

            InvokeOn(resolved, uiMethodName);
            return true;
        }

        if (!IsOwner || !IsSpawned)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                $"[NAV-ACT] {uiMethodName} blocked (no owner instance ready). thisOwner={IsOwner} thisSpawned={IsSpawned} instanceNull={(Instance == null)}");

            HUDManager.Instance?.ShowMessageForNavigator("הנווט עדיין לא מוכן (Owner). נסה שוב בעוד רגע");
            return true;
        }

        return false;
    }

    private static NavigatorActions ResolveLocalOwnerInstance()
    {
        var all = Object.FindObjectsOfType<NavigatorActions>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var a = all[i];
            if (a == null) continue;
            if (a.IsOwner && a.IsSpawned)
                return a;
        }
        return null;
    }

    private static void InvokeOn(NavigatorActions target, string uiMethodName)
    {
        switch (uiMethodName)
        {
            case nameof(UI_OpenDoor):
                target.UI_OpenDoor();
                break;
            case nameof(UI_RemoveBomb):
                target.UI_RemoveBomb();
                break;
            case nameof(UI_UseLifebuoy):
                target.UI_UseLifebuoy();
                break;
            default:
                Debug.LogWarning($"[NAV-ACT] InvokeOn unknown UI method: {uiMethodName}");
                break;
        }
    }

    // =====================================================================
    // Net helpers
    // =====================================================================

    private static NetworkObject FindTravellerNetworkObject()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return null;

        // ✅ Prefer GameManager.traveller (works even if traveller != host)
        var gm = GameManager.Instance;
        if (gm != null && gm.traveller != null)
        {
            var tNo = gm.traveller.GetComponent<NetworkObject>();
            if (tNo != null && tNo.IsSpawned)
                return tNo;
        }

        // fallback: old behavior (host)
        ulong travellerClientId = NetworkManager.ServerClientId;

        if (nm.ConnectedClients != null && nm.ConnectedClients.TryGetValue(travellerClientId, out var client))
            return client.PlayerObject;

        foreach (var no in Object.FindObjectsOfType<NetworkObject>(true))
        {
            if (no != null && no.IsSpawned && no.OwnerClientId == travellerClientId)
                return no;
        }

        return null;
    }

    private static ClientRpcParams MakeTargetParams(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
        };
    }

    [ClientRpc]
    private void SendNavigatorMessageTargetClientRpc(string msg, ClientRpcParams rpcParams = default)
    {
        // Navigator is client-only UI. If you're on host client, ignore.
        if (IsHost) return;
        HUDManager.Instance?.ShowMessageForNavigator(msg);
    }
}
