using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    public Transform travSpawn;
    public Transform navSpawn;

    public GameObject travellerPrefab;
    public GameObject navigatorPrefab;

    private void Awake()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        // רק השרת מבצע Spawn!
        if (!NetworkManager.Singleton.IsServer)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // HOST = Traveller
            SpawnTraveller(clientId);
        }
        else
        {
            // Client = Navigator
            SpawnNavigator(clientId);
        }
    }

    private void SpawnTraveller(ulong clientId)
    {
        var obj = Instantiate(travellerPrefab, travSpawn.position, travSpawn.rotation);
        obj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    private void SpawnNavigator(ulong clientId)
    {
        var obj = Instantiate(navigatorPrefab, navSpawn.position, navSpawn.rotation);
        obj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}
