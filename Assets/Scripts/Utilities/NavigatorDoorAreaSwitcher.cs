// File: Assets/Scripts/NavigatorDoorAreaSwitcher.cs
using Unity.Netcode;
using UnityEngine;

public class NavigatorDoorAreaSwitcher : MonoBehaviour
{
    [Header("Layer names")]
    [SerializeField] private string outsideLayer = "NavigatorOutside";
    [SerializeField] private string insideLayer = "NavigatorInside";

    [Header("Win UI (Scene objects)")]
    [SerializeField] private GameObject travellerWinDecore; // Canvas->UI->TravellerHUD->WinDecore
    [SerializeField] private GameObject navigatorWinDecore; // Canvas->UI->NavigatorHUD->WinDecore
    [SerializeField] private bool playWinAudio = true;

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

        // Only affect local player's UI
        var no = other.GetComponent<NetworkObject>();
        if (no != null && !no.IsOwner)
            return;

        var state = other.GetComponent<PlayerAreaState>();
        if (!state) return;

        var cam = Camera.main;
        if (!cam)
        {
            Debug.LogWarning("No Camera.main found. Ensure Main Camera has tag 'MainCamera'.");
            return;
        }

        if (state.currentArea == PlayerAreaState.AreaState.Maze)
        {
            // Enter NavigatorRoom
            cam.cullingMask |= insideBit;
            cam.cullingMask &= ~outsideBit;
            state.currentArea = PlayerAreaState.AreaState.NavigatorRoom;

            ActivateWinDecore(travellerWinDecore);
            ActivateWinDecore(navigatorWinDecore);
        }
        else
        {
            // Exit NavigatorRoom
            cam.cullingMask |= outsideBit;
            cam.cullingMask &= ~insideBit;
            state.currentArea = PlayerAreaState.AreaState.Maze;
        }
    }

    private void ActivateWinDecore(GameObject winDecore)
    {
        if (winDecore == null)
        {
            Debug.LogWarning("[WinDecore] Reference not set in Inspector.");
            return;
        }

        winDecore.SetActive(true);

        if (!playWinAudio) return;

        var audio = winDecore.GetComponent<AudioSource>() ?? winDecore.GetComponentInChildren<AudioSource>(true);
        if (audio != null)
        {
            audio.Stop();
            audio.Play();
        }
    }
}
