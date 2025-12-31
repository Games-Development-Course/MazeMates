using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManagerSpawner : MonoBehaviour
{
    [SerializeField] private string tutorialSceneName = "TutorialScene";
    [SerializeField] private TutorialManager tutorialManagerPrefab;

    private void Awake()
    {
        Debug.Log("[TMS] Awake (spawner exists)");
    }

    private void OnEnable()
    {
        Debug.Log("[TMS] OnEnable");

        var nm = NetworkManager.Singleton;
        Debug.Log($"[TMS] NM exists={(nm != null)}");

        if (nm != null && nm.SceneManager != null)
        {
            nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            Debug.Log("[TMS] Subscribed to NGO OnLoadEventCompleted");
        }

        SceneManager.sceneLoaded += OnUnitySceneLoaded; // רק לדיבוג: לראות שהסצנה נטענה בכלל
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;

        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[TMS] Unity sceneLoaded: {scene.name} mode={mode}");
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode,
        System.Collections.Generic.List<ulong> completed,
        System.Collections.Generic.List<ulong> timedOut)
    {
        var nm = NetworkManager.Singleton;
        Debug.Log($"[TMS] NGO OnLoadEventCompleted: scene={sceneName} IsServer={(nm != null && nm.IsServer)}");

        if (nm == null || !nm.IsServer) return;

        if (!string.Equals(sceneName, tutorialSceneName, StringComparison.Ordinal))
        {
            Debug.Log($"[TMS] Scene name mismatch. expected='{tutorialSceneName}' got='{sceneName}'");
            return;
        }

        var existing = FindFirstObjectByType<TutorialManager>();
        Debug.Log($"[TMS] Existing TutorialManager found? {(existing != null)}");

        if (existing != null) return;

        if (tutorialManagerPrefab == null)
        {
            Debug.LogError("[TMS] tutorialManagerPrefab is NULL! Assign prefab in Inspector.");
            return;
        }

        var tm = Instantiate(tutorialManagerPrefab);
        var netObj = tm.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[TMS] Prefab missing NetworkObject component!");
            Destroy(tm.gameObject);
            return;
        }

        netObj.Spawn(true);
        Debug.Log($"[TMS] Spawned TutorialManager OK. NetId={netObj.NetworkObjectId} IsSpawned={netObj.IsSpawned}");
    }
}
