using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class TutorialManager : NetworkBehaviour
{
    [Header("Config")]
    public TutorialStep[] steps;

    [Header("HUD")]
    public TutorialHUD travellerHUD;
    public TutorialHUD navigatorHUD;

    [Header("Waiting Messages")]
    [TextArea] public string travellerWaitingForNavigator = "ממתין לנווט…";
    [TextArea] public string navigatorWaitingForTraveller = "ממתין למטייל…";

    public float defaultAutoStepDelay = 0.15f;

    public NetworkVariable<bool> TutorialActive =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private int currentIndex = -1;
    private bool stepActive;
    private bool conditionSatisfied;

    private float stepStartTime;
    private float hudShownTime;
    private float waitMessageShownTime;
    private bool showingWaitMessage;

    private bool travellerMoved, navigatorMoved;
    private bool travellerLooked, navigatorLooked;

    private TutorialStep Current => steps[currentIndex];

    // ============================================================
    // NETWORK START
    // ============================================================

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            TutorialActive.Value = false;
    }

    public void StartTutorial()
    {
        if (!IsServer) return;

        currentIndex = -1;
        TutorialActive.Value = true;
        NextStep();
    }

    private void Update()
    {
        if (!IsServer || !stepActive) return;
        if (currentIndex < 0 || currentIndex >= steps.Length) return;

        TutorialStep step = Current;

        float elapsedHUD = Time.time - hudShownTime;
        float elapsedWait = showingWaitMessage ? Time.time - waitMessageShownTime : 9999f;

        // --- Auto complete ---
        if (!step.completeOnCondition &&
            step.autoCompleteAfter > 0 &&
            Time.time - stepStartTime >= step.autoCompleteAfter)
        {
            CompleteCurrentStep();
            return;
        }

        // --- Complete-on-condition with HUD timing ---
        if (step.completeOnCondition && conditionSatisfied)
        {
            // חייבים זמן מינימום להודעה הראשונית
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
        if (!IsServer) return;

        ClearHUDClientRpc();
        currentIndex++;

        if (currentIndex >= steps.Length)
        {
            TutorialActive.Value = false;
            stepActive = false;
            return;
        }

        stepActive = true;
        conditionSatisfied = false;

        stepStartTime = Time.time;
        hudShownTime = Time.time;
        waitMessageShownTime = -1f;
        showingWaitMessage = false;

        travellerMoved = navigatorMoved = false;
        travellerLooked = navigatorLooked = false;

        var step = Current;

        ApplyUnlocks(step);
        ShowStepHUD(step);

        step.onStepStart?.Invoke();
    }

    // ============================================================
    // UNLOCK CONTROL – CLIENT RPC
    // ============================================================

    private void ApplyUnlocks(TutorialStep s)
    {
        ApplyUnlocksClientRpc(s.targetRole, s.unlockMovement, s.unlockCamera);
    }

    [ClientRpc]
    private void ApplyUnlocksClientRpc(TutorialRoleTarget targetRole, bool unlockMovement, bool unlockCamera)
    {
        var movers = FindObjectsOfType<PlayerMovement1P>();
        foreach (var m in movers)
        {
            if (!m.IsOwner) continue;

            bool affect = targetRole switch
            {
                TutorialRoleTarget.Traveller => m.name.Contains("Trav"),
                TutorialRoleTarget.Navigator => m.name.Contains("Nav"),
                TutorialRoleTarget.Both => true,
                _ => false
            };

            if (affect)
                m.SetFrozen(!unlockMovement);
        }

        var cams = FindObjectsOfType<PlayerCamera1P>();
        foreach (var c in cams)
        {
            if (!c.IsOwner) continue;

            bool affect = targetRole switch
            {
                TutorialRoleTarget.Traveller => c.name.Contains("Trav"),
                TutorialRoleTarget.Navigator => c.name.Contains("Nav"),
                TutorialRoleTarget.Both => true,
                _ => false
            };

            if (affect)
                c.SetCameraFrozen(!unlockCamera);
        }
    }

    // ============================================================
    // HUD PER ROLE
    // ============================================================

    private void ShowStepHUD(TutorialStep step)
    {
        if (travellerHUD != null)
            SetTravellerHUDMessageClientRpc(step.travellerMessage);

        if (navigatorHUD != null)
            SetNavigatorHUDMessageClientRpc(step.navigatorMessage);
    }

    [ClientRpc]
    private void ClearHUDClientRpc()
    {
        if (IsHost) travellerHUD?.Clear();
        else navigatorHUD?.Clear();
    }

    [ClientRpc]
    private void SetTravellerHUDMessageClientRpc(string msg)
    {
        if (IsHost)
            travellerHUD?.ShowMessage(msg);
    }

    [ClientRpc]
    private void SetNavigatorHUDMessageClientRpc(string msg)
    {
        if (!IsHost)
            navigatorHUD?.ShowMessage(msg);
    }

    // ============================================================
    // COMPLETE CURRENT STEP
    // ============================================================

    private void MarkConditionSatisfiedInternal()
    {
        conditionSatisfied = true;
    }

    private void CompleteCurrentStep()
    {
        if (!IsServer || !stepActive) return;

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
    // MOVEMENT + LOOK EVENT HANDLERS
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

        if (step.conditionType == TutorialConditionType.BothMoved)
        {
            if (Time.time - hudShownTime >= Current.minHUDDuration)
            {
                ShowWaitMessage(isTraveller);
                showingWaitMessage = true;
            }



            if (travellerMoved && navigatorMoved)
                MarkConditionSatisfiedInternal();
        }
    }

    // LOOK
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

        if (step.conditionType == TutorialConditionType.BothLookedAround)
        {
            if (Time.time - hudShownTime >= Current.minHUDDuration)
            {
                ShowWaitMessage(isTraveller);
                showingWaitMessage = true;
            }


            if (travellerLooked && navigatorLooked)
                MarkConditionSatisfiedInternal();
        }
    }

    private void ShowWaitMessage(bool isTraveller)
    {
        if (isTraveller)
            SetTravellerHUDMessageClientRpc(travellerWaitingForNavigator);
        else
            SetNavigatorHUDMessageClientRpc(navigatorWaitingForTraveller);
    }

    // ============================================================
    // OTHER EVENTS
    // ============================================================

    public void NotifyNavigatorRemovedBomb() => Check(TutorialConditionType.NavigatorRemoveBomb);
    public void NotifyNavigatorOpenedNormalDoor() => Check(TutorialConditionType.NavigatorOpenNormalDoor);
    public void NotifyNavigatorOpenedPuzzleDoor() => Check(TutorialConditionType.NavigatorOpenPuzzleDoor);
    public void NotifyNavigatorOpenedExitDoor() => Check(TutorialConditionType.NavigatorOpenExitDoor);
    public void NotifyNavigatorPlacedHeart() => Check(TutorialConditionType.NavigatorPlaceHeart);
    public void NotifyNavigatorGaveLifebuoy() => Check(TutorialConditionType.NavigatorGiveLifebuoy);

    public void NotifyTravellerPickedKey() => Check(TutorialConditionType.TravellerPickedKey);
    public void NotifyTravellerPickedHeart() => Check(TutorialConditionType.TravellerPickedHeart);

    public void NotifyPuzzleSolved() => Check(TutorialConditionType.PuzzleSolved);
    public void NotifyBothReachedExit() => Check(TutorialConditionType.BothReachedExit);
    public void NotifyCustomEvent() => Check(TutorialConditionType.CustomEvent);

    private void Check(TutorialConditionType t)
    {
        if (!IsServer || !stepActive) return;
        if (Current.conditionType == t)
            MarkConditionSatisfiedInternal();
    }
}
