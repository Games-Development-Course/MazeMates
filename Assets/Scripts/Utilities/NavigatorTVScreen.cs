using UnityEngine;

public class NavigatorTVScreen : MonoBehaviour
{
    [SerializeField] private MeshRenderer quadRenderer;
    [SerializeField] private int materialIndex = 0;

    private Material[] _originalSharedMats;
    private bool _cached;

    private void Awake()
    {
        if (quadRenderer == null) quadRenderer = GetComponent<MeshRenderer>();
        _originalSharedMats = quadRenderer != null ? quadRenderer.sharedMaterials : null;
        _cached = true;

        Debug.Log($"[TV] Awake | obj={name} | mats={quadRenderer?.sharedMaterials?.Length ?? -1}", this);
    }

    public void Apply(Texture tex)
    {
        if (quadRenderer == null || tex == null) return;

        var shared = quadRenderer.sharedMaterials;
        if (shared == null || shared.Length == 0) return;

        int idx = Mathf.Clamp(materialIndex, 0, shared.Length - 1);

        var m = new Material(shared[idx]);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        else if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        else m.mainTexture = tex;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);

        if (m.HasProperty("_EmissionMap"))
            m.SetTexture("_EmissionMap", tex);

        if (m.HasProperty("_EmissionColor"))
            m.SetColor("_EmissionColor", Color.white);

        m.EnableKeyword("_EMISSION");


        var mats = (Material[])shared.Clone();
        mats[idx] = m;
        quadRenderer.materials = mats;

        Debug.Log($"[TV] Apply | obj={name} | idx={idx} | tex={tex.name}", this);
    }

    public void Clear()
    {
        if (!_cached || quadRenderer == null || _originalSharedMats == null) return;
        quadRenderer.sharedMaterials = _originalSharedMats;
        Debug.Log($"[TV] Clear | obj={name}", this);
    }
}
