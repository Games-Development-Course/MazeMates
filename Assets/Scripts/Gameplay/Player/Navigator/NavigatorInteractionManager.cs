using Unity.Netcode;
using UnityEngine;

public class NavigatorInteractionManager : NetworkBehaviour
{
    public static NavigatorInteractionManager Instance;

    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private TutorialManager tutorial =>
        FindFirstObjectByType<TutorialManager>();

    // ======================================================
    // ACTIONS
    // ======================================================

    public void Execute(NavActionType action)
    {
        switch (action)
        {
            case NavActionType.OpenDoor:
                TryOpenDoor();
                break;

            case NavActionType.ShowPuzzle:
                TryShowPuzzle();
                break;

            case NavActionType.RemoveBomb:
                RemoveBomb();
                tutorial?.NotifyNavigatorRemovedBomb();
                break;

            case NavActionType.UseLifebuoy:
                UseLifebuoy();
                tutorial?.NotifyNavigatorGaveLifebuoy();
                break;

            case NavActionType.PlaceHeart:
                PlaceHeart();
                tutorial?.NotifyNavigatorPlacedHeart();
                break;
        }
    }

    private void TryOpenDoor()
    {
        DoorController door = GameWorldController.Instance.FindDoorPlayerIsOn();

        if (door == null)
        {
            HUDManager.Instance.ShowMessageToNavigator("אין דלת כאן");
            return;
        }

        if (door.doorType == DoorType.Puzzle)
        {
            HUDManager.Instance.ShowMessageToNavigator("דלת זו דורשת לפתור חידה");
            return;
        }

        door.Interact();

        var t = FindFirstObjectByType<TutorialManager>();
        t?.NotifyNavigatorOpenedNormalDoor();
    }

    private void TryShowPuzzle()
    {
        DoorController door = GameWorldController.Instance.FindNearestDoorOnPad(DoorType.Puzzle);

        if (door == null)
        {
            HUDManager.Instance.ShowMessageToNavigator("אין דלת חידה כאן");
            return;
        }

        RequestOpenPuzzleRpc(door.NetworkObjectId);

        tutorial?.NotifyNavigatorOpenedPuzzleDoor();
    }

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
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(doorId, out NetworkObject obj))
            return;

        DoorController door = obj.GetComponent<DoorController>();
        if (door == null) return;

        if (GameManager.Instance.traveller != null &&
            GameManager.Instance.traveller.GetComponent<NetworkObject>().IsOwner)
        {
            door.GetPuzzle()?.TryOpen();
        }
    }

    public void RemoveBomb()
    {
        ResourceManager.Instance.TryRemoveBomb();
    }

    public void UseLifebuoy()
    {
        ResourceManager.Instance.TryUseLifebuoy();
    }

    public void PlaceHeart()
    {
        ResourceManager.Instance.TryPlaceHeart();
    }
}
public enum NavActionType
{
    None,

    // פעולות דלתות
    OpenDoor,
    OpenNormalDoor,
    OpenPuzzleDoor,
    ShowPuzzle,
    OpenExitDoor,

    // לב / לייף־בוי / אייטמים
    UseLifebuoy,
    GiveLifebuoy,
    PlaceHeart,

    // פצצות
    RemoveBomb
}

