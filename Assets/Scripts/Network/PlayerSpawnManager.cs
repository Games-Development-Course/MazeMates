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
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }


    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        // שחקן ראשון הוא המטייל
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SpawnTraveller(clientId);
        }
        else
        {
            SpawnNavigator(clientId);
        }
    }

    private void SpawnTraveller(ulong clientId)
    {
        var obj = Instantiate(travellerPrefab, travSpawn.position, travSpawn.rotation);
        obj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        // כאן התיקון הקריטי:
        GameManager.Instance.traveller = obj;

        Debug.Log("Traveller spawned & linked to GameManager");
    }

    private void SpawnNavigator(ulong clientId)
    {
        var obj = Instantiate(navigatorPrefab, navSpawn.position, navSpawn.rotation);
        obj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}
