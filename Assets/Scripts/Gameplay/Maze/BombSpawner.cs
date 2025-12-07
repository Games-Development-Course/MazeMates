using UnityEngine;
using Unity.Netcode;

public class BombSpawner : NetworkBehaviour
{
    public GameObject bombPrefab;

    // מיקומים קבועים שאתה הכנסת
    public Vector3[] bombPositions;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        foreach (var pos in bombPositions)
        {
            var bomb = Instantiate(bombPrefab, pos, Quaternion.identity);

            // חשוב לבצע spawn כדי שהשרת ינהל את האובייקט!
            bomb.GetComponent<NetworkObject>().Spawn();
        }
    }
}
