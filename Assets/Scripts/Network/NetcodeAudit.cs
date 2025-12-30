// Assets/Scripts/Debug/NetcodeAudit.cs
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NetcodeAudit : MonoBehaviour
{
    private void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.SceneManager != null)
            nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode,
        System.Collections.Generic.List<ulong> completed,
        System.Collections.Generic.List<ulong> timedOut)
    {
        Dump($"NGO OnLoadEventCompleted: {sceneName} completed=[{string.Join(",", completed)}] timedOut=[{string.Join(",", timedOut)}]");
    }

    [ContextMenu("Dump Netcode State")]
    public void DumpFromMenu() => Dump("Manual Dump");

    private static void Dump(string header)
    {
        var nm = NetworkManager.Singleton;
        var role = nm == null ? "NoNM" : (nm.IsHost ? "Host" : nm.IsServer ? "Server" : nm.IsClient ? "Client" : "Offline");

        Debug.Log($"[NetcodeAudit] === {header} | role={role} | activeScene={SceneManager.GetActiveScene().name} ===");

        var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Debug.Log($"[NetcodeAudit] Cameras: total={cams.Length}, enabled={cams.Count(c => c.isActiveAndEnabled)}");
        foreach (var c in cams)
            Debug.Log($"[NetcodeAudit]   Cam {c.name} active={c.gameObject.activeInHierarchy} enabled={c.enabled} tag={c.tag}");

        var sceneNOs = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        Debug.Log($"[NetcodeAudit] Scene NetworkObjects found={sceneNOs.Length}");
        foreach (var no in sceneNOs)
            Debug.Log($"[NetcodeAudit]   NO name={no.name} IsSpawned={no.IsSpawned} NetId={no.NetworkObjectId} active={no.gameObject.activeInHierarchy}");

        if (nm != null && nm.IsServer)
        {
            var spawned = nm.SpawnManager.SpawnedObjectsList;
            Debug.Log($"[NetcodeAudit] Server SpawnedObjects={spawned.Count}");
            foreach (var s in spawned.Take(50))
                Debug.Log($"[NetcodeAudit]   Spawned name={s.name} NetId={s.NetworkObjectId}");
        }
    }
}
