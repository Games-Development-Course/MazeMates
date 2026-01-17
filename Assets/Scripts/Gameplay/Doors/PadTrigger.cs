// File: Assets/Scripts/Gameplay/Door/PadTrigger.cs (או איפה שהקובץ שלך יושב)
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PadTrigger : NetworkBehaviour
{
    private DoorController controller;

    // authoritative state on server, readable by everyone
    private readonly NetworkVariable<bool> playerOnPadNet = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        controller = GetComponentInParent<DoorController>();
        Debug.Log($"[PadTrigger][Awake] controller={(controller != null ? controller.name : "NULL")}");
    }

    private void EnsureController()
    {
        if (controller == null)
            controller = GetComponentInParent<DoorController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // IMPORTANT: only server sets state
        if (!IsServer)
            return;

        if (!other.CompareTag("Player"))
            return;

        // We only care if the TRAVELLER is on pad (host player object).
        if (!IsTravellerPlayerCollider(other))
            return;

        EnsureController();
        playerOnPadNet.Value = true;

        Debug.Log(
            $"[PadTrigger][Server] Traveller ENTER pad | door={(controller != null ? controller.name : "NULL")} " +
            $"isOpen={(controller != null && controller.IsOpen())}"
        );

        // tell traveller what to do (space prompt) - ONLY if door isn't open
        if (controller != null && controller.IsOpen())
            return;

        // Only show messages if you allow space activation
        if (!CanActivateDoorWithSpace())
            return;

        ShowTravellerPadMessageServer();
    }

    private void OnTriggerExit(Collider other)
    {
        // IMPORTANT: only server sets state
        if (!IsServer)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (!IsTravellerPlayerCollider(other))
            return;

        EnsureController();
        playerOnPadNet.Value = false;

        Debug.Log($"[PadTrigger][Server] Traveller EXIT pad | door={(controller != null ? controller.name : "NULL")}");

        // If leaving while puzzle open -> force close (server decides, but close is local puzzle UI)
        // We'll keep your previous behavior but only execute it on everyone so traveller closes it.
        if (controller != null && !controller.IsOpen())
        {
            var puzzle = controller.GetPuzzle();
            if (puzzle != null)
                ForceClosePuzzleClientRpc();
        }
    }

    // Called by server to instruct traveller UI
    private void ShowTravellerPadMessageServer()
    {
        if (controller == null)
            return;

        var gm = GameManager.Instance;
        string msg = null;

        switch (controller.doorType)
        {
            case DoorType.Normal:
                if (!controller.IsOpen())
                {
                    msg = "בקש מהנווט לפתוח את הדלת";
                    break;
                }
                break;

            case DoorType.Puzzle:
                if (!controller.IsOpen())
                {
                    msg = "בקש מהנווט להתחיל את החידה";
                    break;
                }
                break;

            case DoorType.Exit:
                if (gm != null && gm.AllKeysCollected())
                    msg = "יש לך את כל המפתחות!";
                else
                    msg = "עליך לאסוף את כל המפתחות";
                break;
        }

        if (!string.IsNullOrEmpty(msg))
            SendTravellerMessageTargetClientRpc(msg, MakeTargetParams(NetworkManager.ServerClientId));
    }

    private bool IsTravellerPlayerCollider(Collider other)
    {
        // traveller is always host (ServerClientId)
        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null || !no.IsSpawned || !no.IsPlayerObject)
            return false;

        return no.OwnerClientId == NetworkManager.ServerClientId;
    }

    public bool IsPlayerOnPad()
    {
        // Everyone can read the authoritative value
        return playerOnPadNet.Value;
    }

    public bool CanActivateDoorWithSpace()
    {
        if (DoorPadToggle.Instance == null)
            return true;

        return DoorPadToggle.Instance.allowSpaceActivation;
    }

    private static ClientRpcParams MakeTargetParams(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
    }

    [ClientRpc]
    private void SendTravellerMessageTargetClientRpc(string msg, ClientRpcParams rpcParams = default)
    {
        // Runs only on the targeted client
        HUDManager.Instance?.ShowMessageForTraveller(msg);
    }

    [ClientRpc]
    private void ForceClosePuzzleClientRpc()
    {
        if (controller == null)
            controller = GetComponentInParent<DoorController>();

        var puzzle = controller != null ? controller.GetPuzzle() : null;
        if (puzzle != null)
            puzzle.ForceClosePuzzle();
    }
}
