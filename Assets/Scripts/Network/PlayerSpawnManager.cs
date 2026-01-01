// ===============================
// File: Assets/Scripts/Net/PlayerSpawnManager.cs
// Put this ONCE (recommended on the NetworkManager object in the first scene).
// Remove it from other scenes OR leave it—duplicates will self-destruct.
// ===============================
using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerSpawnManager : MonoBehaviour
{
    [Header("Prefabs (must be registered in NetworkManager > NetworkPrefabs)")]
    [SerializeField] private GameObject travellerPrefab;
    [SerializeField] private GameObject navigatorPrefab;

    [Header("Options")]
    [SerializeField] private bool destroyWithScene = false;

    private static PlayerSpawnManager instance;

    private Transform travellerSpawn;
    private Transform navigatorSpawn;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

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

        ResolveSpawnPointsFromScene();
        TrySpawnOrMoveAllPlayers();
    }

    private void OnLoadEventCompleted(
        string sceneName,
        LoadSceneMode mode,
        System.Collections.Generic.List<ulong> completedClients,
        System.Collections.Generic.List<ulong> timedOutClients)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        ResolveSpawnPointsFromScene();
        TrySpawnOrMoveAllPlayers();
    }

    private void ResolveSpawnPointsFromScene()
    {
        travellerSpawn = null;
        navigatorSpawn = null;

        var points = FindObjectsByType<PlayerStartPoint>(FindObjectsSortMode.None);

        foreach (var p in points)
        {
            if (!p.gameObject.activeInHierarchy) continue;

            if (p.role == PlayerStartPoint.Role.Traveller)
                travellerSpawn = p.transform;
            else if (p.role == PlayerStartPoint.Role.Navigator)
                navigatorSpawn = p.transform;
        }

        if (travellerSpawn == null)
            Debug.LogWarning($"[SPAWN] Missing PlayerStartPoint(Role.Traveller) in scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'");

        if (navigatorSpawn == null)
            Debug.LogWarning($"[SPAWN] Missing PlayerStartPoint(Role.Navigator) in scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'");
    }

    private void TrySpawnOrMoveAllPlayers()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var ids = nm.ConnectedClientsIds;
        if (ids == null || ids.Count == 0) return;

        ulong travellerId = NetworkManager.ServerClientId; // host is traveller in your design
        ulong? navigatorId = ids.FirstOrDefault(id => id != travellerId);

        EnsurePlayer(travellerId, travellerPrefab, travellerSpawn);

        if (navigatorId.HasValue && ids.Contains(navigatorId.Value))
            EnsurePlayer(navigatorId.Value, navigatorPrefab, navigatorSpawn);
    }

    private void EnsurePlayer(ulong clientId, GameObject prefab, Transform spawn)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (!nm.ConnectedClients.TryGetValue(clientId, out var cc))
            return;

        if (spawn == null)
            return;

        if (cc.PlayerObject != null)
        {
            cc.PlayerObject.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
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
    }
    
}
