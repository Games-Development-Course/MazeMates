// ===============================
// File: Assets/Scripts/Net/PlayerSpawnManager.cs
// Put this ONCE (recommended on the NetworkManager object in the first scene).
// Remove it from other scenes OR leave it—duplicates will self-destruct.
// ===============================
// Based on your current version. :contentReference[oaicite:0]{index=0}
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

    [Header("Fallback Spawn (used if NO PlayerStartPoint exists in the loaded scene)")]
    [SerializeField] private Vector3 fallbackTravellerPos = new Vector3(1f, 0f, 1f);
    [SerializeField] private Vector3 fallbackNavigatorPos = new Vector3(3f, 0f, 3f);
    [SerializeField] private float fallbackYRotation = 0f;

    [Header("Tutorial Scene Name (optional)")]
    [SerializeField] private string tutorialSceneName = "TutorialScene";

    private static PlayerSpawnManager instance;

    private Transform travellerSpawn;
    private Transform navigatorSpawn;

    private void Awake()
    {
        // Always apply DontDestroyOnLoad on the ROOT so Unity won't warn and it will actually persist.
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

        string sceneName = SceneManager.GetActiveScene().name;

        ResolveSpawnPointsFromScene(sceneName);
        TrySpawnOrMoveAllPlayers(sceneName);
    }

    private void OnLoadEventCompleted(
        string sceneName,
        LoadSceneMode mode,
        System.Collections.Generic.List<ulong> completedClients,
        System.Collections.Generic.List<ulong> timedOutClients)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        ResolveSpawnPointsFromScene(sceneName);
        TrySpawnOrMoveAllPlayers(sceneName);
    }

    // Finds PlayerStartPoint objects ONLY if they exist in that scene.
    // If they don't exist (like your TutorialScene case), we will fall back to default spawn positions.
    private void ResolveSpawnPointsFromScene(string sceneName)
    {
        travellerSpawn = null;
        navigatorSpawn = null;

        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = SceneManager.GetActiveScene();

        // Find all PlayerStartPoint in memory (including inactive), then keep only those that belong to this scene.
        var points = Object.FindObjectsByType<PlayerStartPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            if (p == null) continue;

            if (p.gameObject.scene != scene)
                continue;

            if (p.role == PlayerStartPoint.Role.Traveller)
                travellerSpawn = p.transform;
            else if (p.role == PlayerStartPoint.Role.Navigator)
                navigatorSpawn = p.transform;
        }

        // Keep warnings (useful), but spawn will still happen via fallback.
        if (travellerSpawn == null)
            Debug.LogWarning($"[SPAWN] No PlayerStartPoint(Role.Traveller) found in scene '{scene.name}' -> using fallback/tutorial spawns.");

        if (navigatorSpawn == null)
            Debug.LogWarning($"[SPAWN] No PlayerStartPoint(Role.Navigator) found in scene '{scene.name}' -> using fallback/tutorial spawns.");
    }

    private void TrySpawnOrMoveAllPlayers(string sceneName)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var ids = nm.ConnectedClientsIds;
        if (ids == null || ids.Count == 0) return;

        ulong travellerId = NetworkManager.ServerClientId; // host is traveller in your design

        ulong navigatorCandidate = ids.FirstOrDefault(id => id != travellerId);
        bool hasNavigator = ids.Any(id => id != travellerId);
        ulong? navigatorId = hasNavigator ? navigatorCandidate : (ulong?)null;

        EnsurePlayer(travellerId, travellerPrefab, travellerSpawn, sceneName);

        if (navigatorId.HasValue && ids.Contains(navigatorId.Value))
            EnsurePlayer(navigatorId.Value, navigatorPrefab, navigatorSpawn, sceneName);
    }

    private void EnsurePlayer(ulong clientId, GameObject prefab, Transform spawn, string sceneName)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (!nm.ConnectedClients.TryGetValue(clientId, out var cc))
            return;

        if (prefab == null)
        {
            Debug.LogError($"[SPAWN] Missing prefab for clientId={clientId}");
            return;
        }

        bool isTraveller = (clientId == NetworkManager.ServerClientId);

        // 1) Prefer explicit PlayerStartPoint (if exists in this scene)
        Vector3 pos;
        Quaternion rot;

        if (spawn != null)
        {
            pos = spawn.position;
            rot = spawn.rotation;
        }
        else
        {
            // 2) If we're in TutorialScene, try to take spawn from TutorialManager (if it exposes start transforms)
            if (!string.IsNullOrEmpty(tutorialSceneName) && sceneName == tutorialSceneName)
            {
                if (TryGetTutorialManagerSpawn(isTraveller, out var tPos, out var tRot))
                {
                    pos = tPos;
                    rot = tRot;
                }
                else
                {
                    // 3) Final fallback (inspector values)
                    pos = isTraveller ? fallbackTravellerPos : fallbackNavigatorPos;
                    rot = Quaternion.Euler(0f, fallbackYRotation, 0f);
                }
            }
            else
            {
                // Non-tutorial scenes: fallback if no PlayerStartPoint exists
                pos = isTraveller ? fallbackTravellerPos : fallbackNavigatorPos;
                rot = Quaternion.Euler(0f, fallbackYRotation, 0f);
            }
        }

        // Move existing player if already spawned
        if (cc.PlayerObject != null)
        {
            cc.PlayerObject.transform.SetPositionAndRotation(pos, rot);
            return;
        }

        // Spawn player object
        var obj = Instantiate(prefab, pos, rot);
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[SPAWN] Prefab '{prefab.name}' missing NetworkObject");
            Destroy(obj);
            return;
        }

        netObj.SpawnAsPlayerObject(clientId, destroyWithScene);
    }

    // Tries to read spawn info from TutorialManager at runtime.
    // ✅ This will compile even if TutorialManager doesn't have these fields; it just returns false.
    private bool TryGetTutorialManagerSpawn(bool isTraveller, out Vector3 pos, out Quaternion rot)
    {
        pos = default;
        rot = default;

        var tm = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (tm == null) return false;

        // Common patterns: public Transform travellerSpawn / navigatorSpawn, or start points
        var tmType = tm.GetType();

        // Try fields (Transform)
        var fieldName = isTraveller ? "travellerSpawn" : "navigatorSpawn";
        var f = tmType.GetField(fieldName);
        if (f != null && f.FieldType == typeof(Transform))
        {
            var tr = (Transform)f.GetValue(tm);
            if (tr != null)
            {
                pos = tr.position;
                rot = tr.rotation;
                return true;
            }
        }

        // Try properties (Transform)
        var p = tmType.GetProperty(fieldName);
        if (p != null && p.PropertyType == typeof(Transform))
        {
            var tr = (Transform)p.GetValue(tm);
            if (tr != null)
            {
                pos = tr.position;
                rot = tr.rotation;
                return true;
            }
        }

        // Try alternate common names
        string[] alt = isTraveller
            ? new[] { "travellerStart", "travellerStartPoint", "travellerStartTransform", "startTraveller" }
            : new[] { "navigatorStart", "navigatorStartPoint", "navigatorStartTransform", "startNavigator" };

        for (int i = 0; i < alt.Length; i++)
        {
            var name = alt[i];

            var ff = tmType.GetField(name);
            if (ff != null && ff.FieldType == typeof(Transform))
            {
                var tr = (Transform)ff.GetValue(tm);
                if (tr != null)
                {
                    pos = tr.position;
                    rot = tr.rotation;
                    return true;
                }
            }

            var pp = tmType.GetProperty(name);
            if (pp != null && pp.PropertyType == typeof(Transform))
            {
                var tr = (Transform)pp.GetValue(tm);
                if (tr != null)
                {
                    pos = tr.position;
                    rot = tr.rotation;
                    return true;
                }
            }
        }

        return false;
    }
}
