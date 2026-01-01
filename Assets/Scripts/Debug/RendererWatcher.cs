// Assets/Scripts/Debug/RendererWatcher.cs
using UnityEngine;

public sealed class RendererWatcher : MonoBehaviour
{
    private Renderer r;

    private void Awake()
    {
        r = GetComponent<Renderer>();
        Debug.Log($"[RendererWatcher] {name} start active={gameObject.activeInHierarchy} renderer={(r ? r.enabled : false)} layer={gameObject.layer}");
    }

    private void LateUpdate()
    {
        if (r == null) return;
        if (!gameObject.activeInHierarchy || !r.enabled)
            Debug.LogWarning($"[RendererWatcher] {name} NOT RENDERING active={gameObject.activeInHierarchy} renderer={r.enabled} layer={gameObject.layer}");
        enabled = false; // log once
    }
}
