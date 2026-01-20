using UnityEngine;
using Unity.Netcode;

public class ApplyInitialCullingOnSpawn : NetworkBehaviour
{
    [SerializeField] private string outsideLayer = "NavigatorOutside";
    [SerializeField] private string insideLayer = "NavigatorInside";

    public override void OnNetworkSpawn()
    {
        // ✅ רק השחקן המקומי משפיע על ה-Main Camera במחשב שלו
        if (!IsOwner) return;

        ApplyNow();
    }

    private void ApplyNow()
    {
        var area = GetComponent<PlayerAreaState>();
        if (!area) return;

        Camera cam = Camera.main;
        if (!cam)
        {
            Debug.LogWarning("[CULL] No Camera.main (MainCamera tag missing?)");
            return;
        }

        int outside = LayerMask.NameToLayer(outsideLayer);
        int inside = LayerMask.NameToLayer(insideLayer);
        if (outside < 0 || inside < 0)
        {
            Debug.LogWarning("[CULL] Missing layers for outside/inside");
            return;
        }

        int outsideBit = 1 << outside;
        int insideBit = 1 << inside;

        if (area.currentArea == PlayerAreaState.AreaState.NavigatorRoom)
        {
            // NavigatorRoom: show Inside, hide Outside
            cam.cullingMask |= insideBit;
            cam.cullingMask &= ~outsideBit;
        }
        else
        {
            // Maze: show Outside, hide Inside
            cam.cullingMask |= outsideBit;
            cam.cullingMask &= ~insideBit;
        }
    }
}
