using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class TutorialManager : NetworkBehaviour
{
    [Header("Config")]
    public TutorialStep[] steps;
    public bool autoStart = false;

    [Header("HUD References")]
    public TutorialHUD travellerHUD;
    public TutorialHUD navigatorHUD;

    private int currentIndex = -1;
    private bool stepActive;
    private float stepStartTime;
    private bool conditionSatisfied;

    private void Start()
    {
        if (!IsServer) return;

        if (autoStart && steps != null && steps.Length > 0)
            StartTutorial();
    }

    public void StartTutorial()
    {
        if (!IsServer) return;

        if (steps == null || steps.Length == 0)
        {
            Debug.LogWarning("[TutorialManager] No steps configured.");
            return;
        }

        currentIndex = -1;
        NextStep();
    }

    private void NextStep()
    {
        if (!IsServer) return;

        currentIndex++;

        if (currentIndex >= steps.Length)
        {
            stepActive = false;
            Debug.Log("[TutorialManager] Tutorial finished.");
            return;
        }

        var step = steps[currentIndex];
        stepActive = true;
        conditionSatisfied = false;
        stepStartTime = Time.time;

        ShowStepClientRpc(step.travellerMessage, step.navigatorMessage);

        step.onStepStart?.Invoke();
    }

    [ClientRpc]
    private void ShowStepClientRpc(string travellerMsg, string navigatorMsg)
    {
        if (IsHost)
        {
            if (travellerHUD != null && !string.IsNullOrEmpty(travellerMsg))
                travellerHUD.ShowMessage(travellerMsg);
        }

        if (IsClient && !IsHost)
        {
            if (navigatorHUD != null && !string.IsNullOrEmpty(navigatorMsg))
                navigatorHUD.ShowMessage(navigatorMsg);
        }
    }

    [ClientRpc]
    private void ClearHUDClientRpc()
    {
        if (IsHost)
            travellerHUD?.Clear();
        else
            navigatorHUD?.Clear();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!stepActive || currentIndex < 0 || currentIndex >= steps.Length)
            return;

        var step = steps[currentIndex];
        float elapsed = Time.time - stepStartTime;

        if (!step.completeOnCondition &&
            step.autoCompleteAfter > 0f &&
            elapsed >= step.autoCompleteAfter)
        {
            CompleteCurrentStep();
            return;
        }

        if (step.completeOnCondition &&
            conditionSatisfied &&
            elapsed >= step.minDuration)
        {
            CompleteCurrentStep();
        }
    }

    private void MarkConditionSatisfied(TutorialConditionType type)
    {
        if (!IsServer) return;
        if (!stepActive) return;

        var step = steps[currentIndex];
        if (step.conditionType != type)
            return;

        conditionSatisfied = true;

        if (step.completeOnCondition &&
            Time.time - stepStartTime >= step.minDuration)
        {
            CompleteCurrentStep();
        }
    }

    private void CompleteCurrentStep()
    {
        if (!IsServer) return;

        stepActive = false;

        var step = steps[currentIndex];
        step.onStepComplete?.Invoke();

        Debug.Log(">>> COMPLETE STEP " + currentIndex);

        StartCoroutine(ClearAndNext());
    }

    private IEnumerator ClearAndNext()
    {
        yield return new WaitForSeconds(0.15f); // זמן סנכרון מינימלי

        ClearHUDClientRpc();

        yield return new WaitForSeconds(0.05f);

        NextStep();
    }

    // Public API
    public void NotifyTravellerMoved() => MarkConditionSatisfied(TutorialConditionType.PlayerMoved);
    public void NotifyTravellerLooked() => MarkConditionSatisfied(TutorialConditionType.PlayerLookedAround);
    public void NotifyDoorOpened() => MarkConditionSatisfied(TutorialConditionType.DoorOpened);
    public void NotifyResourcePicked() => MarkConditionSatisfied(TutorialConditionType.ResourcePicked);
    public void NotifyNavigatorPlacedItem() => MarkConditionSatisfied(TutorialConditionType.NavigatorPlacedItem);
    public void NotifyPuzzleSolved() => MarkConditionSatisfied(TutorialConditionType.PuzzleSolved);
    public void NotifyBothReachedExit() => MarkConditionSatisfied(TutorialConditionType.BothReachedExit);
    public void NotifyCustomEvent() => MarkConditionSatisfied(TutorialConditionType.CustomEvent);
}
