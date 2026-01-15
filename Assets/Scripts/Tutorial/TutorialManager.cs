// ======================= Assets/Scripts/Tutorial/TutorialManager.cs =======================
// FIX (full file): Navigator HUD not showing.
// What this version changes vs your previous:
// 1) Role detection uses LocalClientId == NetworkManager.ServerClientId (NOT Owner of TutorialManager).
// 2) Locks/Camera locks are applied to the LOCAL owned PlayerMovement1P / PlayerCamera1P on each client.
// 3) HUD is updated via one RPC, and **each RPC force-resolves the local HUD at call-time**
//    (so if the RPC arrives before HUD objects are ready, it will still find them).
//
// No “architecture rebuild” (still NetworkObject + RPC sync).

using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : NetworkBehaviour
{
    private Vector3 travellerStartPos;
    private Quaternion travellerStartRot;
    private bool travellerStartCaptured;

    [Header("Config")]
    public TutorialStep[] steps;

    [Header("HUD")]
    public TutorialHUD travellerHUD;
    public TutorialHUD navigatorHUD;

    [Header("Scene References")]
    [SerializeField] private Transform travellerRoot;
    [SerializeField] private Camera travellerCamera;

    [Header("Waiting Messages")]
    [TextArea] public string travellerWaitingForNavigator = "ממתין לנווט…";
    [TextArea] public string navigatorWaitingForTraveller = "ממתין למטייל…";

    public float defaultAutoStepDelay = 0.15f;

    public NetworkVariable<bool> TutorialActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private int currentIndex = -1;
    private bool stepActive;
    private bool conditionSatisfied;

    private float stepStartTime;
    private float hudShownTime;

    private bool travellerMoved, navigatorMoved;
    private bool travellerLooked, navigatorLooked;

    private bool tutorialStarted = false;

    private TutorialStep Current =>
        (steps != null && currentIndex >= 0 && currentIndex < steps.Length) ? steps[currentIndex] : null;

    public static List<TutorialColliderAuto> autoColliders = new List<TutorialColliderAuto>();

    public static void RegisterAutoCollider(TutorialColliderAuto c)
    {
        if (c != null && !autoColliders.Contains(c))
            autoColliders.Add(c);
    }

    // ============================================================
    // ROLE HELPERS (NO Owner gating)
    // ============================================================

    private bool IsLocalTraveller()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;

        // ServerClientId is static in NGO
        return nm.LocalClientId == NetworkManager.ServerClientId;
    }

    private PlayerMovement FindLocalOwnedMovement()
    {
        foreach (var m in Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (m != null && m.IsOwner) return m;
        }
        return null;
    }

    private PlayerCamera1P FindLocalOwnedCamera()
    {
        foreach (var c in Object.FindObjectsByType<PlayerCamera1P>(FindObjectsSortMode.None))
        {
            if (c != null && c.IsOwner) return c;
        }
        return null;
    }

    // ============================================================
    // HUD RESOLVE (CRITICAL FIX)
    // ============================================================

    private void EnsureLocalHUDResolved()
    {
        bool iAmTraveller = IsLocalTraveller();

        // If already resolved for this side, done
        if (iAmTraveller)
        {
            if (travellerHUD != null) return;
        }
        else
        {
            if (navigatorHUD != null) return;
        }

        var huds = Object.FindObjectsOfType<TutorialHUD>(true);

        // Try name-based first
        foreach (var h in huds)
        {
            if (h == null) continue;
            var n = h.gameObject.name.ToLower();

            if (travellerHUD == null && (n.Contains("traveller") || n.Contains("מטייל")))
                travellerHUD = h;

            if (navigatorHUD == null && (n.Contains("navigator") || n.Contains("נווט")))
                navigatorHUD = h;
        }

        // Fallback: assign something local so message appears at least somewhere
        if (iAmTraveller)
        {
            if (travellerHUD == null && huds.Length > 0)
                travellerHUD = huds[0];
        }
        else
        {
            if (navigatorHUD == null && huds.Length > 0)
                navigatorHUD = huds[0];
        }

        Debug.Log(
            $"[TM][HUD] EnsureLocalHUDResolved | side={(iAmTraveller ? "HOST/Traveller" : "CLIENT/Navigator")} " +
            $"travHUD={(travellerHUD != null ? travellerHUD.gameObject.name : "NULL")} " +
            $"navHUD={(navigatorHUD != null ? navigatorHUD.gameObject.name : "NULL")} " +
            $"found={huds.Length}"
        );
    }

    // ============================================================
    // NETWORK SPAWN
    // ============================================================

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            $"[TM] OnNetworkSpawn | scene={SceneManager.GetActiveScene().name} " +
            $"IsServer={IsServer} IsClient={IsClient} IsHost={IsHost} IsOwner={IsOwner} " +
            $"LocalClientId={(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId.ToString() : "NULL_NM")} " +
            $"ServerClientId={NetworkManager.ServerClientId} ObjId={NetworkObjectId} " +
            $"TutorialActive={TutorialActive.Value} currentIndex={currentIndex} steps={(steps != null ? steps.Length : 0)}"
        );

        StartCoroutine(ResolveLocalRefsNextFrame());

        if (IsServer && !tutorialStarted)
        {
            tutorialStarted = true;
            Debug.Log("[TM] Server -> StartTutorialAfterSceneSettles()");
            StartCoroutine(StartTutorialAfterSceneSettles());
        }

        if (IsClient)
        {
            Debug.Log("[TM] Client -> RequestTutorialSyncServerRpc()");
            RequestTutorialSyncServerRpc();
        }
    }

    private IEnumerator ResolveLocalRefsNextFrame()
    {
        yield return null;
        EnsureLocalHUDResolved();
    }

    private IEnumerator StartTutorialAfterSceneSettles()
    {
        yield return null;
        yield return new WaitForSeconds(0.25f);
        StartTutorial();
    }

    // ============================================================
    // CLIENT <-> SERVER SYNC
    // ============================================================

    [ServerRpc(RequireOwnership = false)]
    private void RequestTutorialSyncServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong targetClientId = rpcParams.Receive.SenderClientId;

        Debug.Log(
            $"[TM][ServerRpc] RequestTutorialSyncServerRpc from clientId={targetClientId} | " +
            $"TutorialActive={TutorialActive.Value} currentIndex={currentIndex} steps={(steps != null ? steps.Length : 0)}"
        );

        if (!TutorialActive.Value || currentIndex < 0 || steps == null || currentIndex >= steps.Length)
        {
            Debug.Log("[TM][ServerRpc] Snapshot NOT sent (tutorial not running yet / invalid index)");
            return;
        }

        SendSnapshotToClient(targetClientId, steps[currentIndex]);
    }

    private void SendSnapshotToClient(ulong clientId, TutorialStep step)
    {
        Debug.Log($"[TM] SendSnapshotToClient | clientId={clientId} stepId={step.stepId}");

        SendLocksToSpecificClient(clientId, step);

        if (step.applyMouseSettingsOnStepStart)
            SendMouseToSpecificClient(clientId, step);

        ShowStepHUDToSpecificClient(clientId, step);

        StepStartedCollidersTargetClientRpc(step.stepId, MakeTargetParams(clientId));
    }

    private static ClientRpcParams MakeTargetParams(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
        };
    }

    // ============================================================
    // CONNECTED CALLBACK
    // ============================================================

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        if (!TutorialActive.Value) return;
        if (currentIndex < 0 || steps == null || currentIndex >= steps.Length) return;

        Debug.Log($"[TM][Server] OnClientConnected -> snapshot to clientId={clientId} stepId={steps[currentIndex].stepId}");
        SendSnapshotToClient(clientId, steps[currentIndex]);
    }

    // ============================================================
    // START TUTORIAL
    // ============================================================

    public void StartTutorial()
    {
        if (!IsServer) return;

        currentIndex = -1;
        TutorialActive.Value = true;
        NextStep();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!stepActive) return;
        if (currentIndex < 0 || steps == null || currentIndex >= steps.Length) return;

        TutorialStep step = Current;
        if (step == null) return;

        float elapsedHUD = Time.time - hudShownTime;

        if (!step.completeOnCondition && step.autoCompleteAfter > 0 &&
            Time.time - stepStartTime >= step.autoCompleteAfter)
        {
            CompleteCurrentStep();
            return;
        }

        if (step.completeOnCondition && conditionSatisfied)
        {
            if (elapsedHUD < step.minHUDDuration) return;
            CompleteCurrentStep();
        }
    }

    // ============================================================
    // STEP FLOW
    // ============================================================

    private void NextStep()
    {
        if (!IsServer) return;

        currentIndex++;

        if (steps == null || currentIndex >= steps.Length)
        {
            Debug.Log("[TUTORIAL] Finished all steps.");
            stepActive = false;
            TutorialActive.Value = false;
            return;
        }

        Debug.Log($"[TUTORIAL] Next step index={currentIndex} stepId={Current.stepId} cond={Current.conditionType}");

        stepActive = true;
        conditionSatisfied = false;

        stepStartTime = Time.time;
        hudShownTime = Time.time;

        travellerMoved = navigatorMoved = false;
        travellerLooked = navigatorLooked = false;

        StartCoroutine(DelayedColliderStep(Current.stepId, 0.2f));

        ApplyLocks(Current);
        ApplyMouse(Current);
        ShowStepHUD(Current);

        AlignTravellerForStep(Current);

        Current.onStepStart?.Invoke();
    }

    private IEnumerator DelayedColliderStep(string id, float delay)
    {
        yield return new WaitForSeconds(delay);
        StepStartedCollidersClientRpc(id);
    }

    [ClientRpc]
    private void StepStartedCollidersClientRpc(string stepId)
    {
        Debug.Log($"[TM][RPC] StepStartedCollidersClientRpc | side={(IsLocalTraveller() ? "HOST/Traveller" : "CLIENT/Navigator")} stepId={stepId} autoColliders={autoColliders.Count}");
        foreach (var c in autoColliders)
        {
            if (c != null)
                c.OnStepStarted(stepId);
        }
    }

    [ClientRpc]
    private void StepStartedCollidersTargetClientRpc(string stepId, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[TM][RPC] StepStartedCollidersTargetClientRpc | side={(IsLocalTraveller() ? "HOST/Traveller" : "CLIENT/Navigator")} stepId={stepId} autoColliders={autoColliders.Count}");
        foreach (var c in autoColliders)
        {
            if (c != null)
                c.OnStepStarted(stepId);
        }
    }

    // ============================================================
    // LOCKS (apply to LOCAL owned components)
    // ============================================================

    private void ApplyLocks(TutorialStep s)
    {
        ApplyLocksClientRpc(
            s.travellerLockMovement,
            s.travellerLockCamera,
            s.navigatorLockMovement,
            s.navigatorLockCamera
        );
    }

    private void SendLocksToSpecificClient(ulong clientId, TutorialStep step)
    {
        var p = MakeTargetParams(clientId);

        ApplyLocksClientRpc(
            step.travellerLockMovement,
            step.travellerLockCamera,
            step.navigatorLockMovement,
            step.navigatorLockCamera,
            p
        );
    }

    [ClientRpc]
    private void ApplyLocksClientRpc(
        bool travellerLockMovement,
        bool travellerLockCamera,
        bool navigatorLockMovement,
        bool navigatorLockCamera,
        ClientRpcParams rpcParams = default
    )
    {
        bool iAmTraveller = IsLocalTraveller();

        bool lockMove = iAmTraveller ? travellerLockMovement : navigatorLockMovement;
        bool lockCam = iAmTraveller ? travellerLockCamera : navigatorLockCamera;

        var myMove = FindLocalOwnedMovement();
        if (myMove != null)
            myMove.SetFrozen(lockMove);
        else
            Debug.LogWarning("[TM][RPC] ApplyLocks -> local Movement NOT FOUND (no IsOwner movement)");

        var myCam = FindLocalOwnedCamera();
        if (myCam != null)
            myCam.SetCameraFrozen(lockCam);
        else
            Debug.LogWarning("[TM][RPC] ApplyLocks -> local Camera NOT FOUND (no IsOwner camera)");
    }

    // ============================================================
    // MOUSE / CURSOR (apply locally)
    // ============================================================

    private void ApplyMouse(TutorialStep s)
    {
        if (!s.applyMouseSettingsOnStepStart) return;

        ApplyMouseClientRpc(
            s.travellerCursorVisible,
            (int)s.travellerCursorLockMode,
            s.navigatorCursorVisible,
            (int)s.navigatorCursorLockMode
        );
    }

    private void SendMouseToSpecificClient(ulong clientId, TutorialStep step)
    {
        var p = MakeTargetParams(clientId);

        ApplyMouseClientRpc(
            step.travellerCursorVisible,
            (int)step.travellerCursorLockMode,
            step.navigatorCursorVisible,
            (int)step.navigatorCursorLockMode,
            p
        );
    }

    [ClientRpc]
    private void ApplyMouseClientRpc(
        bool travellerCursorVisible,
        int travellerCursorLockMode,
        bool navigatorCursorVisible,
        int navigatorCursorLockMode,
        ClientRpcParams rpcParams = default
    )
    {
        bool iAmTraveller = IsLocalTraveller();

        if (iAmTraveller)
        {
            Cursor.visible = travellerCursorVisible;
            Cursor.lockState = (CursorLockMode)travellerCursorLockMode;
        }
        else
        {
            Cursor.visible = navigatorCursorVisible;
            Cursor.lockState = (CursorLockMode)navigatorCursorLockMode;
        }
    }

    // ============================================================
    // HUD (single RPC, force-resolve HUD on arrival)
    // ============================================================

    private void ShowStepHUD(TutorialStep step)
    {
        SetHudMessageClientRpc(step.travellerMessage, step.navigatorMessage);
    }

    private void ShowStepHUDToSpecificClient(ulong clientId, TutorialStep step)
    {
        var p = MakeTargetParams(clientId);
        SetHudMessageTargetClientRpc(step.travellerMessage, step.navigatorMessage, p);
    }

    [ClientRpc]
    private void SetHudMessageClientRpc(string travellerMsg, string navigatorMsg)
    {
        EnsureLocalHUDResolved();

        bool iAmTraveller = IsLocalTraveller();

        if (iAmTraveller)
        {
            if (travellerHUD == null)
            {
                Debug.LogWarning("[TM][HUD] travellerHUD is NULL on host (after resolve)");
                return;
            }
            travellerHUD.ShowMessage(travellerMsg);
        }
        else
        {
            if (navigatorHUD == null)
            {
                Debug.LogWarning("[TM][HUD] navigatorHUD is NULL on client (after resolve)");
                return;
            }
            navigatorHUD.ShowMessage(navigatorMsg);
        }
    }

    [ClientRpc]
    private void SetHudMessageTargetClientRpc(string travellerMsg, string navigatorMsg, ClientRpcParams rpcParams = default)
    {
        EnsureLocalHUDResolved();

        bool iAmTraveller = IsLocalTraveller();

        if (iAmTraveller)
        {
            if (travellerHUD == null)
            {
                Debug.LogWarning("[TM][HUD] travellerHUD is NULL on host (after resolve)");
                return;
            }
            travellerHUD.ShowMessage(travellerMsg);
        }
        else
        {
            if (navigatorHUD == null)
            {
                Debug.LogWarning("[TM][HUD] navigatorHUD is NULL on client (after resolve)");
                return;
            }
            navigatorHUD.ShowMessage(navigatorMsg);
        }
    }

    // ============================================================
    // COMPLETE STEP
    // ============================================================

    private void MarkConditionSatisfiedInternal()
    {
        if (Current == null) return;
        Debug.Log($"[TUTORIAL] Condition satisfied for step '{Current.stepId}'");
        conditionSatisfied = true;
    }

    private void CompleteCurrentStep()
    {
        Debug.Log($"[TUTORIAL] Step COMPLETE {(Current != null ? Current.stepId : "NULL")}");

        if (!IsServer || !stepActive)
            return;

        stepActive = false;

        if (Current != null)
            Current.onStepComplete?.Invoke();

        StartCoroutine(GoNext());
    }

    private IEnumerator GoNext()
    {
        yield return new WaitForSeconds(defaultAutoStepDelay);
        NextStep();
    }

    // ============================================================
    // MOVEMENT + LOOK  (server authoritative)
    // ============================================================

    public void NotifyTravellerMoved() => HandleMovement(ref travellerMoved, true);
    public void NotifyNavigatorMoved() => HandleMovement(ref navigatorMoved, false);

    private void HandleMovement(ref bool flag, bool isTraveller)
    {
        if (!IsServer || !stepActive) return;

        flag = true;
        var step = Current;
        if (step == null) return;

        if (step.conditionType == TutorialConditionType.TravellerMoved && isTraveller)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.NavigatorMoved && !isTraveller)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.BothMoved && travellerMoved && navigatorMoved)
            MarkConditionSatisfiedInternal();
    }

    public void NotifyTravellerLooked() => HandleLook(ref travellerLooked, true);
    public void NotifyNavigatorLooked() => HandleLook(ref navigatorLooked, false);

    private void HandleLook(ref bool flag, bool isTraveller)
    {
        if (!IsServer || !stepActive) return;

        flag = true;
        var step = Current;
        if (step == null) return;

        if (step.conditionType == TutorialConditionType.TravellerLookedAround && isTraveller)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.NavigatorLookedAround && !isTraveller)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.BothLookedAround && travellerLooked && navigatorLooked)
            MarkConditionSatisfiedInternal();
    }

    public bool IsTutorialRunningForStep(string stepId)
    {
        if (!TutorialActive.Value) return false;
        if (currentIndex < 0 || steps == null || currentIndex >= steps.Length) return false;
        return steps[currentIndex].stepId == stepId;
    }

    // ============================================================
    // ✅ SYSTEMIC CONDITION DISPATCH
    // ============================================================

    private void NotifyCondition(TutorialConditionType condition)
    {
        if (IsServer) Check(condition);
        else NotifyConditionServerRpc(condition);
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyConditionServerRpc(TutorialConditionType condition)
    {
        Debug.Log($"[TUTORIAL][ServerRpc] received condition: {condition}");
        Check(condition);
    }

    private void Check(TutorialConditionType t)
    {
        Debug.Log(
            $"[TUTORIAL][CHECK] cond={t} step={(currentIndex >= 0 && currentIndex < (steps != null ? steps.Length : 0) ? Current.stepId : "NONE")} " +
            $"expect={(currentIndex >= 0 && currentIndex < (steps != null ? steps.Length : 0) ? Current.conditionType.ToString() : "NONE")} active={stepActive}"
        );

        if (!IsServer) return;
        if (!stepActive) return;
        if (Current == null) return;

        if (Current.conditionType != t)
        {
            Debug.Log("[TUTORIAL][CHECK] Condition mismatch — ignored");
            return;
        }

        Debug.Log($"[TUTORIAL][CHECK] MATCH! Step '{Current.stepId}' satisfied");
        MarkConditionSatisfiedInternal();
    }

    // ============================================================
    // CONDITION EVENTS
    // ============================================================

    public void NotifyNavigatorRemovedBomb() => NotifyCondition(TutorialConditionType.NavigatorRemoveBomb);
    public void NotifyNavigatorOpenedNormalDoor() => NotifyCondition(TutorialConditionType.NavigatorOpenNormalDoor);
    public void NotifyNavigatorOpenedPuzzleDoor() => NotifyCondition(TutorialConditionType.NavigatorOpenPuzzleDoor);

    public void NotifyNavigatorOpenedExitDoor()
    {
        NotifyCondition(TutorialConditionType.NavigatorOpenExitDoor);
        ActivateDiscoMode();
    }

    public void NotifyNavigatorPlacedHeart() => NotifyCondition(TutorialConditionType.NavigatorPlaceHeart);
    public void NotifyNavigatorGaveLifebuoy() => NotifyCondition(TutorialConditionType.NavigatorGiveLifebuoy);
    public void NotifyTravellerPlacedPuzzlePiece() => NotifyCondition(TutorialConditionType.TravellerPlacedPuzzlePiece);
    public void NotifyTravellerPickedKey() => NotifyCondition(TutorialConditionType.TravellerPickedKey);
    public void NotifyTravellerPickedHeart() => NotifyCondition(TutorialConditionType.TravellerPickedHeart);
    public void NotifyPuzzleSolved() => NotifyCondition(TutorialConditionType.PuzzleSolved);
    public void NotifyBothReachedExit() => NotifyCondition(TutorialConditionType.BothReachedExit);
    public void NotifyCustomEvent() => NotifyCondition(TutorialConditionType.CustomEvent);
    public void NotifyTravellerSteppedBomb()
    {
        if (IsServer) HandleTravellerSteppedBomb_Server();
        else TravellerSteppedBombServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void TravellerSteppedBombServerRpc()
    {
        HandleTravellerSteppedBomb_Server();
    }

    private void HandleTravellerSteppedBomb_Server()
    {
        ResetTravellerToStart_Server();

        // עדיין נותנים לטוטוריאל לעבוד רגיל (יסומן רק אם זה ה-step הנוכחי)
        Check(TutorialConditionType.TravellerSteppedBomb);
    }


    // ============================================================
    // TRAVELLER ROTATION / LOOK
    // ============================================================

    private void AlignTravellerForStep(TutorialStep step)
    {
        if (!step.rotateTravellerOnStepStart) return;
        if (string.IsNullOrEmpty(step.travellerLookTargetId)) return;

        RotateTravellerClientRpc(step.travellerLookTargetId);
    }

    [ClientRpc]
    private void RotateTravellerClientRpc(string targetId)
    {
        if (!IsLocalTraveller()) return; // only traveller

        if (travellerRoot == null)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.traveller != null)
                travellerRoot = gm.traveller.transform;
        }

        if (travellerRoot == null)
        {
            Debug.LogWarning("[TUTORIAL] travellerRoot is null – cannot rotate");
            return;
        }

        Transform target = TutorialLookTarget.Get(targetId);
        if (target == null)
        {
            Debug.LogWarning($"[TUTORIAL] No TutorialLookTarget with id '{targetId}'");
            return;
        }

        Vector3 dir = target.position - travellerRoot.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        travellerRoot.rotation = rot;

        if (travellerCamera == null)
        {
            var cam = travellerRoot.GetComponentInChildren<Camera>();
            if (cam != null) travellerCamera = cam;
        }

        if (travellerCamera != null)
        {
            Vector3 euler = travellerCamera.transform.eulerAngles;
            euler.y = rot.eulerAngles.y;
            travellerCamera.transform.eulerAngles = euler;
        }
    }

    public void ActivateDiscoMode()
    {
        var disco = FindObjectOfType<DiscoTime>();
        if (disco != null)
        {
            disco.EnableDisco();
            Debug.Log("🎉 Disco Mode Activated!");
        }
    }

    // ============================================================
    // REGISTER TRAVELLER
    // ============================================================

    public void RegisterTraveller(Transform root)
    {
        travellerRoot = root;

        if (!travellerStartCaptured && travellerRoot != null)
        {
            travellerStartCaptured = true;
            travellerStartPos = travellerRoot.position;
            travellerStartRot = travellerRoot.rotation;
            Debug.Log($"[TM] Captured traveller start at {travellerStartPos}");
        }
    }

    public void RegisterTravellerCamera(Camera cam) => travellerCamera = cam;

    private void ResetTravellerToStart_Server()
{
    if (!IsServer) return;

    var gm = GameManager.Instance;
    if (gm == null || gm.traveller == null) return;

    var travellerNo = gm.traveller.GetComponent<Unity.Netcode.NetworkObject>();
    if (travellerNo == null || !travellerNo.IsSpawned) return;

    var move = gm.traveller.GetComponentInChildren<PlayerMovement>(true);
    if (move == null) return;

    var p = new Unity.Netcode.ClientRpcParams
    {
        Send = new Unity.Netcode.ClientRpcSendParams
        {
            TargetClientIds = new[] { travellerNo.OwnerClientId }
        }
    };

    move.BombResetAndTeleportClientRpc(
        travellerStartPos,
        travellerStartRot,
        0.0f,   // delay
        0.0f,   // red
        0.0f,   // fadeOut
        0.0f,   // fadeIn
        p
    );
}

}
