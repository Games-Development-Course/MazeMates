using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    [Header("Config")]
    public TutorialStep[] steps;
    public bool autoStart = true;

    [Header("HUD References")]
    public TutorialHUD travellerHUD;
    public TutorialHUD navigatorHUD;

    private int currentIndex = -1;
    private bool stepActive;
    private float stepStartTime;
    private bool conditionSatisfied;

    private void Start()
    {
        if (autoStart && steps != null && steps.Length > 0)
        {
            StartTutorial();
        }
    }

    public void StartTutorial()
    {
        currentIndex = -1;
        NextStep();
    }

    private void NextStep()
    {
        currentIndex++;

        if (currentIndex >= steps.Length)
        {
            stepActive = false;
            // כאן אפשר לקרוא לאיוונט "tutorial finished" אם תרצה
            return;
        }

        var step = steps[currentIndex];
        stepActive = true;
        conditionSatisfied = false;
        stepStartTime = Time.time;

        // HUD messages
        if (travellerHUD != null)
        {
            travellerHUD.ShowMessage(step.travellerMessage);
        }

        if (navigatorHUD != null)
        {
            navigatorHUD.ShowMessage(step.navigatorMessage);
        }

        step.onStepStart?.Invoke();
    }

    private void Update()
    {
        if (!stepActive || currentIndex < 0 || currentIndex >= steps.Length)
            return;

        var step = steps[currentIndex];
        float elapsed = Time.time - stepStartTime;

        // מצב של שלב שנגמר לפי טיימר בלבד
        if (!step.completeOnCondition && step.autoCompleteAfter > 0f && elapsed >= step.autoCompleteAfter)
        {
            CompleteCurrentStep();
            return;
        }

        // מצב של שלב שנגמר לפי תנאי, אבל חייב לעמוד גם בזמן מינימלי
        if (step.completeOnCondition && conditionSatisfied && elapsed >= step.minDuration)
        {
            CompleteCurrentStep();
        }
    }

    private void MarkConditionSatisfied(TutorialConditionType type)
    {
        if (!stepActive)
            return;

        var step = steps[currentIndex];

        if (step.conditionType != type)
            return;

        conditionSatisfied = true;

        if (!step.completeOnCondition)
            return;

        if (Time.time - stepStartTime >= step.minDuration)
        {
            CompleteCurrentStep();
        }
    }

    private void CompleteCurrentStep()
    {
        if (!stepActive)
            return;

        stepActive = false;

        var step = steps[currentIndex];
        step.onStepComplete?.Invoke();

        // אפשר לנקות HUD אם תרצה
        // travellerHUD?.Clear();
        // navigatorHUD?.Clear();

        NextStep();
    }

    // הפונקציות הבאות הן ה API שאתה מחבר אליהן את המשחק
    // מהסקריפטים של ה Traveller, Navigator, דלתות, פאזלים וכו

    public void NotifyTravellerMoved()
    {
        MarkConditionSatisfied(TutorialConditionType.PlayerMoved);
    }

    public void NotifyTravellerLooked()
    {
        MarkConditionSatisfied(TutorialConditionType.PlayerLookedAround);
    }

    public void NotifyDoorOpened()
    {
        MarkConditionSatisfied(TutorialConditionType.DoorOpened);
    }

    public void NotifyResourcePicked()
    {
        MarkConditionSatisfied(TutorialConditionType.ResourcePicked);
    }

    public void NotifyNavigatorPlacedItem()
    {
        MarkConditionSatisfied(TutorialConditionType.NavigatorPlacedItem);
    }

    public void NotifyPuzzleSolved()
    {
        MarkConditionSatisfied(TutorialConditionType.PuzzleSolved);
    }

    public void NotifyBothReachedExit()
    {
        MarkConditionSatisfied(TutorialConditionType.BothReachedExit);
    }

    public void NotifyCustomEvent()
    {
        MarkConditionSatisfied(TutorialConditionType.CustomEvent);
    }
}
