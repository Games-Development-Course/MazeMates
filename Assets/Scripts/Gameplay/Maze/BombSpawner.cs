using Fusion;
using UnityEngine;

public class BombSpawner : NetworkBehaviour
{
    public NetworkPrefabRef bombPrefab;

    public Vector3[] bombPositions;

    public override void Spawned()
    {
        // רק StateAuthority (בד"כ השרת) יוצר את הפצצות
        if (!Object.HasStateAuthority)
            return;

        foreach (var pos in bombPositions)
        {
            Runner.Spawn(bombPrefab, pos, Quaternion.identity);
        }
    }
}
