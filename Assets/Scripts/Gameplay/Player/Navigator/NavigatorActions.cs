// NavigatorActions.cs
using Unity.Netcode;
using UnityEngine;

public class NavigatorActions : NetworkBehaviour
{
    public static NavigatorActions Instance { get; private set; }

    private TutorialManager tutorial;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            Instance = this;

        // נחסוך Find בכל קריאה
        tutorial = Object.FindFirstObjectByType<TutorialManager>();
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

  
    // ======================================================
    // UI – מחובר ישירות לכפתורים ב-Inspector
    // ======================================================

    public void UI_OpenDoor()
    {
        if (!IsLocalNavigator()) return;

        DoorController door = DoorController.FindDoorPlayerIsOn();

        if (door == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("אין דלת כאן");
            return;
        }

        if (door.doorType == DoorType.Puzzle)
        {
            HUDManager.Instance?.ShowMessageForNavigator("דלת זו דורשת לפתור חידה");
            return;
        }

        // ⭐ זה האירוע שהטוטוריאל מחכה לו
        if (door.doorType == DoorType.Normal)
            tutorial?.NotifyNavigatorOpenedNormalDoor();

        if (door.doorType == DoorType.Exit)
            tutorial?.NotifyNavigatorOpenedExitDoor();

        door.Interact();
    }

    public void UI_ShowPuzzle()
    {
        if (!IsLocalNavigator()) return;

        DoorController door = DoorController.FindNearestDoorOnPad(DoorType.Puzzle, transform.position);

        if (door == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("אין דלת חידה כאן");
            return;
        }

        RequestOpenPuzzleRpc(door.NetworkObjectId);

        // ⭐ הטוטוריאל צריך לדעת שהנווט פתח דלת חידה
        tutorial?.NotifyNavigatorOpenedPuzzleDoor();
    }
    // NavigatorActions.cs

    private bool IsLocalNavigator()
    {
        // במשחק הנוכחי: Host = Traveller, Client = Navigator
        return !IsHost;
    }

    public void UI_RemoveBomb()
    {
        Debug.Log("[NAV] RemoveBomb pressed. IsOwner=" + IsOwner +
                  " IsServer=" + IsServer + " IsHost=" + IsHost);

        if (!IsLocalNavigator()) return;

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        ResourceManager.Instance.TryRemoveBomb();
    }


    public void UI_UseLifebuoy()
    {
        if (!IsLocalNavigator()) return;

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        ResourceManager.Instance.TryUseLifebuoy();
        // ResourceManager כבר קורא NotifyNavigatorGaveLifebuoy מה-RPC
    }

    public void UI_PlaceHeart()
    {
        if (!IsLocalNavigator()) return;

        if (ResourceManager.Instance == null)
        {
            HUDManager.Instance?.ShowMessageForNavigator("מערכת משאבים לא מוכנה");
            return;
        }

        ResourceManager.Instance.TryPlaceHeart();
        // ResourceManager כבר קורא NotifyNavigatorPlacedHeart מה-RPC
    }

    // ======================================================
    // פתיחת פאזל – Rpc
    // ======================================================

    [Rpc(SendTo.Server)]
    private void RequestOpenPuzzleRpc(ulong doorId)
    {
        Server_OpenPuzzleRpc(doorId);
    }

    [Rpc(SendTo.Everyone)]
    private void Server_OpenPuzzleRpc(ulong doorId)
    {
        OpenPuzzleForTravellerRpc(doorId);
    }

    [Rpc(SendTo.Everyone)]
    private void OpenPuzzleForTravellerRpc(ulong doorId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(doorId, out NetworkObject obj))
            return;

        DoorController door = obj.GetComponent<DoorController>();
        if (door == null) return;

        var gm = GameManager.Instance;
        if (gm == null || gm.traveller == null) return;

        var travellerNet = gm.traveller.GetComponent<NetworkObject>();
        if (travellerNet != null && travellerNet.IsOwner)
        {
            door.GetPuzzle()?.TryOpen();
        }
    }
}
