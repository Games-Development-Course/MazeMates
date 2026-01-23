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
    [SerializeField] private float hintReminderIntervalSeconds = 15f;   // כל כמה זמן לנסות להדליק שוב
    [SerializeField] private float hintSpotlightPulseSeconds = 6f;      // כמה זמן הזרקור דולק כל פעם
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

        // ✅ נוכחות על הפד (תמיד)
        SetNavigatorPadPresenceTargetClientRpc(true, MakeAllNonServerClientsTargetParams());

        // ✅ אם הדלת עדיין לא פתוחה – זרקור "פתח דלת" זמין (לפני שהתחילה חידה)
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

        // ✅ כיבוי נוכחות + הכל
        SetNavigatorPadPresenceTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
        SetNavigatorOpenDoorAvailableTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
        SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());

        StopHintLoop_Server();

        // סגירת פאזל אם צריך
        if (controller != null && !controller.IsOpen())
        {
            var puzzle = controller.GetPuzzle();
            if (puzzle != null)
                ForceClosePuzzleClientRpc();
        }
    }

    // -------------------------------------------------------
    // Called by DoorController when PUZZLE actually starts
    // -------------------------------------------------------
    public void NotifyPuzzleStarted_Server()
    {
        if (!IsServer) return;

        EnsureController();
        if (controller == null) return;

        // ✅ רק מסתירים "פתח דלת", אבל המטייל עדיין על הפד!
        SetNavigatorOpenDoorAvailableTargetClientRpc(false, MakeAllNonServerClientsTargetParams());

        // ✅ מתחילים לולאת רמזים
        StartHintLoop_Server();
    }

    // -------------------------------------------------------
    // Called by DoorController when a normal/exit door opens
    // -------------------------------------------------------
    public void NotifyDoorActionStartedOrOpened_Server()
    {
        if (!IsServer) return;

        // ✅ מסתירים "פתח דלת"
        SetNavigatorOpenDoorAvailableTargetClientRpc(false, MakeAllNonServerClientsTargetParams());

        // ✅ אם היה לנו רמזים רצים – מפסיקים
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

        // ✅ הפולס הראשון יהיה עוד X שניות מרגע תחילת החידה
        nextPulseTimeServer = Time.time + hintReminderIntervalSeconds;

        hintLoopCo = StartCoroutine(HintLoop_Server());
    }


    private void StopHintLoop_Server()
    {
        puzzleActive = false;

        // ✅ אם היינו באמצע Pulse ארוך — לבטל אותו
        cancelCurrentPulse = true;

        if (hintLoopCo != null)
        {
            StopCoroutine(hintLoopCo);
            hintLoopCo = null;
        }

        // ניקוי: לכבות זרקור רמז
        SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
    }


    private IEnumerator HintLoop_Server()
    {
        while (puzzleActive)
        {
            // מחכים עד הזמן הבא
            while (puzzleActive && Time.time < nextPulseTimeServer)
                yield return null;

            if (!puzzleActive) yield break;

            // קובעים כבר עכשיו את הפולס הבא
            nextPulseTimeServer = Time.time + hintReminderIntervalSeconds;

            // תנאים
            if (!playerOnPadNet.Value) continue;

            EnsureController();
            if (controller == null) continue;

            if (controller.IsOpen()) continue;

            var gm = GameManager.Instance;
            if (gm == null || gm.lifebuoys <= 0) continue;

            // ✅ פולס ON ואז OFF (עם אפשרות ביטול אם השתמשו ברמז באמצע)
            cancelCurrentPulse = false;

            SetNavigatorHintSpotlightTargetClientRpc(true, MakeAllNonServerClientsTargetParams());

            float endTime = Time.time + hintSpotlightPulseSeconds;
            while (puzzleActive && !cancelCurrentPulse && Time.time < endTime)
                yield return null;

            SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
        }
    }

    // ✅ לקרוא לזה כשהנווט השתמש ברמז
    public void NotifyHintUsed_Server()
    {
        if (!IsServer) return;
        if (!puzzleActive) return;

        // ✅ אם יש Pulse פעיל כרגע — לבטל את ההמתנה הארוכה שלו
        cancelCurrentPulse = true;

        // כבה מיד
        SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());

        // ✅ תדליק שוב עוד X שניות *מהשימוש*, לא מהפולס הקודם
        nextPulseTimeServer = Time.time + hintReminderIntervalSeconds;
    }



    // -------------------------------------------------------
    // Traveller message
    // -------------------------------------------------------
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
            SendTravellerMessageTargetClientRpc(msg, MakeTargetParams(NetworkManager.ServerClientId));
    }

    private bool IsTravellerPlayerCollider(Collider other)
    {
        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null || !no.IsSpawned || !no.IsPlayerObject)
            return false;

        // אצלכם traveller הוא ה-host
        return no.OwnerClientId == NetworkManager.ServerClientId;
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

    // ✅ NEW: נוכחות על הפד
    [ClientRpc]
    private void SetNavigatorPadPresenceTargetClientRpc(bool onPad, ClientRpcParams rpcParams = default)
    {
        NavigatorSpotlights.I?.SetTravellerOnPad(onPad);
    }

    // ✅ NEW: זמינות זרקור "פתח דלת"
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
