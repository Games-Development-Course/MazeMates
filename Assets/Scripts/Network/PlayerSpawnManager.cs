using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class PlayerSpawnManager : MonoBehaviour
{
    public Transform travSpawn;
    public Transform navSpawn;

    public GameObject travellerPrefab;
    public GameObject navigatorPrefab;

    private bool navigatorSpawned = false;

    private void Awake()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        // Host = Traveller
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SpawnTraveller(clientId);
        }
        else
        {
            SpawnNavigator(clientId);
            OnNavigatorSpawned(); // נקרא כשהנווט הגיע
        }
    }

    private void SpawnTraveller(ulong clientId)
    {
        var obj = Instantiate(travellerPrefab, travSpawn.position, travSpawn.rotation);
        obj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        GameManager.Instance.traveller = obj;

        var move = obj.GetComponent<PlayerMovement1P>();
        if (move != null) move.enabled = false;

        HUDManager.Instance?.Traveller?.ShowMessage("ממתין להתחברות הנווט…");

        Debug.Log("Traveller spawned & locked");
    }

    private void SpawnNavigator(ulong clientId)
    {
        Debug.Log("NAV: navSpawn.position = " + navSpawn.position);

        var obj = Instantiate(navigatorPrefab, navSpawn.position, navSpawn.rotation);
        Debug.Log("NAV: after Instantiate, obj.position = " + obj.transform.position);

        obj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        navigatorSpawned = true;

        Debug.Log("Navigator spawned");
    }

    private void OnNavigatorSpawned()
    {
        if (!navigatorSpawned || GameManager.Instance.traveller == null)
            return;

        Debug.Log("Both players present — unlocking traveller & starting tutorial");

        StartCoroutine(DelayedTutorialStart());
    }

    private IEnumerator DelayedTutorialStart()
    {
        // זמן המתנה לסנכרון כל האובייקטים ב־Client
        yield return new WaitForSeconds(0.25f);

        // מנקה הודעת "ממתין"
        HUDManager.Instance?.Traveller?.Clear();

        // משחרר תנועה
        var move = GameManager.Instance.traveller.GetComponent<PlayerMovement1P>();
        if (move != null) move.enabled = true;

        // מתחיל טוטוריאל
        var tutorial = FindFirstObjectByType<TutorialManager>();
        if (tutorial != null)
            tutorial.StartTutorial();
    }
}
