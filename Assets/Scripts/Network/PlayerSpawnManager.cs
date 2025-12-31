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

        // Handles initial join in the current scene (if you want spawning there too).
        TrySpawnOrMoveAllPlayers();
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode,
        System.Collections.Generic.List<ulong> completedClients,
        System.Collections.Generic.List<ulong> timedOutClients)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        Debug.Log($"[SPAWN] OnLoadEventCompleted scene={sceneName} completed=[{string.Join(",", completedClients)}] timedOut=[{string.Join(",", timedOutClients)}]");

        TrySpawnOrMoveAllPlayers();
    }

    private void TrySpawnOrMoveAllPlayers()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var ids = nm.ConnectedClientsIds;
        if (ids == null || ids.Count == 0) return;

        ulong hostId = NetworkManager.ServerClientId; // host's clientId
        ulong? navId = ids.FirstOrDefault(id => id != hostId);

        EnsurePlayer(hostId, travellerPrefab, travSpawn);

        if (navId.HasValue && ids.Contains(navId.Value))
            EnsurePlayer(navId.Value, navigatorPrefab, navSpawn);
    }

    private void EnsurePlayer(ulong clientId, GameObject prefab, Transform spawn)
    {
        var nm = NetworkManager.Singleton;
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
            // Already exists: move to spawn point (useful when players persist across scenes)
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
        Debug.Log($"[SPAWN] Spawned PlayerObject '{prefab.name}' for clientId={clientId} destroyWithScene={destroyWithScene}");
    }
}
