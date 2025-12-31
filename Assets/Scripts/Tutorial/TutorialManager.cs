// ======================= TutorialManager.cs =======================
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

    private TutorialStep Current => steps[currentIndex];

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
        base.OnNetworkSpawn();

        if (IsServer)
            TutorialActive.Value = false;

        // במולטיפלייר יש היררכייה אחת -> לא מכבים HUD-ים פה
        if (travellerHUD != null) travellerHUD.gameObject.SetActive(true);
        if (navigatorHUD != null) navigatorHUD.gameObject.SetActive(true);

        Debug.Log(
            $"[TUTORIAL][OnNetworkSpawn] LocalClientId={NetworkManager.Singleton?.LocalClientId} IsHost={IsHost} IsClient={IsClient} IsServer={IsServer}"
        );
    }

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

    // ============================================================
    // CLIENT CONNECTED
    // ============================================================

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        if (!TutorialActive.Value) return;
        if (currentIndex < 0 || currentIndex >= steps.Length) return;

        SendLocksToSpecificClient(clientId, Current);
    }

    private void SendLocksToSpecificClient(ulong clientId, TutorialStep step)
    {
        var p = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } },
        };

        ApplyLocksClientRpc(
            step.travellerLockMovement,
            step.travellerLockCamera,
            step.navigatorLockMovement,
            step.navigatorLockCamera,
            p
        );

        if (step.applyMouseSettingsOnStepStart)
        {
            ApplyMouseClientRpc(
                step.travellerCursorVisible,
                (int)step.travellerCursorLockMode,
                step.navigatorCursorVisible,
                (int)step.navigatorCursorLockMode,
                p
            );
        }
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

        if (currentIndex < 0 || currentIndex >= steps.Length) return;

        TutorialStep step = Current;
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

        if (currentIndex >= steps.Length)
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

    [ClientRpc]
    private void ApplyLocksClientRpc(
     bool travellerLockMovement,
     bool travellerLockCamera,
     bool navigatorLockMovement,
     bool navigatorLockCamera,
     ClientRpcParams rpcParams = default
 )
    {
        bool iAmTraveller = IsHost;   // host = traveller
        bool iAmNavigator = !IsHost;  // client = navigator

        Debug.Log(
            $"[TUTORIAL] ApplyLocks {(IsHost ? "HOST/Traveller" : "CLIENT/Navigator")} " +
            $"Trav(M:{travellerLockMovement},C:{travellerLockCamera}) " +
            $"Nav(M:{navigatorLockMovement},C:{navigatorLockCamera})"
        );

        // Movement: call SetFrozen on the owner movement component
        foreach (var m in Object.FindObjectsByType<PlayerMovement1P>(FindObjectsSortMode.None))
        {
            if (!m.IsOwner) continue;

            if (iAmTraveller) m.SetFrozen(travellerLockMovement);
            if (iAmNavigator) m.SetFrozen(navigatorLockMovement);
        }

        // Camera: call SetCameraFrozen on the owner camera component
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

        // Traveller HUD message
        SetTravellerHUDMessageClientRpc(step.travellerMessage);

        // Navigator HUD message
        SetNavigatorHUDMessageClientRpc(step.navigatorMessage);
    }

    [ClientRpc]
    private void SetTravellerHUDMessageClientRpc(string msg)
    {
        Debug.Log(
            $"[TUTORIAL][RPC] SetTravellerHUDMessageClientRpc side={(IsHost ? "HOST/Traveller" : "CLIENT/Navigator")} msg='{msg}'"
        );

        // רק המטייל (Host) מציג travellerHUD
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

        // רק הנווט (Client) מציג navigatorHUD
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
        Debug.Log($"[TUTORIAL] Condition satisfied for step '{Current.stepId}'");
        conditionSatisfied = true;
    }

    private void CompleteCurrentStep()
    {
        Debug.Log($"[TUTORIAL] Step COMPLETE  {Current.stepId}");

        if (!IsServer || !stepActive)
        {
            Debug.Log("[TUTORIAL] Step complete aborted: not server or not active");
            return;
        }

        stepActive = false;
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
        if (currentIndex < 0 || currentIndex >= steps.Length) return false;
        return steps[currentIndex].stepId == stepId;
    }

    // ============================================================
    // ✅ SYSTEMIC CONDITION DISPATCH (fix)
    // ============================================================

    /// <summary>
    /// Call this from ANY side (host/client). It will always validate the condition on the server.
    /// </summary>
    private void NotifyCondition(TutorialConditionType condition)
    {
        if (IsServer)
        {
            Check(condition);
        }
        else
        {
            NotifyConditionServerRpc(condition);
        }
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
            $"[TUTORIAL][CHECK] cond={t} step={(currentIndex >= 0 && currentIndex < steps.Length ? Current.stepId : "NONE")} " +
            $"expect={(currentIndex >= 0 && currentIndex < steps.Length ? Current.conditionType.ToString() : "NONE")} active={stepActive}"
        );

        if (!IsServer) return;
        if (!stepActive) return;

        if (Current.conditionType != t)
        {
            Debug.Log("[TUTORIAL][CHECK] Condition mismatch — ignored");
            return;
        }

        Debug.Log($"[TUTORIAL][CHECK] MATCH! Step '{Current.stepId}' satisfied");
        MarkConditionSatisfiedInternal();
    }

    // ============================================================
    // CONDITION EVENTS (now all go through NotifyCondition)
    // ============================================================

    public void NotifyNavigatorRemovedBomb() =>
        NotifyCondition(TutorialConditionType.NavigatorRemoveBomb);

    public void NotifyNavigatorOpenedNormalDoor() =>
        NotifyCondition(TutorialConditionType.NavigatorOpenNormalDoor);

    public void NotifyNavigatorOpenedPuzzleDoor() =>
        NotifyCondition(TutorialConditionType.NavigatorOpenPuzzleDoor);

    public void NotifyNavigatorOpenedExitDoor()
    {
        NotifyCondition(TutorialConditionType.NavigatorOpenExitDoor);
        ActivateDiscoMode();
    }

    public void NotifyNavigatorPlacedHeart() =>
        NotifyCondition(TutorialConditionType.NavigatorPlaceHeart);

    public void NotifyNavigatorGaveLifebuoy() =>
        NotifyCondition(TutorialConditionType.NavigatorGiveLifebuoy);

    public void NotifyTravellerPlacedPuzzlePiece() =>
        NotifyCondition(TutorialConditionType.TravellerPlacedPuzzlePiece);

    public void NotifyTravellerPickedKey() =>
        NotifyCondition(TutorialConditionType.TravellerPickedKey);

    public void NotifyTravellerPickedHeart() =>
        NotifyCondition(TutorialConditionType.TravellerPickedHeart);

    public void NotifyPuzzleSolved() =>
        NotifyCondition(TutorialConditionType.PuzzleSolved);

    public void NotifyBothReachedExit() =>
        NotifyCondition(TutorialConditionType.BothReachedExit);

    public void NotifyCustomEvent() =>
        NotifyCondition(TutorialConditionType.CustomEvent);

    public void NotifyTravellerSteppedBomb() =>
        NotifyCondition(TutorialConditionType.TravellerSteppedBomb);

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
        if (!IsHost) return; // רק אצל המטייל

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
