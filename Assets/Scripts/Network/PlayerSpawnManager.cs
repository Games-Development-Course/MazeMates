// Assets/Scripts/Net/PlayerSpawnManager.cs
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerSpawnManager : MonoBehaviour
{
    [Header("Spawn points")]
    [SerializeField] private Transform travSpawn;
    [SerializeField] private Transform navSpawn;

    [Header("Prefabs (must be registered in NetworkManager > NetworkPrefabs)")]
    [SerializeField] private GameObject travellerPrefab;
    [SerializeField] private GameObject navigatorPrefab;

    [Header("Options")]
    [SerializeField] private bool destroyWithScene = false; // usually false if you load scenes with NGO

    [Header("Tutorial")]
    [SerializeField] private string tutorialSceneName = "TutorialScene";
    [SerializeField] private bool autoStartTutorialInTutorialScene = true;

    // NEW: Prefab that will be spawned on the server when TutorialScene loads
    [SerializeField] private TutorialManager tutorialManagerPrefab;

    // prevents starting tutorial multiple times
    private bool tutorialStartedThisScene = false;
    private string lastLoadedSceneName = "";

    private void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;

        nm.OnClientConnectedCallback += OnClientConnectedServerOnly;
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;

        nm.OnClientConnectedCallback -= OnClientConnectedServerOnly;
    }

    private void OnClientConnectedServerOnly(ulong _)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        TrySpawnOrMoveAllPlayers();

        // If we are already in TutorialScene (late join/reconnect), ensure TM exists
        if (SceneManager.GetActiveScene().name == tutorialSceneName)
        {
            EnsureTutorialManagerSpawned();
            TryAutoStartTutorialIfReady();
        }
    }

    private void OnLoadEventCompleted(
        string sceneName,
        LoadSceneMode mode,
        System.Collections.Generic.List<ulong> completedClients,
        System.Collections.Generic.List<ulong> timedOutClients)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        Debug.Log(
            $"[SPAWN] OnLoadEventCompleted scene={sceneName} completed=[{string.Join(",", completedClients)}] timedOut=[{string.Join(",", timedOutClients)}]"
        );

        // Reset per-scene guard when a new scene is loaded
        if (sceneName != lastLoadedSceneName)
        {
            lastLoadedSceneName = sceneName;
            tutorialStartedThisScene = false;
        }

        // If TutorialScene just loaded, spawn TutorialManager BEFORE trying to start tutorial
        if (sceneName == tutorialSceneName)
        {
            EnsureTutorialManagerSpawned();
        }

        TrySpawnOrMoveAllPlayers();

        // Start tutorial only in TutorialScene
        if (sceneName == tutorialSceneName)
            TryAutoStartTutorialIfReady();
    }

    private void TrySpawnOrMoveAllPlayers()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var ids = nm.ConnectedClientsIds;
        if (ids == null || ids.Count == 0) return;

        ulong hostId = NetworkManager.ServerClientId;

        // Find the other client (Navigator) if present
        bool hasNavigator = ids.Any(id => id != hostId);
        ulong navigatorId = hasNavigator ? ids.First(id => id != hostId) : 0;

        EnsurePlayer(hostId, travellerPrefab, travSpawn);

        if (hasNavigator)
            EnsurePlayer(navigatorId, navigatorPrefab, navSpawn);
    }

    private void EnsurePlayer(ulong clientId, GameObject prefab, Transform spawn)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (!nm.ConnectedClients.TryGetValue(clientId, out var cc))
        {
            Debug.LogWarning($"[SPAWN] No ConnectedClient entry for clientId={clientId}");
            return;
        }

        if (spawn == null)
        {
            Debug.LogError($"[SPAWN] Missing spawn Transform for clientId={clientId}");
            return;
        }

        if (cc.PlayerObject != null)
        {
            cc.PlayerObject.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            Debug.Log($"[SPAWN] Moved existing PlayerObject for clientId={clientId} to spawn");
            return;
        }

        if (prefab == null)
        {
            Debug.LogError($"[SPAWN] Missing prefab for clientId={clientId}");
            return;
        }

        var obj = Instantiate(prefab, spawn.position, spawn.rotation);
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[SPAWN] Prefab '{prefab.name}' missing NetworkObject");
            Destroy(obj);
            return;
        }

        netObj.SpawnAsPlayerObject(clientId, destroyWithScene);
        Debug.Log(
            $"[SPAWN] Spawned PlayerObject '{prefab.name}' for clientId={clientId} destroyWithScene={destroyWithScene}"
        );
    }

    // NEW: spawns TutorialManager prefab on the server when TutorialScene loads (if not already present)
    private void EnsureTutorialManagerSpawned()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        // Already exists in scene? then don't spawn another
        var existing = FindFirstObjectByType<TutorialManager>();
        if (existing != null)
        {
            // If it exists but isn't spawned yet, just wait for NGO; don't duplicate.
            Debug.Log($"[SPAWN] TutorialManager already exists (IsSpawned={existing.IsSpawned}) -> not spawning another.");
            return;
        }

        if (tutorialManagerPrefab == null)
        {
            Debug.LogError("[SPAWN] tutorialManagerPrefab is NULL! Assign the TutorialManager prefab in PlayerSpawnManager inspector.");
            return;
        }

        var tmInstance = Instantiate(tutorialManagerPrefab);
        var netObj = tmInstance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[SPAWN] TutorialManager prefab is missing NetworkObject component!");
            Destroy(tmInstance.gameObject);
            return;
        }

        netObj.Spawn(true);
        Debug.Log($"[SPAWN] Spawned TutorialManager prefab successfully. NetId={netObj.NetworkObjectId}");
    }

    private void TryAutoStartTutorialIfReady()
    {
        if (!autoStartTutorialInTutorialScene) return;
        if (tutorialStartedThisScene) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        if (SceneManager.GetActiveScene().name != tutorialSceneName) return;

        // Need 2 players connected
        if (nm.ConnectedClientsIds == null || nm.ConnectedClientsIds.Count < 2) return;

        // Ensure both PlayerObjects exist (spawned)
        foreach (var id in nm.ConnectedClientsIds)
        {
            if (!nm.ConnectedClients.TryGetValue(id, out var cc) || cc.PlayerObject == null)
                return;
        }

        // Ensure TutorialManager exists (spawn it if missing)
        var tm = FindFirstObjectByType<TutorialManager>();
        if (tm == null)
        {
            Debug.LogWarning("[SPAWN] TutorialManager not found in TutorialScene. Trying to spawn it now...");
            EnsureTutorialManagerSpawned();
            tm = FindFirstObjectByType<TutorialManager>();
            if (tm == null) return;
        }

        // Must be spawned as a NetworkObject
        if (!tm.IsSpawned)
        {
            Debug.LogWarning("[SPAWN] TutorialManager exists but is not spawned yet (wait a frame).");
            return;
        }

        if (!tm.TutorialActive.Value)
        {
            Debug.Log("[SPAWN] Starting tutorial now -> TutorialManager.StartTutorial()");
            tm.StartTutorial();
        }

        tutorialStartedThisScene = true;
    }
}
