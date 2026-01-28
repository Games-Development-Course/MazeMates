// ===============================
// File: Assets/Scripts/Net/PlayerSpawnManager.cs
// Attach this ONCE to the NetworkManager in StartScene.
// ===============================

using System.Collections;
using System.Linq;
using System.Collections.Generic;
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

    // ✅ Role assignment (stable)
    private ulong _travellerClientId = ulong.MaxValue;
    private ulong _navigatorClientId = ulong.MaxValue;

    private Coroutine _pushIdsCo;

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

        if (_pushIdsCo != null) { StopCoroutine(_pushIdsCo); _pushIdsCo = null; }
    }

    private void OnServerStarted()
    {
        AssignRolesServerStable();
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

        AssignRolesServerStable();

        var sceneName = SceneManager.GetActiveScene().name;
        if (!IsGameplayScene(sceneName)) return;

        if (sceneSpawnsReady)
            EnsureOnlyThisClient(clientId);
    }

    private bool IsGameplayScene(string sceneName)
    {
        return gameplayScenes != null && gameplayScenes.Contains(sceneName);
    }

    private void BeginSceneInit(string sceneName)
    {
        currentSceneName = sceneName;

        sceneSpawnsReady = false;

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

        ResolvePlayerStartPoints(sceneName);

        if (sceneName == gameSceneName)
            yield return WaitForMazeReady(token);

        ResolvePlayerStartPoints(sceneName);

        yield return WaitForNavigatorSpawnPoint(sceneName, token);

        if (sceneName == tutorialSceneName)
            TryResolveFromTutorialManager();

        if (token != sceneLoadToken) yield break;

        sceneSpawnsReady = true;

        AssignRolesServerStable();
        SpawnOrMoveAllPlayers();
    }

    // -------------------------------------------------
    // ✅ Stable role assignment (Traveller != necessarily host)
    // -------------------------------------------------
    private void AssignRolesServerStable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var ids = nm.ConnectedClientsIds?.ToList();
        if (ids == null || ids.Count == 0) return;

        ids.Sort();

        if (_travellerClientId == ulong.MaxValue || !nm.ConnectedClients.ContainsKey(_travellerClientId))
            _travellerClientId = ids[0];

        _navigatorClientId = ids.FirstOrDefault(id => id != _travellerClientId);
        if (_navigatorClientId == 0 && _travellerClientId == 0)
        {
            if (ids.Count < 2)
                _navigatorClientId = ulong.MaxValue;
        }

        if (_travellerClientId == ulong.MaxValue)
            _travellerClientId = NetworkManager.ServerClientId;
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
    private void SpawnOrMoveAllPlayers()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var ids = nm.ConnectedClientsIds;
        if (ids == null || ids.Count == 0) return;

        AssignRolesServerStable();

        ulong travellerId = _travellerClientId;
        bool hasNavigator = _navigatorClientId != ulong.MaxValue && ids.Contains(_navigatorClientId);
        ulong navigatorId = _navigatorClientId;

        EnsurePlayer(travellerId, travellerPrefab, travellerSpawn, true);

        if (hasNavigator)
            EnsurePlayer(navigatorId, navigatorPrefab, navigatorSpawn, false);

        // ✅ Push unique NetIds into GameManager for ID-based resolution/locking
        PushPlayerNetIdsToGameManager(travellerId, hasNavigator ? (ulong?)navigatorId : null);
    }

    private void EnsureOnlyThisClient(ulong clientId)
    {
        if (!sceneSpawnsReady)
            return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        AssignRolesServerStable();

        bool isTraveller = clientId == _travellerClientId;
        var prefab = isTraveller ? travellerPrefab : navigatorPrefab;
        var spawn = isTraveller ? travellerSpawn : navigatorSpawn;

        EnsurePlayer(clientId, prefab, spawn, isTraveller);

        // ✅ After ensuring this client, refresh IDs too (safe)
        bool hasNavigator = _navigatorClientId != ulong.MaxValue && nm.ConnectedClientsIds.Contains(_navigatorClientId);
        PushPlayerNetIdsToGameManager(_travellerClientId, hasNavigator ? (ulong?)_navigatorClientId : null);
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
                return;
            }
        }

        Vector3 pos = spawn ? spawn.position : fallbackTravellerPos;
        Quaternion rot = spawn ? spawn.rotation : Quaternion.Euler(0f, fallbackYRotation, 0f);

        if (cc.PlayerObject != null)
        {
            ApplyRoleIfPresent(cc.PlayerObject, isTraveller);
            TeleportNetworkSafe(cc.PlayerObject, pos, rot);
            return;
        }

        var obj = Instantiate(prefab, pos, rot);
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[SPAWN] Prefab '{prefab.name}' missing NetworkObject");
            Destroy(obj);
            return;
        }

        ApplyRoleIfPresent(netObj, isTraveller);
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene);
    }

    private static void ApplyRoleIfPresent(NetworkObject playerObject, bool isTraveller)
    {
        if (playerObject == null) return;

        var pm = playerObject.GetComponent<PlayerMovement>();
        if (pm != null)
            pm.SetRole(isTraveller ? PlayerMovement.PlayerRole.Traveller : PlayerMovement.PlayerRole.Navigator);

        var area = playerObject.GetComponent<PlayerAreaState>();
        if (area != null)
            area.currentArea = isTraveller
                ? PlayerAreaState.AreaState.Maze
                : PlayerAreaState.AreaState.NavigatorRoom;
    }

    private IEnumerator WaitForNavigatorSpawnPoint(string sceneName, int token)
    {
        const int maxFrames = 180;

        for (int i = 0; i < maxFrames; i++)
        {
            if (token != sceneLoadToken) yield break;

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
        if (nt != null && nt.CanCommitToTransform)
        {
            nt.Teleport(pos, rot, obj.transform.localScale);
            return;
        }

        obj.transform.SetPositionAndRotation(pos, rot);
    }

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

    // -------------------------------------------------
    // ✅ Push NetIds to GameManager (ID-based resolve)
    // -------------------------------------------------
    private void PushPlayerNetIdsToGameManager(ulong travellerClientId, ulong? navigatorClientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        if (_pushIdsCo != null) { StopCoroutine(_pushIdsCo); _pushIdsCo = null; }
        _pushIdsCo = StartCoroutine(PushIdsWhenReady(travellerClientId, navigatorClientId));
    }

    private IEnumerator PushIdsWhenReady(ulong travellerClientId, ulong? navigatorClientId)
    {
        // Wait a few frames for GameManager to exist + be spawned in the gameplay scene
        for (int i = 0; i < 120; i++)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.IsSpawned && gm.IsServer)
            {
                var nm = NetworkManager.Singleton;
                if (nm == null) yield break;

                if (!nm.ConnectedClients.TryGetValue(travellerClientId, out var tClient) || tClient.PlayerObject == null)
                    yield break;

                ulong travellerNetObjId = tClient.PlayerObject.NetworkObjectId;

                ulong navigatorNetObjId = 0;
                if (navigatorClientId.HasValue &&
                    nm.ConnectedClients.TryGetValue(navigatorClientId.Value, out var nClient) &&
                    nClient.PlayerObject != null)
                {
                    navigatorNetObjId = nClient.PlayerObject.NetworkObjectId;
                }

                if (navigatorNetObjId != 0)
                    gm.SetPlayersNetIdsServer(travellerNetObjId, navigatorNetObjId);

                _pushIdsCo = null;
                yield break;
            }

            yield return null;
        }

        _pushIdsCo = null;
    }
}
