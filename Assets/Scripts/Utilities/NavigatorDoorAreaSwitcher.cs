using UnityEngine;

public class NavigatorDoorAreaSwitcher : MonoBehaviour
{
    [Header("Layer names")]
    [SerializeField] private string outsideLayer = "NavigatorOutside";
    [SerializeField] private string insideLayer = "NavigatorInside";

    private int outsideBit;
    private int insideBit;

    private void Awake()
    {
        int outside = LayerMask.NameToLayer(outsideLayer);
        int inside = LayerMask.NameToLayer(insideLayer);

        if (outside < 0) Debug.LogError($"Layer '{outsideLayer}' not found.");
        if (inside < 0) Debug.LogError($"Layer '{insideLayer}' not found.");

        outsideBit = 1 << outside;
        insideBit = 1 << inside;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        var state = other.GetComponent<PlayerAreaState>();
        if (!state)
            return;

        Camera cam = Camera.main; // Main Camera with CinemachineBrain
        if (!cam)
        {
            Debug.LogWarning("No Camera.main found. Make sure your Main Camera has the 'MainCamera' tag.");
            return;
        }

        if (state.currentArea == PlayerAreaState.AreaState.Maze)
            EnterNavigatorRoom(state, cam);
        else
            ExitNavigatorRoom(state, cam);
    }

    private void EnterNavigatorRoom(PlayerAreaState state, Camera cam)
    {
        // show Inside, hide Outside
        cam.cullingMask |= insideBit;
        cam.cullingMask &= ~outsideBit;

        state.currentArea = PlayerAreaState.AreaState.NavigatorRoom;
    }

    private void ExitNavigatorRoom(PlayerAreaState state, Camera cam)
    {
        // show Outside, hide Inside
        cam.cullingMask |= outsideBit;
        cam.cullingMask &= ~insideBit;

        state.currentArea = PlayerAreaState.AreaState.Maze;
    }
}
