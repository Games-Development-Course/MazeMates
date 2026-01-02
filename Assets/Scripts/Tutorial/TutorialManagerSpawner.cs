using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TutorialManagerSpawner : MonoBehaviour
{
    [Header("Prefab must be registered in NetworkManager > NetworkPrefabs")]
    [SerializeField] private GameObject tutorialManagerPrefab;

    [Tooltip("Exact tutorial scene name (as in Build Settings)")]
    [SerializeField] private string tutorialSceneName = "TutorialScene";

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnUnitySceneLoaded;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted += OnNetcodeLoadEventCompleted;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted -= OnNetcodeLoadEventCompleted;
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // זה נתפס גם אם טעינת הסצנה נעשתה עם SceneManager.LoadScene
        TrySpawnForScene(scene.name, "Unity.sceneLoaded");
    }

    private void OnNetcodeLoadEventCompleted(
        string sceneName,
        LoadSceneMode mode,
        System.Collections.Generic.List<ulong> completedClients,
        System.Collections.Generic.List<ulong> timedOutClients)
    {
        // זה נתפס כשנטען דרך NetworkManager.SceneManager.LoadScene
        TrySpawnForScene(sceneName, "NGO.OnLoadEventCompleted");
    }

    private void TrySpawnForScene(string sceneName, string source)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogWarning($"[TutorialManagerSpawner] ({source}) NetworkManager.Singleton is NULL");
            return;
        }

        if (!nm.IsServer)
        {
            // רק השרת עושה spawn
            return;
        }

        if (sceneName != tutorialSceneName)
            return;

        Debug.Log($"[TutorialManagerSpawner] ({source}) In tutorial scene -> ensuring TutorialManager exists...");

        EnsureTutorialManagerSpawned();
    }

    private void EnsureTutorialManagerSpawned()
    {
        // אם כבר קיים בסצנה (גם אם inactive) - לא ליצור עוד אחד
        var existing = Object.FindObjectsOfType<TutorialManager>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            var existingNetObj = existing[i].GetComponent<NetworkObject>();
            if (existingNetObj != null && existingNetObj.IsSpawned)
            {
                Debug.Log("[TutorialManagerSpawner] TutorialManager already spawned.");
                return;
            }
        }

        if (tutorialManagerPrefab == null)
        {
            Debug.LogError("[TutorialManagerSpawner] tutorialManagerPrefab is NOT assigned in Inspector!");
            return;
        }

        var prefabNetObj = tutorialManagerPrefab.GetComponent<NetworkObject>();
        if (prefabNetObj == null)
        {
            Debug.LogError("[TutorialManagerSpawner] Prefab is missing NetworkObject component!");
            return;
        }

        var go = Instantiate(tutorialManagerPrefab);
        var spawnedNetObj = go.GetComponent<NetworkObject>();

        spawnedNetObj.Spawn(true);
        Debug.Log("[TutorialManagerSpawner] TutorialManager Spawn() called (server).");
    }
}
