using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class GameWorldController : NetworkBehaviour
{
    public static GameWorldController Instance;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        Debug.Log("GameWorldController READY on " + (IsServer ? "SERVER" : "CLIENT"));
    }

    // ============================================================
    // FIND DOOR BY SPECIFIC TYPE
    // ============================================================

    public DoorController FindNearestDoorOnPad(DoorType type)
    {
        var allDoors = FindObjectsByType<DoorController>(FindObjectsSortMode.None);

        foreach (var d in allDoors)
        {
            if (d.doorType != type) continue;
            if (d.TravellerIsOnPad()) return d;
        }

        return null;
    }

    // ============================================================
    // FIND ANY DOOR THE TRAVELLER IS CURRENTLY ON
    // ============================================================

    public DoorController FindDoorPlayerIsOn()
    {
        var allDoors = FindObjectsByType<DoorController>(FindObjectsSortMode.None);

        foreach (var d in allDoors)
        {
            if (d.TravellerIsOnPad())
                return d;
        }

        return null;
    }
}
