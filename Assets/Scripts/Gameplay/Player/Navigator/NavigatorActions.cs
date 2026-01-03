// ======================= File: Assets/Scripts/Gameplay/Player/Navigator/NavigatorActions.cs =======================
// FIX: OpenDoor / ShowPuzzle should work reliably in multiplayer.
//
// Why it failed (from your logs):
// 1) UI button sometimes invoked on a NavigatorActions instance that is NOT owner (IsOwner=False).
// 2) Client-side "find door player is on" can return NULL because pad/trigger state isn't synced to client.
//
// Solution (NO DoorController API changes):
// - Always route UI calls to the LOCAL owner's NavigatorActions.Instance.
// - Send ServerRpc.
// - On SERVER: find the Traveller's current pad by scanning PadTrigger components (server has authoritative triggers).
//   We pick the DoorController whose PadTrigger.IsPlayerOnPad() == true.
// - Then open/interact on server.
//
// NOTE: This does NOT require any overload of DoorController.FindDoorPlayerIsOn.
// ================================================================================================================

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
        if (IsOwner)
            Instance = this;

        tutorial = Object.FindFirstObjectByType<TutorialManager>();

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NavigatorActions][OnNetworkSpawn] IsOwner={IsOwner} IsHost={IsHost} IsServer={IsServer} LocalClientId={(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 9999)}"
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
        // Route to local owner's instance (fixes IsOwner=False button wiring)
        if (Instance != null && Instance != this)
        {
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                $"[NAV-ACT] UI_OpenDoor routed -> local Instance (thisOwner={IsOwner}, instanceOwner={Instance.IsOwner})"
            );
            Instance.UI_OpenDoor();
            return;
        }

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NAV-ACT] UI_OpenDoor pressed | IsHost={IsHost} IsOwner={IsOwner}"
        );

        if (!IsLocalNavigator())
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[NAV-ACT] UI_OpenDoor aborted: not local navigator");
            return;
        }

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[NAV-ACT] Sending OpenDoorOnTravellerPadServerRpc()");
        OpenDoorOnTravellerPadServerRpc();
    }

    public void UI_ShowPuzzle()
    {
        // Route to local owner's instance (fixes IsOwner=False button wiring)
        if (Instance != null && Instance != this)
        {
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                $"[NAV-ACT] UI_ShowPuzzle routed -> local Instance (thisOwner={IsOwner}, instanceOwner={Instance.IsOwner})"
            );
            Instance.UI_ShowPuzzle();
            return;
        }

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NAV-ACT] UI_ShowPuzzle pressed | IsHost={IsHost} IsOwner={IsOwner}"
        );

        if (!IsLocalNavigator())
            return;

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[NAV-ACT] Sending OpenPuzzleOnTravellerPadServerRpc()");
        OpenPuzzleOnTravellerPadServerRpc();
    }

    private bool IsLocalNavigator()
    {
        return !IsHost;
    }

    // =====================================================================
    // SERVER AUTHORITATIVE DOOR ACTIONS
    // =====================================================================

    [ServerRpc(RequireOwnership = false)]
    private void OpenDoorOnTravellerPadServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        Debug.Log($"[NAV-ACT][ServerRpc] OpenDoorOnTravellerPadServerRpc from clientId={sender}");

        var travellerObj = FindTravellerNetworkObject();
        if (travellerObj == null)
        {
            Debug.LogWarning("[NAV-ACT][ServerRpc] Traveller NetworkObject not found.");
            SendNavigatorMessageTargetClientRpc("לא נמצא מטייל בסצנה", MakeTargetParams(sender));
            return;
        }

        // Find door by authoritative pad triggers (server-side)
        DoorController door = FindDoorTravellerIsStandingOnServer(null);

        Debug.Log($"[NAV-ACT][ServerRpc] FindDoorTravellerIsStandingOnServer(NORMAL/ANY) -> {(door == null ? "NULL" : door.name)}");

        if (door == null)
        {
            SendNavigatorMessageTargetClientRpc("אין דלת כאן", MakeTargetParams(sender));
            return;
        }

        Debug.Log($"[NAV-ACT][ServerRpc] Door type={door.doorType} open={door.IsOpen()} name={door.name}");

        if (door.doorType == DoorType.Puzzle)
        {
            SendNavigatorMessageTargetClientRpc("דלת זו דורשת לפתור חידה", MakeTargetParams(sender));
            return;
        }

        Debug.Log($"[NAV-ACT][ServerRpc] Calling door.Interact() on server for {door.name}");
        door.Interact();

        // Optional: if your tutorial step expects "opened normal door"
        tutorial?.NotifyNavigatorOpenedNormalDoor();
    }

    [ServerRpc(RequireOwnership = false)]
    private void OpenPuzzleOnTravellerPadServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        Debug.Log($"[NAV-ACT][ServerRpc] OpenPuzzleOnTravellerPadServerRpc from clientId={sender}");

        var travellerObj = FindTravellerNetworkObject();
        if (travellerObj == null)
        {
            Debug.LogWarning("[NAV-ACT][ServerRpc] Traveller NetworkObject not found.");
            SendNavigatorMessageTargetClientRpc("לא נמצא מטייל בסצנה", MakeTargetParams(sender));
            return;
        }

        DoorController door = FindDoorTravellerIsStandingOnServer(DoorType.Puzzle);

        Debug.Log($"[NAV-ACT][ServerRpc] FindDoorTravellerIsStandingOnServer(PUZZLE) -> {(door == null ? "NULL" : door.name)}");

        if (door == null)
        {
            SendNavigatorMessageTargetClientRpc("אין דלת חידה כאן", MakeTargetParams(sender));
            return;
        }

        Debug.Log($"[NAV-ACT][ServerRpc] Calling door.RequestOpenPuzzleDoorRpc() on server for {door.name}");
        door.RequestOpenPuzzleDoorRpc();

        tutorial?.NotifyNavigatorOpenedPuzzleDoor();
    }

    /// <summary>
    /// SERVER ONLY:
    /// Find which door the traveller is standing on by scanning PadTrigger components.
    /// PadTrigger.playerOnPad is local but on the server it is authoritative for traveller collisions.
    /// </summary>
    private static DoorController FindDoorTravellerIsStandingOnServer(DoorType? filter)
    {
        // We intentionally search PadTrigger (not client-side helper methods)
        var pads = Object.FindObjectsOfType<PadTrigger>(true);

        DoorController best = null;

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

            // If multiple pads are "true" (edge case), pick first; add smarter selection if needed.
            best = door;
            break;
        }

        return best;
    }

    // =====================================================================
    // Existing features (kept)
    // =====================================================================

    public void UI_RemoveBomb()
    {
        // Route to local owner's instance
        if (Instance != null && Instance != this)
        {
            Instance.UI_RemoveBomb();
            return;
        }

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[NAV] RemoveBomb pressed | Owner={IsOwner} Server={IsServer} Host={IsHost}"
        );

        if (!IsLocalNavigator())
            return;

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        ResourceManager.Instance.TryRemoveBomb();
    }

    public void UI_UseLifebuoy()
    {
        // Route to local owner's instance
        if (Instance != null && Instance != this)
        {
            Instance.UI_UseLifebuoy();
            return;
        }

        if (!IsLocalNavigator())
            return;

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        ResourceManager.Instance.TryUseLifebuoy();
    }

    public void UI_PlaceHeart()
    {
        // Route to local owner's instance
        if (Instance != null && Instance != this)
        {
            Instance.UI_PlaceHeart();
            return;
        }

        if (!IsLocalNavigator())
            return;

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        ResourceManager.Instance.TryPlaceHeart();
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static NetworkObject FindTravellerNetworkObject()
    {
        // Traveller is always ServerClientId (host)
        ulong travellerClientId = NetworkManager.ServerClientId;
        var nm = NetworkManager.Singleton;
        if (nm == null) return null;

        if (nm.ConnectedClients != null && nm.ConnectedClients.TryGetValue(travellerClientId, out var client))
        {
            return client.PlayerObject;
        }

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
        if (IsHost) return;
        HUDManager.Instance?.ShowMessageForNavigator(msg);
    }
}
