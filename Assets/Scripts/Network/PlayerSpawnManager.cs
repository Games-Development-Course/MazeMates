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
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            Debug.Log($"[SPAWN] PlayerSpawnManager Awake. IsServer={NetworkManager.Singleton.IsServer}, LocalClientId={NetworkManager.Singleton.LocalClientId}");
        }
        else
        {
            Debug.LogWarning("[SPAWN] PlayerSpawnManager Awake but NetworkManager.Singleton is NULL");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[SPAWN] OnClientConnected called but NetworkManager.Singleton is NULL");
            return;
        }

        Debug.Log($"[SPAWN] OnClientConnected: clientId={clientId}, localClientId={NetworkManager.Singleton.LocalClientId}, IsServer={NetworkManager.Singleton.IsServer}");

        if (!NetworkManager.Singleton.IsServer)
            return;

        // השחקן הראשון (Host) = מטייל
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("[SPAWN] Treating this client as TRAVELLER (Host)");
            SpawnTraveller(clientId);
        }
        else
        {
            // שחקן נוסף = נווט
            Debug.Log("[SPAWN] Treating this client as NAVIGATOR (remote client)");
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
        if (gm == null)
        {
            Debug.LogError("[SPAWN] GameManager.Instance is NULL in SpawnTraveller");
            return;
        }

        if (travSpawn == null)
        {
            Debug.LogError("[SPAWN] travSpawn is NULL! לא מוגדר Spawn Point למטייל");
            return;
        }

        Vector3 pos = travSpawn.position;
        Quaternion rot = travSpawn.rotation;

        Debug.Log($"[SPAWN] Spawning Traveller for client {clientId} at pos={pos}, rot={rot.eulerAngles}");

        var obj = Instantiate(travellerPrefab, pos, rot);
        var netObj = obj.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[SPAWN] Traveller prefab is missing NetworkObject");
            return;
        }

        netObj.SpawnAsPlayerObject(clientId);
        Debug.Log($"[SPAWN] Traveller NetworkObject spawned. OwnerClientId={netObj.OwnerClientId}");

        EnableAllNetworkBehaviours(obj);

        gm.traveller = obj;
        gm.travellerMove = obj.GetComponent<PlayerMovement1P>();
        gm.travellerCam = obj.GetComponentInChildren<PlayerCamera1P>();

        if (gm.travellerMove == null) Debug.LogWarning("[SPAWN] TravellerMove is NULL");
        if (gm.travellerCam == null) Debug.LogWarning("[SPAWN] TravellerCam is NULL");

        Freeze(obj);

        HUDManager.Instance?.Traveller?.ShowMessage("ממתין להתחברות הנווט…");

        Debug.Log($"[SPAWN] Traveller fully initialized for client {clientId}");
    }

    // ==========================================
    // NAVIGATOR
    // ==========================================

    private void SpawnNavigator(ulong clientId)
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[SPAWN] GameManager.Instance is NULL in SpawnNavigator");
            return;
        }

        if (navSpawn == null)
        {
            Debug.LogError("[SPAWN] navSpawn is NULL! לא מוגדר Spawn Point לנווט");
            return;
        }

        Vector3 pos = navSpawn.position;
        Quaternion rot = navSpawn.rotation;

        Debug.Log($"[SPAWN] Spawning Navigator for client {clientId} at pos={pos}, rot={rot.eulerAngles}");

        var obj = Instantiate(navigatorPrefab, pos, rot);
        var netObj = obj.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[SPAWN] Navigator prefab is missing NetworkObject");
            return;
        }

        netObj.SpawnAsPlayerObject(clientId);
        Debug.Log($"[SPAWN] Navigator NetworkObject spawned. OwnerClientId={netObj.OwnerClientId}");

        EnableAllNetworkBehaviours(obj);

        gm.navigator = obj;
        gm.navigatorMove = obj.GetComponent<PlayerMovement1P>();
        gm.navigatorCam = obj.GetComponentInChildren<PlayerCamera1P>();

        if (gm.navigatorMove == null) Debug.LogWarning("[SPAWN] NavigatorMove is NULL");
        if (gm.navigatorCam == null) Debug.LogWarning("[SPAWN] NavigatorCam is NULL");

        Freeze(obj);

        navigatorSpawned = true;

        Debug.Log($"[SPAWN] Navigator fully initialized for client {clientId}");
    }

    // ==========================================
    // ENABLE NETWORK BEHAVIOURS
    // ==========================================

    private void EnableAllNetworkBehaviours(GameObject obj)
    {
        var all = obj.GetComponentsInChildren<NetworkBehaviour>(true);
        Debug.Log($"[SPAWN] EnableAllNetworkBehaviours on '{obj.name}'  count={all.Length}");

        foreach (var nb in all)
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
        if (move != null)
        {
            move.SetFrozen(true);
            Debug.Log($"[SPAWN] Freeze movement on '{obj.name}'");
        }

        var cam = obj.GetComponentInChildren<PlayerCamera1P>();
        if (cam != null)
        {
            cam.SetCameraFrozen(true);
            Debug.Log($"[SPAWN] Freeze camera on '{obj.name}'");
        }
    }

    private void Unfreeze(GameObject obj)
    {
        var move = obj.GetComponent<PlayerMovement1P>();
        if (move != null)
        {
            move.SetFrozen(false);
            Debug.Log($"[SPAWN] Unfreeze movement on '{obj.name}'");
        }

        var cam = obj.GetComponentInChildren<PlayerCamera1P>();
        if (cam != null)
        {
            cam.SetCameraFrozen(false);
            Debug.Log($"[SPAWN] Unfreeze camera on '{obj.name}'");
        }
    }

    // ==========================================
    // BOTH CONNECTED → START TUTORIAL
    // ==========================================

    private void OnNavigatorSpawned()
    {
        var gm = GameManager.Instance;

        if (!navigatorSpawned || gm == null || gm.traveller == null)
        {
            Debug.Log($"[SPAWN] OnNavigatorSpawned blocked. navigatorSpawned={navigatorSpawned}, gmNull={gm == null}, travellerNull={gm == null || gm.traveller == null}");
            return;
        }

        Debug.Log("[SPAWN] Both players connected → starting tutorial (delayed)");
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
        {
            Debug.Log("[SPAWN] TutorialManager found → StartTutorial()");
            t.StartTutorial();
        }
        else
        {
            Debug.LogWarning("[SPAWN] TutorialManager NOT FOUND in StartTutorialDelayed");
        }
    }
}
