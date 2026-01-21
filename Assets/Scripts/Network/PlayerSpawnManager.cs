// ===============================
// File: Assets/Scripts/Net/PlayerSpawnManager.cs
// Attach this ONCE to the NetworkManager in StartScene.
// ===============================

using System.Collections;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerSpawnManager : MonoBehaviour
{
    [Header("Prefabs (must be registered in NetworkManager > NetworkPrefabs)")]
    [SerializeField] private GameObject travellerPrefab;
    [SerializeField] private GameObject navigatorPrefab;

    [Header("Gameplay Scenes (spawn allowed ONLY here)")]
    [SerializeField]
    private string[] gameplayScenes =
    {
        "TutorialScene",
        "GameScene"
    };

    [Header("Scene Names")]
    [SerializeField] private string tutorialSceneName = "TutorialScene";
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Fallback Spawn (safety only)")]
    [SerializeField] private Vector3 fallbackTravellerPos = new Vector3(1f, 0f, 1f);
    [SerializeField] private float fallbackYRotation = 0f;

    [Header("Options")]
    [SerializeField] private bool destroyWithScene = false;

    private static PlayerSpawnManager instance;

    private Transform travellerSpawn;
    private Transform navigatorSpawn;

    private string currentSceneName;
    private bool sceneSpawnsReady;

    private int sceneLoadToken;
    private Coroutine sceneInitRoutine;

    // -------------------------------------------------

    private void Awake()
    {
        var root = transform.root.gameObject;

        if (instance != null && instance != this)
        {
            Destroy(root);
            return;
        }

        instance = this;
        DontDestroyOnLoad(root);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnUnitySceneLoaded;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnServerStarted += OnServerStarted;
        nm.OnClientConnectedCallback += OnClientConnectedServerOnly;

        if (nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted += OnNetcodeLoadEventCompleted;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnServerStarted -= OnServerStarted;
        nm.OnClientConnectedCallback -= OnClientConnectedServerOnly;

        if (nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted -= OnNetcodeLoadEventCompleted;
    }

    // -------------------------------------------------
    // Scene / Network events
    // -------------------------------------------------

    private void OnServerStarted()
    {
        var scene = SceneManager.GetActiveScene().name;
        BeginSceneInit(scene);
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BeginSceneInit(scene.name);
    }

    private void OnNetcodeLoadEventCompleted(
        string sceneName,
        LoadSceneMode mode,
        System.Collections.Generic.List<ulong> completedClients,
        System.Collections.Generic.List<ulong> timedOutClients)
    {
        BeginSceneInit(sceneName);
    }

    private void OnClientConnectedServerOnly(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var sceneName = SceneManager.GetActiveScene().name;
        if (!IsGameplayScene(sceneName)) return;

        if (sceneSpawnsReady)
            EnsureOnlyThisClient(clientId);
    }

    // -------------------------------------------------
    // Core flow
    // -------------------------------------------------

    private bool IsGameplayScene(string sceneName)
    {
        return gameplayScenes != null && gameplayScenes.Contains(sceneName);
    }

    private void BeginSceneInit(string sceneName)
    {
        currentSceneName = sceneName;

        sceneSpawnsReady = false;

        // ✅ IMPORTANT: do nothing in non-gameplay scenes (e.g., StartScene)
        if (!IsGameplayScene(sceneName))
            return;

        sceneLoadToken++;

        if (sceneInitRoutine != null)
        {
            StopCoroutine(sceneInitRoutine);
            sceneInitRoutine = null;
        }

        sceneInitRoutine = StartCoroutine(SceneInit(sceneName, sceneLoadToken));
    }

    private IEnumerator SceneInit(string sceneName, int token)
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        // 1) First resolve
        ResolvePlayerStartPoints(sceneName);

        // 2) GameScene: wait for maze to finish aligning (world may move)
        if (sceneName == gameSceneName)
            yield return WaitForMazeReady(token);

        // 3) Re-resolve AFTER maze is ready/aligned (critical for Navigator)
        ResolvePlayerStartPoints(sceneName);

        // 4) Now wait until navigator spawn exists (late load safety)
        yield return WaitForNavigatorSpawnPoint(sceneName, token);

        if (sceneName == tutorialSceneName)
            TryResolveFromTutorialManager();

        if (token != sceneLoadToken) yield break;

        sceneSpawnsReady = true;
        SpawnOrMoveAllPlayers();

    }

    // -------------------------------------------------
    // Spawn resolution
    // -------------------------------------------------

    private void ResolvePlayerStartPoints(string sceneName)
    {
        travellerSpawn = null;
        navigatorSpawn = null;

        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = SceneManager.GetActiveScene();

        var points = Object.FindObjectsByType<PlayerStartPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (var p in points)
        {
            if (p.gameObject.scene != scene) continue;

            if (p.role == PlayerStartPoint.Role.Traveller)
                travellerSpawn = p.transform;
            else if (p.role == PlayerStartPoint.Role.Navigator)
                navigatorSpawn = p.transform;
        }
    }

    private IEnumerator WaitForMazeReady(int token)
    {
        const int maxFrames = 300;

        for (int i = 0; i < maxFrames; i++)
        {
            if (token != sceneLoadToken) yield break;

            var mg = Object.FindFirstObjectByType<MazeGenerator3D>(FindObjectsInactive.Include);
            if (mg != null && mg.IsReady && mg.TravellerSpawn != null)
            {
                travellerSpawn = mg.TravellerSpawn;
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning("[SPAWN] MazeGenerator not ready in time – using fallback.");
    }

    private void TryResolveFromTutorialManager()
    {
        if (travellerSpawn == null &&
            TryGetTutorialManagerSpawn(true, out var tPos, out var tRot))
            travellerSpawn = CreateTempAnchor("TMP_TravellerSpawn", tPos, tRot);

        if (navigatorSpawn == null &&
            TryGetTutorialManagerSpawn(false, out var nPos, out var nRot))
            navigatorSpawn = CreateTempAnchor("TMP_NavigatorSpawn", nPos, nRot);
    }

    private static Transform CreateTempAnchor(string name, Vector3 pos, Quaternion rot)
    {
        var go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        go.transform.SetPositionAndRotation(pos, rot);
        return go.transform;
    }

    // -------------------------------------------------
    // Spawn / Teleport
    // -------------------------------------------------
    private void RegisterPlayersInGameManager(ulong travellerId, ulong? navigatorId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        if (nm.ConnectedClients.TryGetValue(travellerId, out var tClient) && tClient.PlayerObject != null)
            gm.traveller = tClient.PlayerObject.gameObject;

        if (navigatorId.HasValue &&
            nm.ConnectedClients.TryGetValue(navigatorId.Value, out var nClient) &&
            nClient.PlayerObject != null)
        {
            gm.navigator = nClient.PlayerObject.gameObject;
        }

        // דיבאג שמוכיח שהשרת באמת רואה מיקום נכון (לא (0,0,0))
        var tPos = gm.traveller ? gm.traveller.transform.position : Vector3.zero;
        var nPos = gm.navigator ? gm.navigator.transform.position : Vector3.zero;
        Debug.Log($"[SPAWN] RegisterPlayersInGameManager | traveller={(gm.traveller ? gm.traveller.name : "NULL")} pos={tPos} | navigator={(gm.navigator ? gm.navigator.name : "NULL")} pos={nPos}");
    }

    private void SpawnOrMoveAllPlayers()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var ids = nm.ConnectedClientsIds;
        if (ids == null || ids.Count == 0) return;

        ulong travellerId = NetworkManager.ServerClientId;

        // navigatorId exists only if there is a non-server client connected
        bool hasNavigator = ids.Any(id => id != travellerId);
        ulong navigatorId = ids.FirstOrDefault(id => id != travellerId);

        EnsurePlayer(travellerId, travellerPrefab, travellerSpawn, true);

        if (hasNavigator && ids.Contains(navigatorId))
            EnsurePlayer(navigatorId, navigatorPrefab, navigatorSpawn, false);


        RegisterPlayersInGameManager(travellerId, hasNavigator ? navigatorId : (ulong?)null);   


    }

    private void EnsureOnlyThisClient(ulong clientId)
    {
        if (!sceneSpawnsReady)
            return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        bool isTraveller = clientId == NetworkManager.ServerClientId;
        var prefab = isTraveller ? travellerPrefab : navigatorPrefab;
        var spawn = isTraveller ? travellerSpawn : navigatorSpawn;

        EnsurePlayer(clientId, prefab, spawn, isTraveller);
    }

    private void EnsurePlayer(
        ulong clientId,
        GameObject prefab,
        Transform spawn,
        bool isTraveller)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        if (!nm.ConnectedClients.TryGetValue(clientId, out var cc)) return;

        if (!spawn)
        {
            if (isTraveller)
            {
                spawn = null; // Traveller allowed to fallback
            }
            else
            {
                Debug.LogError("[SPAWN] ❌ Navigator has NO PlayerStartPoint in scene");
                return; // 🚨 Hard fail for Navigator
            }
        }

        Vector3 pos = spawn ? spawn.position : fallbackTravellerPos;
        Quaternion rot = spawn ? spawn.rotation : Quaternion.Euler(0f, fallbackYRotation, 0f);

        // If already exists: teleport + re-apply role (Option A safety)
        if (cc.PlayerObject != null)
        {
            ApplyRoleIfPresent(cc.PlayerObject, isTraveller);
            TeleportNetworkSafe(cc.PlayerObject, pos, rot);
            return;
        }

        // Spawn new
        var obj = Instantiate(prefab, pos, rot);
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[SPAWN] Prefab '{prefab.name}' missing NetworkObject");
            Destroy(obj);
            return;
        }

        // ✅ Option A: set role BEFORE SpawnAsPlayerObject
        ApplyRoleIfPresent(netObj, isTraveller);

        netObj.SpawnAsPlayerObject(clientId, destroyWithScene);
    }

    /// <summary>
    /// Option A helper:
    /// Assumes PlayerMovement has: public void SetRole(PlayerMovement.PlayerRole r)
    /// </summary>
    private static void ApplyRoleIfPresent(NetworkObject playerObject, bool isTraveller)
    {
        if (playerObject == null) return;

        var pm = playerObject.GetComponent<PlayerMovement>();
        if (pm != null)
            pm.SetRole(isTraveller ? PlayerMovement.PlayerRole.Traveller : PlayerMovement.PlayerRole.Navigator);

        // ✅ Set initial area state
        var area = playerObject.GetComponent<PlayerAreaState>();
        if (area != null)
            area.currentArea = isTraveller
                ? PlayerAreaState.AreaState.Maze
                : PlayerAreaState.AreaState.NavigatorRoom; // נווט מתחיל בחדר נווט
    }

    private IEnumerator WaitForNavigatorSpawnPoint(string sceneName, int token)
    {
        const int maxFrames = 180; // ~3 seconds at 60fps

        for (int i = 0; i < maxFrames; i++)
        {
            if (token != sceneLoadToken) yield break;

            // try resolve again
            ResolvePlayerStartPoints(sceneName);

            if (navigatorSpawn != null)
                yield break;

            yield return null;
        }

        Debug.LogWarning($"[SPAWN] Navigator PlayerStartPoint not found in time for scene '{sceneName}'. Using fallback.");
    }

    private static void TeleportNetworkSafe(NetworkObject obj, Vector3 pos, Quaternion rot)
    {
        if (obj == null) return;

        var nt = obj.GetComponent<NetworkTransform>();
        if (nt != null)
        {
            if (nt.CanCommitToTransform)
            {
                nt.Teleport(pos, rot, obj.transform.localScale);
                return;
            }
        }

        // Fallback: at least set locally (authoritative side will replicate)
        obj.transform.SetPositionAndRotation(pos, rot);
    }

    // -------------------------------------------------
    // Tutorial helper (unchanged)
    // -------------------------------------------------

    private bool TryGetTutorialManagerSpawn(
        bool isTraveller,
        out Vector3 pos,
        out Quaternion rot)
    {
        pos = default;
        rot = default;

        var tm = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (tm == null) return false;

        var name = isTraveller ? "travellerSpawn" : "navigatorSpawn";
        var field = tm.GetType().GetField(name);
        if (field != null && field.FieldType == typeof(Transform))
        {
            var tr = (Transform)field.GetValue(tm);
            pos = tr.position;
            rot = tr.rotation;
            return true;
        }

        return false;
    }
}
