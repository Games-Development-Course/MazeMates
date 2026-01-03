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

    /// <summary>
    /// כפתור יחיד: אם המטייל עומד על דלת רגילה -> פותח
    /// אם על דלת חידה -> פותח פאזל
    /// אם על דלת יציאה -> מנסה ניצחון (רק אם כל המפתחות נאספו)
    /// </summary>
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

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[NAV-ACT] Sending UseDoorOnTravellerPadServerRpc()");
        UseDoorOnTravellerPadServerRpc();
    }

    // תאימות לאחור: אם עדיין יש כפתורים ישנים בסצנה/Prefab – שלא יישבר
    public void UI_ShowPuzzle() => UI_OpenDoor();

    // אם יש לך כפתור “VictoryDoor”/דומה שקורא למתודה אחרת – תשאיר/תכוון אותו לזה
    public void UI_VictoryDoor() => UI_OpenDoor();

    private bool IsLocalNavigator()
    {
        // אצלך: Host = Traveller, Client = Navigator
        return !IsHost;
    }

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

        // Find door by authoritative pad triggers (server-side)
        DoorController door = FindDoorTravellerIsStandingOnServer(null);

        Debug.Log($"[NAV-ACT][ServerRpc] FindDoorTravellerIsStandingOnServer(ANY) -> {(door == null ? "NULL" : door.name)}");

        if (door == null)
        {
            SendNavigatorMessageTargetClientRpc("אין דלת כאן", MakeTargetParams(sender));
            return;
        }

        Debug.Log($"[NAV-ACT][ServerRpc] Door type={door.doorType} open={door.IsOpen()} name={door.name}");

        // החלטה לפי סוג הדלת
        if (door.doorType == DoorType.Puzzle)
        {
            Debug.Log($"[NAV-ACT][ServerRpc] Puzzle door -> RequestOpenPuzzleDoorRpc() for {door.name}");
            door.RequestOpenPuzzleDoorRpc();
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

        // Normal (וגם כל סוג אחר שאינו Puzzle/Exit)
        Debug.Log($"[NAV-ACT][ServerRpc] Normal door -> Interact() for {door.name}");
        door.Interact();
        tutorial?.NotifyNavigatorOpenedNormalDoor();
    }

    /// <summary>
    /// SERVER ONLY:
    /// Find which door the traveller is standing on by scanning PadTrigger components.
    /// PadTrigger.playerOnPad is local but on the server it is authoritative for traveller collisions.
    /// </summary>
    private static DoorController FindDoorTravellerIsStandingOnServer(DoorType? filter)
    {
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
