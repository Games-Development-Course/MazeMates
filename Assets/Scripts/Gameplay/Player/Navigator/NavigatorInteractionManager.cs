using Unity.Netcode;
using UnityEngine;

public enum NavActionType
{
    OpenDoor,
    ShowPuzzle,
    RemoveBomb,
    UseLifebuoy,
    PlaceHeart
}

public class NavigatorInteractionManager : NetworkBehaviour
{
    public static NavigatorInteractionManager Instance;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        Debug.Log("NavigatorInteractionManager READY");
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

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
                break;
            case NavActionType.UseLifebuoy:
                UseLifebuoy();
                break;
            case NavActionType.PlaceHeart:
                PlaceHeart();
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
    }

    private void TryShowPuzzle()
    {
        DoorController door = GameWorldController.Instance.FindNearestDoorOnPad(DoorType.Puzzle);

        if (door == null)
        {
            HUDManager.Instance.ShowMessageToNavigator("אין דלת חידה כאן");
            return;
        }

        HUDManager.Instance.Navigator.ShowPuzzleImage(door.navigatorPreview);

        RequestOpenPuzzleRpc(door.NetworkObjectId);
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
        if (door == null)
            return;

        if (GameManager.Instance.traveller != null &&
            GameManager.Instance.traveller.GetComponent<NetworkObject>().IsOwner)
        {
            door.GetPuzzle()?.TryOpen();
        }
    }

    public void RemoveBomb()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("ResourceManager.Instance is NULL");
            return;
        }

        ResourceManager.Instance.TryRemoveBomb();
    }

    public void UseLifebuoy()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("ResourceManager.Instance is NULL");
            return;
        }

        ResourceManager.Instance.TryUseLifebuoy();
    }

    public void PlaceHeart()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("ResourceManager.Instance is NULL");
            return;
        }

        ResourceManager.Instance.TryPlaceHeart();
    }
}
