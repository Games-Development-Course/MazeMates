// ======================= TutorialManager.cs =======================
// FIXED: client sync + targeted HUD/Locks/Mouse + missing functions + robust HUD resolve
// Based on your file. :contentReference[oaicite:0]{index=0}

using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TutorialManager : NetworkBehaviour
{
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

    private TutorialStep Current => (steps != null && currentIndex >= 0 && currentIndex < steps.Length) ? steps[currentIndex] : null;

    public static List<TutorialColliderAuto> autoColliders = new List<TutorialColliderAuto>();

    public static void RegisterAutoCollider(TutorialColliderAuto c)
    {
        if (c != null && !autoColliders.Contains(c))
            autoColliders.Add(c);
    }

    // ============================================================
    // NETWORK SPAWN
    // ============================================================

    public override void OnNetworkSpawn()
    {
        // Always resolve local refs on BOTH host and client
        StartCoroutine(ResolveLocalRefsNextFrame());

        // Server starts tutorial once (you wanted it active when scene starts)
        if (IsServer && !tutorialStarted)
        {
            tutorialStarted = true;
            StartCoroutine(StartTutorialAfterSceneSettles());
        }

        // Every client requests a sync snapshot (so late join / missed RPC won't break)
        if (IsClient)
            RequestTutorialSyncServerRpc();
    }

    private IEnumerator ResolveLocalRefsNextFrame()
    {
        yield return null; // allow scene objects to be ready

        if (travellerHUD == null || navigatorHUD == null)
        {
            var huds = Object.FindObjectsOfType<TutorialHUD>(true);

            // Try name-based first
            foreach (var h in huds)
            {
                if (h == null) continue;
                var n = h.gameObject.name.ToLower();
                if (travellerHUD == null && (n.Contains("traveller") || n.Contains("מטייל"))) travellerHUD = h;
                if (navigatorHUD == null && (n.Contains("navigator") || n.Contains("נווט"))) navigatorHUD = h;
            }

            // Fallback: if still null, take "any" HUD for the local side so UI at least works
            if (IsHost && travellerHUD == null && huds.Length > 0) travellerHUD = huds[0];
            if (!IsHost && navigatorHUD == null && huds.Length > 0) navigatorHUD = huds[0];
        }
    }

    private IEnumerator StartTutorialAfterSceneSettles()
    {
        // Let NGO finish spawning objects on clients
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

        // If tutorial not running yet, nothing to sync
        if (!TutorialActive.Value || currentIndex < 0 || steps == null || currentIndex >= steps.Length)
            return;

        SendSnapshotToClient(targetClientId, steps[currentIndex]);
    }

    private void SendSnapshotToClient(ulong clientId, TutorialStep step)
    {
        // locks + mouse + hud for the CURRENT step, targeted to this client
        SendLocksToSpecificClient(clientId, step);

        if (step.applyMouseSettingsOnStepStart)
            SendMouseToSpecificClient(clientId, step);

        ShowStepHUDToSpecificClient(clientId, step);

        // also notify step-started colliders (targeted)
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

        // When a client connects while tutorial is already running, send full snapshot (locks+hud+mouse+colliders)
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

        // auto complete time-based
        if (!step.completeOnCondition && step.autoCompleteAfter > 0 &&
            Time.time - stepStartTime >= step.autoCompleteAfter)
        {
            CompleteCurrentStep();
            return;
        }

        // condition-based complete
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

        Debug.Log(
            $"[TUTORIAL] Next step index={currentIndex} stepId={Current.stepId} cond={Current.conditionType}"
        );

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
        foreach (var c in autoColliders)
        {
            if (c != null)
                c.OnStepStarted(stepId);
        }
    }

    [ClientRpc]
    private void StepStartedCollidersTargetClientRpc(string stepId, ClientRpcParams rpcParams = default)
    {
        foreach (var c in autoColliders)
        {
            if (c != null)
                c.OnStepStarted(stepId);
        }
    }

    // ============================================================
    // LOCKS
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
        // host = traveller, client = navigator (your design)
        bool iAmTraveller = IsHost;
        bool iAmNavigator = !IsHost;

        Debug.Log(
            $"[TUTORIAL] ApplyLocks {(IsHost ? "HOST/Traveller" : "CLIENT/Navigator")} " +
            $"Trav(M:{travellerLockMovement},C:{travellerLockCamera}) " +
            $"Nav(M:{navigatorLockMovement},C:{navigatorLockCamera})"
        );

        foreach (var m in Object.FindObjectsByType<PlayerMovement1P>(FindObjectsSortMode.None))
        {
            if (!m.IsOwner) continue;

            if (iAmTraveller) m.SetFrozen(travellerLockMovement);
            if (iAmNavigator) m.SetFrozen(navigatorLockMovement);
        }

        foreach (var c in Object.FindObjectsByType<PlayerCamera1P>(FindObjectsSortMode.None))
        {
            if (!c.IsOwner) continue;

            if (iAmTraveller) c.SetCameraFrozen(travellerLockCamera);
            if (iAmNavigator) c.SetCameraFrozen(navigatorLockCamera);
        }
    }

    // ============================================================
    // MOUSE / CURSOR
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
        bool iAmTraveller = IsHost;
        bool iAmNavigator = !IsHost;

        if (iAmTraveller)
        {
            Cursor.visible = travellerCursorVisible;
            Cursor.lockState = (CursorLockMode)travellerCursorLockMode;
        }
        else if (iAmNavigator)
        {
            Cursor.visible = navigatorCursorVisible;
            Cursor.lockState = (CursorLockMode)navigatorCursorLockMode;
        }
    }

    // ============================================================
    // HUD
    // ============================================================

    private void ShowStepHUD(TutorialStep step)
    {
        Debug.Log($"[TUTORIAL] ShowStepHUD stepId={step.stepId}");

        // broadcast to all; each side will display only its own HUD
        SetTravellerHUDMessageClientRpc(step.travellerMessage);
        SetNavigatorHUDMessageClientRpc(step.navigatorMessage);
    }

    private void ShowStepHUDToSpecificClient(ulong clientId, TutorialStep step)
    {
        var p = MakeTargetParams(clientId);

        // send both messages to that one client; gating inside RPC will show correct one
        SetTravellerHUDMessageTargetClientRpc(step.travellerMessage, p);
        SetNavigatorHUDMessageTargetClientRpc(step.navigatorMessage, p);
    }

    [ClientRpc]
    private void SetTravellerHUDMessageClientRpc(string msg)
    {
        Debug.Log(
            $"[TUTORIAL][RPC] SetTravellerHUDMessageClientRpc side={(IsHost ? "HOST/Traveller" : "CLIENT/Navigator")} msg='{msg}'"
        );

        // only traveller (Host) shows travellerHUD
        if (!IsHost) return;

        if (travellerHUD == null)
        {
            Debug.LogWarning("[TUTORIAL] travellerHUD is NULL on host");
            return;
        }

        travellerHUD.ShowMessage(msg);
    }

    [ClientRpc]
    private void SetNavigatorHUDMessageClientRpc(string msg)
    {
        Debug.Log(
            $"[TUTORIAL][RPC] SetNavigatorHUDMessageClientRpc side={(IsHost ? "HOST/Traveller" : "CLIENT/Navigator")} msg='{msg}'"
        );

        // only navigator (Client) shows navigatorHUD
        if (IsHost) return;

        if (navigatorHUD == null)
        {
            Debug.LogWarning("[TUTORIAL] navigatorHUD is NULL on client");
            return;
        }

        navigatorHUD.ShowMessage(msg);
    }

    [ClientRpc]
    private void SetTravellerHUDMessageTargetClientRpc(string msg, ClientRpcParams rpcParams = default)
    {
        // identical gating as broadcast version
        if (!IsHost) return;

        if (travellerHUD == null)
        {
            Debug.LogWarning("[TUTORIAL] travellerHUD is NULL on host");
            return;
        }

        travellerHUD.ShowMessage(msg);
    }

    [ClientRpc]
    private void SetNavigatorHUDMessageTargetClientRpc(string msg, ClientRpcParams rpcParams = default)
    {
        // identical gating as broadcast version
        if (IsHost) return;

        if (navigatorHUD == null)
        {
            Debug.LogWarning("[TUTORIAL] navigatorHUD is NULL on client");
            return;
        }

        navigatorHUD.ShowMessage(msg);
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
        Debug.Log($"[TUTORIAL] Step COMPLETE  {(Current != null ? Current.stepId : "NULL")}");

        if (!IsServer || !stepActive)
        {
            Debug.Log("[TUTORIAL] Step complete aborted: not server or not active");
            return;
        }

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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
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
    public void NotifyTravellerSteppedBomb() => NotifyCondition(TutorialConditionType.TravellerSteppedBomb);

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
        if (!IsHost) return; // only traveller

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

    public void RegisterTraveller(Transform root) => travellerRoot = root;
    public void RegisterTravellerCamera(Camera cam) => travellerCamera = cam;
}
