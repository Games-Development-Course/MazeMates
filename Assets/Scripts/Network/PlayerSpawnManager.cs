using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class PlayerSpawnManager : MonoBehaviour
{
    [Header("Fallback spawn points")]
    public Transform travSpawn;
    public Transform navSpawn;

    [Header("Prefabs")]
    public GameObject travellerPrefab;
    public GameObject navigatorPrefab;

    private bool navigatorSpawned = false;

    private void Awake()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        // השחקן הראשון (Host) = מטייל
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SpawnTraveller(clientId);
        }
        else
        {
            // שחקן נוסף = נווט
            SpawnNavigator(clientId);
            OnNavigatorSpawned();
        }
    }

    // ==========================================
    // TRAVELLER
    // ==========================================

    private void SpawnTraveller(ulong clientId)
    {
        var gm = GameManager.Instance;

        Vector3 pos = travSpawn.position;
        Quaternion rot = travSpawn.rotation;

        var obj = Instantiate(travellerPrefab, pos, rot);
        var netObj = obj.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);

        EnableAllNetworkBehaviours(obj);

        gm.traveller = obj;
        gm.travellerMove = obj.GetComponent<PlayerMovement1P>();
        gm.travellerCam = obj.GetComponentInChildren<PlayerCamera1P>();

        Freeze(obj);

        HUDManager.Instance?.Traveller?.ShowMessage("ממתין להתחברות הנווט…");

        Debug.Log($"[Spawn] Traveller at {pos}");
    }

    // ==========================================
    // NAVIGATOR
    // ==========================================

    private void SpawnNavigator(ulong clientId)
    {
        var gm = GameManager.Instance;

        Vector3 pos = navSpawn.position;
        Quaternion rot = navSpawn.rotation;

        var obj = Instantiate(navigatorPrefab, pos, rot);
        var netObj = obj.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);

        EnableAllNetworkBehaviours(obj);

        gm.navigator = obj;
        gm.navigatorMove = obj.GetComponent<PlayerMovement1P>();
        gm.navigatorCam = obj.GetComponentInChildren<PlayerCamera1P>();

        Freeze(obj);

        navigatorSpawned = true;

        Debug.Log($"[Spawn] Navigator at {pos}");
    }

    // ==========================================
    // ENABLE NETWORK BEHAVIOURS (FIX)
    // ==========================================

    private void EnableAllNetworkBehaviours(GameObject obj)
    {
        foreach (var nb in obj.GetComponentsInChildren<NetworkBehaviour>(true))
        {
            nb.enabled = true;
        }
    }

    // ==========================================
    // FREEZE / UNFREEZE
    // ==========================================

    private void Freeze(GameObject obj)
    {
        var move = obj.GetComponent<PlayerMovement1P>();
        if (move != null) move.SetFrozen(true);

        var cam = obj.GetComponentInChildren<PlayerCamera1P>();
        if (cam != null) cam.SetCameraFrozen(true);
    }

    private void Unfreeze(GameObject obj)
    {
        var move = obj.GetComponent<PlayerMovement1P>();
        if (move != null) move.SetFrozen(false);

        var cam = obj.GetComponentInChildren<PlayerCamera1P>();
        if (cam != null) cam.SetCameraFrozen(false);
    }

    // ==========================================
    // BOTH CONNECTED → START TUTORIAL
    // ==========================================

    private void OnNavigatorSpawned()
    {
        var gm = GameManager.Instance;

        if (!navigatorSpawned || gm.traveller == null)
            return;

        StartCoroutine(StartTutorialDelayed());
    }

    private IEnumerator StartTutorialDelayed()
    {
        yield return new WaitForSeconds(0.2f);

        if (HUDManager.Instance != null && HUDManager.Instance.Traveller != null)
        {
            HUDManager.Instance.Traveller.Clear();
        }

        var t = FindFirstObjectByType<TutorialManager>();
        if (t != null)
            t.StartTutorial();
    }
}
    