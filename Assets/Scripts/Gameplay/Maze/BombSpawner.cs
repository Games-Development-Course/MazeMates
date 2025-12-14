using Unity.Netcode;
using UnityEngine;

public class BombSpawner : NetworkBehaviour
{
    public GameObject bombPrefab;

    // ������� ������ ���� �����
    public Vector3[] bombPositions;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        foreach (var pos in bombPositions)
        {
            var bomb = Instantiate(bombPrefab, pos, Quaternion.identity);

            // ���� ���� spawn ��� ����� ���� �� ��������!
            bomb.GetComponent<NetworkObject>().Spawn();
        }
    }
}
