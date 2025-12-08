using UnityEngine;
using UnityEngine.Events;

public enum TutorialRoleTarget
{
    Traveller,
    Navigator,
    Both
}

public enum TutorialConditionType
{
    None,
    PlayerMoved,
    PlayerLookedAround,
    DoorOpened,
    ResourcePicked,
    NavigatorPlacedItem,
    PuzzleSolved,
    BothReachedExit,
    CustomEvent
}

[CreateAssetMenu(menuName = "MazeMates/Tutorial Step", fileName = "NewTutorialStep")]
public class TutorialStep : ScriptableObject
{
    [Header("Identification")]
    public string stepId;
    [TextArea]
    public string description;

    [Header("Logic")]
    public TutorialRoleTarget targetRole;
    public TutorialConditionType conditionType = TutorialConditionType.None;

    [Header("HUD Messages")]
    [TextArea]
    public string travellerMessage;
    [TextArea]
    public string navigatorMessage;

    [Header("Timing")]
    [Tooltip("אם true השלב מסתיים כאשר התנאי מתקיים")]
    public bool completeOnCondition = true;

    [Tooltip("זמן מינימלי שהשלב חייב לרוץ לפני שמותר לסיים אותו")]
    public float minDuration = 0f;

    [Tooltip("אם ערך גדול מאפס וב completeOnCondition = false השלב יסתיים אוטומטית אחרי מספר שניות")]
    public float autoCompleteAfter = 0f;

    [Header("Events")]
    public UnityEvent onStepStart;
    public UnityEvent onStepComplete;
}
