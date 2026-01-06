using UnityEngine;

public class ForceWallMinimapRT : MonoBehaviour
{
    [SerializeField] private Camera minimapCam;
    [SerializeField] private Renderer wallScreenRenderer;

    [Header("Use an RT asset (optional). If empty -> create runtime RT")]
    [SerializeField] private RenderTexture rtAsset;
    [SerializeField] private int runtimeSize = 512;

    void Awake()
    {
        if (!minimapCam) minimapCam = GetComponent<Camera>();
        if (!minimapCam) { Debug.LogError("No minimapCam"); return; }

        RenderTexture rt = rtAsset;
        if (!rt)
        {
            rt = new RenderTexture(runtimeSize, runtimeSize, 16, RenderTextureFormat.ARGB32);
            rt.name = "MinimapRT_Runtime";
            rt.Create();
        }

        minimapCam.enabled = true;
        minimapCam.targetTexture = rt;

        if (wallScreenRenderer)
        {
            // חשוב: material ולא sharedMaterial כדי שלכל קליינט יהיה אינסטנס משלו
            wallScreenRenderer.material.mainTexture = rt;
        }

        Debug.Log($"[Minimap] camEnabled={minimapCam.enabled} rt={(minimapCam.targetTexture ? minimapCam.targetTexture.name : "NULL")} wall={(wallScreenRenderer ? wallScreenRenderer.name : "NULL")}");
    }
}
