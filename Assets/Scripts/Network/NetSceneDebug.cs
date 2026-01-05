// Assets/Scripts/Debug/NetSceneDebug.cs
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class NetSceneDebug : MonoBehaviour
{
    private void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (nm.SceneManager != null)
        {
            nm.SceneManager.OnSceneEvent += OnNetSceneEvent;
            nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        }

        SceneManager.sceneLoaded += OnUnitySceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
        {
            nm.SceneManager.OnSceneEvent -= OnNetSceneEvent;
            nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }

        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private static string Role()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return "NoNM";
        if (nm.IsHost) return "Host";
        if (nm.IsServer) return "Server";
        if (nm.IsClient) return "Client";
        return "Offline";
    }

    private static void LogCameras(string where)
    {
        var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        var enabled = cams.Where(c => c != null && c.isActiveAndEnabled).ToArray();

        Debug.Log($"[NetSceneDebug] {where} | role={Role()} | activeScene={SceneManager.GetActiveScene().name} | cameras={cams.Length} enabled={enabled.Length}");

        foreach (var c in cams)
        {
            if (c == null) continue;
            Debug.Log($"[NetSceneDebug]   Cam: name={c.name} active={c.gameObject.activeInHierarchy} enabled={c.enabled} tag={c.tag} depth={c.depth}");
        }
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[NetSceneDebug] Unity sceneLoaded: {scene.name} mode={mode} role={Role()}");
        LogCameras("After Unity sceneLoaded");
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        Debug.Log($"[NetSceneDebug] Active scene changed: {oldScene.name} -> {newScene.name} role={Role()}");
        LogCameras("After activeSceneChanged");
    }

    private void OnNetSceneEvent(SceneEvent sceneEvent)
    {
        Debug.Log($"[NetSceneDebug] NGO SceneEvent: type={sceneEvent.SceneEventType} scene={sceneEvent.SceneName} clientId={sceneEvent.ClientId} role={Role()}");
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        Debug.Log($"[NetSceneDebug] NGO LoadEventCompleted: scene={sceneName} mode={mode} role={Role()} completed=[{string.Join(",", clientsCompleted)}] timedOut=[{string.Join(",", clientsTimedOut)}]");
        LogCameras("After NGO LoadEventCompleted");
    }
}
