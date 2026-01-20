using UnityEngine;

public class ForceMinimapChildLayer : MonoBehaviour
{
    [SerializeField] private Transform minimapChild;
    [SerializeField] private string minimapLayerName = "MinimapIcon";

    private void Awake()
    {
        if (!minimapChild) return;
        int layer = LayerMask.NameToLayer(minimapLayerName);
        if (layer < 0) return;

        SetLayerRecursively(minimapChild.gameObject, layer);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
