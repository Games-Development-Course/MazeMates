using UnityEngine;
using Unity.Netcode;

public class PlayerAreaCullingInit : NetworkBehaviour
{
    [SerializeField] private string outsideLayer = "NavigatorOutside";
    [SerializeField] private string insideLayer = "NavigatorInside";

    public override void OnNetworkSpawn()
    {
        // ✅ רק על השחקן המקומי, כדי לא לשבש את ה-Main Camera של אחרים
        if (!IsOwner) return;

        var state = GetComponent<PlayerAreaState>();
        if (!state) return;

        var cam = Camera.main;
        if (!cam) return;

        int outside = LayerMask.NameToLayer(outsideLayer);
        int inside = LayerMask.NameToLayer(insideLayer);
        if (outside < 0 || inside < 0) return;

        int outsideBit = 1 << outside;
        int insideBit = 1 << inside;

        if (state.currentArea == PlayerAreaState.AreaState.Maze)
        {
            // show Outside, hide Inside
            cam.cullingMask |= outsideBit;
            cam.cullingMask &= ~insideBit;
        }
        else
        {
            // show Inside, hide Outside
            cam.cullingMask |= insideBit;
            cam.cullingMask &= ~outsideBit;
        }
    }
}
