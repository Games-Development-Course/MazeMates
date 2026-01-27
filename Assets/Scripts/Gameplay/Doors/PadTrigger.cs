// Assets/Scripts/Gameplay/Door/PadTrigger.cs
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PadTrigger : NetworkBehaviour
{
    private DoorController controller;

    [Header("Hint spotlight reminder (Puzzle only)")]
    [SerializeField] private float hintReminderIntervalSeconds = 15f;
    [SerializeField] private float hintSpotlightPulseSeconds = 6f;
    private float nextPulseTimeServer = -1f;

    private Coroutine hintLoopCo;
    private bool puzzleActive;
    private bool cancelCurrentPulse;

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
        if (!IsServer) return;
        if (!other.CompareTag("Player")) return;
        if (!IsTravellerPlayerCollider(other)) return;

        EnsureController();
        playerOnPadNet.Value = true;

        if (controller == null) return;

        Debug.Log($"[PadTrigger][Server] Traveller ENTER pad | door={controller.name} isOpen={controller.IsOpen()}");

        SetNavigatorPadPresenceTargetClientRpc(true, MakeAllNonServerClientsTargetParams());

        if (!controller.IsOpen())
            SetNavigatorOpenDoorAvailableTargetClientRpc(true, MakeAllNonServerClientsTargetParams());

        if (!CanActivateDoorWithSpace())
            return;

        ShowTravellerPadMessageServer();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Player")) return;
        if (!IsTravellerPlayerCollider(other)) return;

        EnsureController();
        playerOnPadNet.Value = false;

        Debug.Log($"[PadTrigger][Server] Traveller EXIT pad | door={(controller != null ? controller.name : "NULL")}");

        SetNavigatorPadPresenceTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
        SetNavigatorOpenDoorAvailableTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
        SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());

        StopHintLoop_Server();

        if (controller != null && !controller.IsOpen())
        {
            var puzzle = controller.GetPuzzle();
            if (puzzle != null)
                ForceClosePuzzleClientRpc();
        }
    }

    public void NotifyPuzzleStarted_Server()
    {
        if (!IsServer) return;

        EnsureController();
        if (controller == null) return;

        SetNavigatorOpenDoorAvailableTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
        StartHintLoop_Server();
    }

    public void NotifyDoorActionStartedOrOpened_Server()
    {
        if (!IsServer) return;

        SetNavigatorOpenDoorAvailableTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
        StopHintLoop_Server();
    }

    private void StartHintLoop_Server()
    {
        if (!IsServer) return;

        EnsureController();
        if (controller == null) return;
        if (controller.doorType != DoorType.Puzzle) return;

        StopHintLoop_Server();

        puzzleActive = true;
        nextPulseTimeServer = Time.time + hintReminderIntervalSeconds;

        hintLoopCo = StartCoroutine(HintLoop_Server());
    }

    private void StopHintLoop_Server()
    {
        puzzleActive = false;
        cancelCurrentPulse = true;

        if (hintLoopCo != null)
        {
            StopCoroutine(hintLoopCo);
            hintLoopCo = null;
        }

        SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
    }

    private IEnumerator HintLoop_Server()
    {
        while (puzzleActive)
        {
            while (puzzleActive && Time.time < nextPulseTimeServer)
                yield return null;

            if (!puzzleActive) yield break;

            nextPulseTimeServer = Time.time + hintReminderIntervalSeconds;

            if (!playerOnPadNet.Value) continue;

            EnsureController();
            if (controller == null) continue;
            if (controller.IsOpen()) continue;

            var gm = GameManager.Instance;
            if (gm == null || gm.lifebuoys <= 0) continue;

            cancelCurrentPulse = false;

            SetNavigatorHintSpotlightTargetClientRpc(true, MakeAllNonServerClientsTargetParams());

            float endTime = Time.time + hintSpotlightPulseSeconds;
            while (puzzleActive && !cancelCurrentPulse && Time.time < endTime)
                yield return null;

            SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
        }
    }

    public void NotifyHintUsed_Server()
    {
        if (!IsServer) return;
        if (!puzzleActive) return;

        cancelCurrentPulse = true;
        SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
        nextPulseTimeServer = Time.time + hintReminderIntervalSeconds;
    }

    private void ShowTravellerPadMessageServer()
    {
        if (controller == null) return;

        var gm = GameManager.Instance;
        string msg = null;

        switch (controller.doorType)
        {
            case DoorType.Normal:
                if (!controller.IsOpen())
                    msg = "בקש מחברך ללחוץ על 'פתח דלת'";
                break;

            case DoorType.Puzzle:
                if (!controller.IsOpen())
                    msg = "בקש מחברך ללחוץ על 'פתח דלת' \nכדי להתחיל את החידה";
                break;

            case DoorType.Exit:
                if (gm != null && gm.AllKeysCollected())
                    msg = "יש לך את כל המפתחות! בקש מחברך ללחוץ על 'פתח דלת' \nכדי לצאת";
                else
                    msg = "עליך לאסוף את כל המפתחות כדי לצאת";
                break;
        }

        if (!string.IsNullOrEmpty(msg))
            SendTravellerMessageTargetClientRpc(msg, MakeTargetParams(GetTravellerClientIdSafe()));
    }

    // ✅ NEW: Traveller detection via GameManager.traveller NetworkObjectId (host-independent)
    private bool IsTravellerPlayerCollider(Collider other)
    {
        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null || !no.IsSpawned || !no.IsPlayerObject)
            return false;

        var gm = GameManager.Instance;
        if (gm != null && gm.traveller != null)
        {
            var tNo = gm.traveller.GetComponent<NetworkObject>();
            if (tNo != null && tNo.IsSpawned)
                return tNo.NetworkObjectId == no.NetworkObjectId;
        }

        // fallback
        return no.OwnerClientId == NetworkManager.ServerClientId;
    }

    private ulong GetTravellerClientIdSafe()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.traveller != null)
        {
            var tNo = gm.traveller.GetComponent<NetworkObject>();
            if (tNo != null && tNo.IsSpawned)
                return tNo.OwnerClientId;
        }
        return NetworkManager.ServerClientId;
    }

    public bool IsPlayerOnPad() => playerOnPadNet.Value;

    public bool CanActivateDoorWithSpace()
    {
        if (DoorPadToggle.Instance == null) return true;
        return DoorPadToggle.Instance.allowSpaceActivation;
    }

    private static ClientRpcParams MakeTargetParams(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
    }

    private static ClientRpcParams MakeAllNonServerClientsTargetParams()
    {
        var nm = NetworkManager.Singleton;
        var ids = nm.ConnectedClientsIds;

        var list = new List<ulong>(ids.Count);
        foreach (var id in ids)
            if (id != NetworkManager.ServerClientId)
                list.Add(id);

        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = list.ToArray() }
        };
    }

    [ClientRpc]
    private void SendTravellerMessageTargetClientRpc(string msg, ClientRpcParams rpcParams = default)
    {
        HUDManager.Instance?.ShowMessageForTraveller(msg);
    }

    [ClientRpc]
    private void SetNavigatorPadPresenceTargetClientRpc(bool onPad, ClientRpcParams rpcParams = default)
    {
        NavigatorSpotlights.I?.SetTravellerOnPad(onPad);
    }

    [ClientRpc]
    private void SetNavigatorOpenDoorAvailableTargetClientRpc(bool available, ClientRpcParams rpcParams = default)
    {
        NavigatorSpotlights.I?.SetOpenDoorAvailable(available);
    }

    [ClientRpc]
    private void SetNavigatorHintSpotlightTargetClientRpc(bool on, ClientRpcParams rpcParams = default)
    {
        NavigatorSpotlights.I?.SetHintReady(on);
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
