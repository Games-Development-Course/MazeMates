// ======================= File: Assets/Scripts/Tutorial/TutorialManagerSpawner.cs =======================
// SAFE VERSION: won't break player spawning.
// - Only server spawns TutorialManager
// - Only in TutorialScene
// - Delays spawn slightly AFTER scene load events
// - Wraps in try/catch so NGO flow never breaks
// - Does NOT call NetworkShow
// - Does NOT try to spawn "existing in-scene" objects (avoids scene object edge cases)

using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TutorialManagerSpawner : MonoBehaviour
{
    [Header("Prefab must be registered in NetworkManager > NetworkPrefabs")]
    [SerializeField] private GameObject tutorialManagerPrefab;

    [Tooltip("Exact tutorial scene name (as in Build Settings)")]
    [SerializeField] private string tutorialSceneName = "TutorialScene";

    private bool spawnQueued;

    private void OnEnable()
    {
        Debug.Log($"[TMS] OnEnable | activeScene={SceneManager.GetActiveScene().name}");

        SceneManager.sceneLoaded += OnUnitySceneLoaded;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
        {
            nm.SceneManager.OnLoadEventCompleted += OnNetcodeLoadEventCompleted;
            Debug.Log($"[TMS] Subscribed to NGO.OnLoadEventCompleted | IsServer={nm.IsServer} IsHost={nm.IsHost} LocalClientId={nm.LocalClientId}");
        }
        else
        {
            Debug.LogWarning("[TMS] NetworkManager or SceneManager is NULL on OnEnable");
        }
    }

    private void OnDisable()
    {
        Debug.Log($"[TMS] OnDisable | activeScene={SceneManager.GetActiveScene().name}");

        SceneManager.sceneLoaded -= OnUnitySceneLoaded;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted -= OnNetcodeLoadEventCompleted;
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[TMS] Unity.sceneLoaded | scene={scene.name} mode={mode}");
        QueueSpawnIfNeeded(scene.name, "Unity.sceneLoaded");
    }

    private void OnNetcodeLoadEventCompleted(
        string sceneName,
        LoadSceneMode mode,
        List<ulong> completedClients,
        List<ulong> timedOutClients)
    {
        Debug.Log($"[TMS] NGO.OnLoadEventCompleted | scene={sceneName} completed=[{string.Join(",", completedClients)}] timedOut=[{string.Join(",", timedOutClients)}]");
        QueueSpawnIfNeeded(sceneName, "NGO.OnLoadEventCompleted");
    }

    private void QueueSpawnIfNeeded(string sceneName, string source)
    {
        var nm = NetworkManager.Singleton;

        Debug.Log($"[TMS] ({source}) QueueSpawnIfNeeded | scene={sceneName} wanted={tutorialSceneName} nm={(nm != null)} isServer={nm?.IsServer}");

        if (nm == null) return;
        if (!nm.IsServer) return;
        if (sceneName != tutorialSceneName) return;

        // Prevent double-queue from both callbacks
        if (spawnQueued)
        {
            Debug.Log("[TMS] Spawn already queued -> skip");
            return;
        }

        spawnQueued = true;
        StartCoroutine(SpawnAfterSceneSettles());
    }

    private IEnumerator SpawnAfterSceneSettles()
    {
        // Let NGO finish all internal spawn/scene sync first
        yield return null;
        yield return new WaitForSeconds(0.2f);

        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                Debug.LogWarning("[TMS] SpawnAfterSceneSettles aborted: not server / no NM");
                spawnQueued = false;
                yield break;
            }

            // If already exists spawned, do nothing
            var existing = Object.FindObjectsOfType<TutorialManager>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                var tm = existing[i];
                if (tm == null) continue;
                var no = tm.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned)
                {
                    Debug.Log($"[TMS] TutorialManager already spawned (found '{tm.gameObject.name}' ObjId={no.NetworkObjectId}).");
                    spawnQueued = false;
                    yield break;
                }
            }

            if (tutorialManagerPrefab == null)
            {
                Debug.LogError("[TMS] tutorialManagerPrefab is NOT assigned in Inspector!");
                spawnQueued = false;
                yield break;
            }

            var prefabNO = tutorialManagerPrefab.GetComponent<NetworkObject>();
            if (prefabNO == null)
            {
                Debug.LogError("[TMS] Prefab is missing NetworkObject component!");
                spawnQueued = false;
                yield break;
            }

            Debug.Log($"[TMS] Spawning TutorialManager | prefab='{tutorialManagerPrefab.name}' SpawnWithObservers={prefabNO.SpawnWithObservers}");

            var go = Instantiate(tutorialManagerPrefab);
            go.name = tutorialManagerPrefab.name + "_Spawned";
            var noSpawned = go.GetComponent<NetworkObject>();

            noSpawned.Spawn(true);

            Debug.Log($"[TMS] TutorialManager Spawn() OK | ObjId={noSpawned.NetworkObjectId} IsSpawned={noSpawned.IsSpawned} ConnectedClients=[{string.Join(",", nm.ConnectedClientsIds)}]");
        }
        catch (System.SystemException e)
        {
            Debug.LogError($"[TMS] EXCEPTION during spawn (won't crash NGO): {e}");
        }
        finally
        {
            spawnQueued = false;
        }
    }
}
