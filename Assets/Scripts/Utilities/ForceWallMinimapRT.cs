using UnityEngine;

public class ForceWallMinimapRT : MonoBehaviour
{
    [Header("Minimap Camera (renders into RT)")]
    [SerializeField] private Camera minimapCam;

    [Header("Renderer of the Quad/Screen")]
    [SerializeField] private Renderer wallScreenRenderer;

    [Header("Optional RT asset. If empty -> create runtime RT")]
    [SerializeField] private RenderTexture rtAsset;
    [SerializeField] private int runtimeSize = 1024;

    [Header("Camera Output")]
    [SerializeField] private Color background = Color.black;

    [Tooltip("If empty -> render everything")]
    [SerializeField] private LayerMask cullingMask = ~0;

    private RenderTexture _rt;

    private void Awake()
    {
        if (!wallScreenRenderer) wallScreenRenderer = GetComponent<Renderer>();

        if (!minimapCam)
        {
            // אם הסקריפט בטעות על ה-Quad ולא על מצלמה, לא נניח GetComponent<Camera>()
            minimapCam = FindFirstObjectByType<Camera>();
        }

        if (!minimapCam)
        {
            Debug.LogError("[MinimapRT] No minimapCam assigned.");
            return;
        }

        _rt = rtAsset;
        if (!_rt)
        {
            _rt = new RenderTexture(runtimeSize, runtimeSize, 16, RenderTextureFormat.ARGB32);
            _rt.name = "MinimapRT_Runtime";
            _rt.Create();
        }

        // --- camera sanity ---
        minimapCam.enabled = true;
        minimapCam.targetTexture = _rt;

        minimapCam.orthographic = true;
        minimapCam.clearFlags = CameraClearFlags.SolidColor;
        minimapCam.backgroundColor = background;
        minimapCam.cullingMask = cullingMask;

        // --- apply to quad material (URP uses _BaseMap) ---
        if (wallScreenRenderer)
        {
            var mat = wallScreenRenderer.material; // instance per client
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", _rt);
            else if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", _rt);
            else
                mat.mainTexture = _rt;

            // לפעמים המסך “כהה” כי ה-Material Tint לא לבן
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        }

        Debug.Log($"[MinimapRT] OK | cam={minimapCam.name} rt={_rt.name} quad={wallScreenRenderer?.name}");
    }
}
