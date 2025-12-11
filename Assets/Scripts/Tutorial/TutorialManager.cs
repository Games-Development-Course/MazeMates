// TutorialManager.cs
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class TutorialManager : NetworkBehaviour
{
    // ============================================================
    // SINGLETON
    // ============================================================

    public static TutorialManager Instance { get; private set; }

    // ============================================================
    // CONFIG
    // ============================================================

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

    // ============================================================
    // NETWORKED STATE
    // ============================================================

    [Networked]
    public NetworkBool TutorialActive { get; set; }

    // ============================================================
    // INTERNAL STATE
    // ============================================================

    private int currentIndex = -1;
    private bool stepActive;
    private bool conditionSatisfied;

    private float stepStartTime;
    private float hudShownTime;

    private bool travellerMoved, navigatorMoved;
    private bool travellerLooked, navigatorLooked;

    private TutorialStep Current
    {
        get
        {
            if (steps == null || steps.Length == 0) return null;
            if (currentIndex < 0 || currentIndex >= steps.Length) return null;
            return steps[currentIndex];
        }
    }

    // רשימת קוליידרים אוטומטיים (דלת, לב, פאזל וכו')
    public static readonly List<TutorialColliderAuto> autoColliders = new List<TutorialColliderAuto>();

    public static void RegisterAutoCollider(TutorialColliderAuto c)
    {
        if (c != null && !autoColliders.Contains(c))
            autoColliders.Add(c);
    }

    // ============================================================
    // NETWORK SPAWN (FUSION)
    // ============================================================

    public override void Spawned()
    {
        base.Spawned();

        Instance = this;

        if (Object.HasStateAuthority)
            TutorialActive = false;

        Debug.Log("[TUTORIAL] Spawned TutorialManager (Fusion)");
    }

    // ============================================================
    // START TUTORIAL
    // ============================================================

    public void StartTutorial()
    {
        if (!Object || !Object.HasStateAuthority)
        {
            Debug.Log("[TUTORIAL] StartTutorial ignored – not StateAuthority");
            return;
        }

        if (steps == null || steps.Length == 0)
        {
            Debug.LogWarning("[TUTORIAL] StartTutorial called but no steps configured.");
            return;
        }

        currentIndex = -1;
        TutorialActive = true;
        NextStep();
    }

    // ============================================================
    // UPDATE LOOP (SERVER SIDE)
    // ============================================================

    private void Update()
    {
        if (!Object || !Object.HasStateAuthority) return;
        if (!stepActive) return;

        var step = Current;
        if (step == null) return;

        float elapsedHUD = Time.time - hudShownTime;

        // שלבים שמסתיימים לפי טיימר (ללא תנאי)
        if (!step.completeOnCondition &&
            step.autoCompleteAfter > 0 &&
            Time.time - stepStartTime >= step.autoCompleteAfter)
        {
            CompleteCurrentStep();
            return;
        }

        // שלבים עם תנאי – נגמרים רק אחרי שה־HUD היה על המסך מספיק זמן
        if (step.completeOnCondition && conditionSatisfied)
        {
            if (elapsedHUD < step.minHUDDuration)
                return;

            CompleteCurrentStep();
        }
    }

    // ============================================================
    // STEP FLOW
    // ============================================================

    private void NextStep()
    {
        if (!Object || !Object.HasStateAuthority) return;

        currentIndex++;

        if (steps == null || currentIndex >= steps.Length)
        {
            Debug.Log("[TUTORIAL] Finished all steps.");

            stepActive = false;
            TutorialActive = false;

            // אופציונלי: לנקות HUD לגמרי בסוף הטוטוריאל
            ClearHUDRpc();
            return;
        }

        var step = Current;
        Debug.Log($"[TUTORIAL] Next step index={currentIndex} stepId={step.stepId} cond={step.conditionType}");

        stepActive = true;
        conditionSatisfied = false;

        stepStartTime = Time.time;
        hudShownTime = Time.time;

        travellerMoved = navigatorMoved = false;
        travellerLooked = navigatorLooked = false;

        // שמנו דיליי קטן כדי לתת לפיזיקה/סצנה להתייצב לפני ביטול קוליידרים
        Runner.StartCoroutine(DelayedColliderStep(step.stepId, 0.2f));

        ApplyLocks(step);
        ShowStepHUD(step);

        AlignTravellerForStep(step);

        step.onStepStart?.Invoke();
    }

    private IEnumerator DelayedColliderStep(string id, float delay)
    {
        yield return new WaitForSeconds(delay);
        StepStartedCollidersRpc(id);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void StepStartedCollidersRpc(string stepId, RpcInfo info = default)
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
        ApplyLocksRpc(
            s.travellerLockMovement,
            s.travellerLockCamera,
            s.navigatorLockMovement,
            s.navigatorLockCamera);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ApplyLocksRpc(
        bool travellerLockMovement,
        bool travellerLockCamera,
        bool navigatorLockMovement,
        bool navigatorLockCamera,
        RpcInfo info = default)
    {
        Debug.Log(
            $"[TUTORIAL] ApplyLocks | Trav(M:{travellerLockMovement},C:{travellerLockCamera}) " +
            $"Nav(M:{navigatorLockMovement},C:{navigatorLockCamera})");

        // הקפאת תנועה לפי HasInputAuthority ותפקיד
        foreach (var m in UnityEngine.Object.FindObjectsByType<PlayerMovement1P>(FindObjectsSortMode.None))
        {
            if (!m.HasInputAuthority) continue;

            if (m.IsTraveller)
                m.SetFrozen(travellerLockMovement);
            else if (m.IsNavigator)
                m.SetFrozen(navigatorLockMovement);
        }

        // הקפאת מצלמה
        foreach (var c in UnityEngine.Object.FindObjectsByType<PlayerCamera1P>(FindObjectsSortMode.None))
        {
            if (!c.HasInputAuthority) continue;

            if (c.IsTraveller)
                c.SetCameraFrozen(travellerLockCamera);
            else if (c.IsNavigator)
                c.SetCameraFrozen(navigatorLockCamera);
        }
    }

    // ============================================================
    // HUD
    // ============================================================

    private void ShowStepHUD(TutorialStep step)
    {
        SetTravellerHUDMessageRpc(step.travellerMessage);
        SetNavigatorHUDMessageRpc(step.navigatorMessage);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ClearHUDRpc(RpcInfo info = default)
    {
        if (travellerHUD != null)
            travellerHUD.Clear();

        if (navigatorHUD != null)
            navigatorHUD.Clear();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SetTravellerHUDMessageRpc(string msg, RpcInfo info = default)
    {
        if (travellerHUD != null)
            travellerHUD.ShowMessage(msg);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SetNavigatorHUDMessageRpc(string msg, RpcInfo info = default)
    {
        if (navigatorHUD != null)
            navigatorHUD.ShowMessage(msg);
    }

    // ============================================================
    // COMPLETE STEP
    // ============================================================

    private void MarkConditionSatisfiedInternal()
    {
        var step = Current;
        string id = step != null ? step.stepId : "UNKNOWN";

        Debug.Log($"[TUTORIAL] Condition satisfied for step '{id}'");
        conditionSatisfied = true;
    }

    private void CompleteCurrentStep()
    {
        var step = Current;

        Debug.Log($"[TUTORIAL] Step COMPLETE  {(step != null ? step.stepId : "NULL_STEP")}");

        if (!Object || !Object.HasStateAuthority || !stepActive)
        {
            Debug.Log("[TUTORIAL] Step complete aborted: not StateAuthority or not active");
            return;
        }

        stepActive = false;
        step?.onStepComplete?.Invoke();

        Runner.StartCoroutine(GoNext());
    }

    private IEnumerator GoNext()
    {
        yield return new WaitForSeconds(defaultAutoStepDelay);
        NextStep();
    }

    // ============================================================
    // MOVEMENT + LOOK
    // ============================================================

    public void NotifyTravellerMoved() => HandleMovement(ref travellerMoved, true);
    public void NotifyNavigatorMoved() => HandleMovement(ref navigatorMoved, false);

    private void HandleMovement(ref bool flag, bool isTravellerFlag)
    {
        if (!Object || !Object.HasStateAuthority || !stepActive) return;

        flag = true;

        var step = Current;
        if (step == null) return;

        if (step.conditionType == TutorialConditionType.TravellerMoved && isTravellerFlag)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.NavigatorMoved && !isTravellerFlag)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.BothMoved &&
            travellerMoved && navigatorMoved)
            MarkConditionSatisfiedInternal();
    }

    public void NotifyTravellerLooked() => HandleLook(ref travellerLooked, true);
    public void NotifyNavigatorLooked() => HandleLook(ref navigatorLooked, false);

    private void HandleLook(ref bool flag, bool isTravellerFlag)
    {
        if (!Object || !Object.HasStateAuthority || !stepActive) return;

        flag = true;

        var step = Current;
        if (step == null) return;

        if (step.conditionType == TutorialConditionType.TravellerLookedAround && isTravellerFlag)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.NavigatorLookedAround && !isTravellerFlag)
            MarkConditionSatisfiedInternal();

        if (step.conditionType == TutorialConditionType.BothLookedAround &&
            travellerLooked && navigatorLooked)
            MarkConditionSatisfiedInternal();
    }

    // ============================================================
    // IS TUTORIAL RUNNING FOR STEP (SAFE FOR FUSION)
    // ============================================================

    public bool IsTutorialRunningForStep(string stepId)
    {
        // חשוב: קודם בודקים את Object לפני שנוגעים ב-Networked
        if (!Object || !Object.IsValid)
            return false;

        if (!TutorialActive)
            return false;

        if (steps == null || steps.Length == 0)
            return false;

        if (currentIndex < 0 || currentIndex >= steps.Length)
            return false;

        return steps[currentIndex].stepId == stepId;
    }

    // ============================================================
    // CONDITION EVENTS
    // ============================================================

    public void NotifyNavigatorRemovedBomb() => NotifyNavigatorCondition(TutorialConditionType.NavigatorRemoveBomb);

    public void NotifyNavigatorOpenedNormalDoor()
    {
        NotifyNavigatorCondition(TutorialConditionType.NavigatorOpenNormalDoor);
        Debug.Log("reached NotifyNavigatorOpenedNormalDoor");
    }

    public void NotifyNavigatorOpenedPuzzleDoor() => NotifyNavigatorCondition(TutorialConditionType.NavigatorOpenPuzzleDoor);

    public void NotifyNavigatorOpenedExitDoor()
    {
        NotifyNavigatorCondition(TutorialConditionType.NavigatorOpenExitDoor);
        ActivateDiscoMode();
    }

    public void NotifyNavigatorPlacedHeart() => NotifyNavigatorCondition(TutorialConditionType.NavigatorPlaceHeart);
    public void NotifyNavigatorGaveLifebuoy() => NotifyNavigatorCondition(TutorialConditionType.NavigatorGiveLifebuoy);

    // לשמור תאימות אם הוספת TravellerPlacedPuzzlePiece בטייפ
    public void NotifyTravellerPlacedPuzzlePiece() =>
        NotifyNavigatorCondition(TutorialConditionType.TravellerPlacedPuzzlePiece);

    public void NotifyTravellerPickedKey() => Check(TutorialConditionType.TravellerPickedKey);
    public void NotifyTravellerPickedHeart() => Check(TutorialConditionType.TravellerPickedHeart);
    public void NotifyPuzzleSolved() => Check(TutorialConditionType.PuzzleSolved);
    public void NotifyBothReachedExit() => Check(TutorialConditionType.BothReachedExit);
    public void NotifyCustomEvent() => Check(TutorialConditionType.CustomEvent);

    private void NotifyNavigatorCondition(TutorialConditionType condition)
    {
        Debug.Log("reached NotifyNavigatorCondition");

        if (Object.HasStateAuthority)
        {
            Check(condition);
        }
        else
        {
            NotifyNavigatorConditionRpc(condition);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void NotifyNavigatorConditionRpc(TutorialConditionType condition, RpcInfo info = default)
    {
        Debug.Log("reached NotifyNavigatorConditionRpc");
        Debug.Log($"[TUTORIAL] StateAuthority received navigator condition: {condition}");

        Check(condition);
    }

    private void Check(TutorialConditionType t)
    {
        Debug.Log("Reached Check()");

        var step = Current;
        string id = step != null ? step.stepId : "NULL_STEP";
        var expected = step != null ? step.conditionType : TutorialConditionType.None;

        Debug.Log($"[TUTORIAL][CHECK] cond={t} step={id} expect={expected} active={stepActive}");

        if (!Object || !Object.HasStateAuthority) return;
        if (!stepActive || step == null) return;

        if (step.conditionType != t)
        {
            Debug.Log("[TUTORIAL][CHECK] Condition mismatch — ignored");
            return;
        }

        Debug.Log($"[TUTORIAL][CHECK] MATCH!  Step '{step.stepId}' satisfied");
        MarkConditionSatisfiedInternal();
    }

    // ============================================================
    // TRAVELLER ROTATION / LOOK
    // ============================================================

    private void AlignTravellerForStep(TutorialStep step)
    {
        if (step == null) return;
        if (!step.rotateTravellerOnStepStart) return;
        if (string.IsNullOrEmpty(step.travellerLookTargetId)) return;

        RotateTravellerRpc(step.travellerLookTargetId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RotateTravellerRpc(string targetId, RpcInfo info = default)
    {
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

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        travellerRoot.rotation = rot;

        if (travellerCamera == null)
        {
            var cam = travellerRoot.GetComponentInChildren<Camera>();
            if (cam != null)
                travellerCamera = cam;
        }

        if (travellerCamera != null)
        {
            Vector3 euler = travellerCamera.transform.eulerAngles;
            euler.y = rot.eulerAngles.y;
            travellerCamera.transform.eulerAngles = euler;
        }
    }

    // ============================================================
    // FUN STUFF
    // ============================================================

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
    // REGISTER TRAVELLER REFERENCES
    // ============================================================

    public void RegisterTraveller(Transform root)
    {
        travellerRoot = root;
    }

    public void RegisterTravellerCamera(Camera cam)
    {
        travellerCamera = cam;
    }
}
