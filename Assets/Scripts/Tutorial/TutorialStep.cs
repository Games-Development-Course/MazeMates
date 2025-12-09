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

    // שליטה בסיסית
    TravellerMoved,
    NavigatorMoved,
    BothMoved,              // ⭐ שני השחקנים חייבים לזוז

    TravellerLookedAround,
    NavigatorLookedAround,
    BothLookedAround,        // ⭐ שני השחקנים חייבים להסתכל מסביב

    // כפתורי הנווט
    NavigatorRemoveBomb,
    NavigatorOpenNormalDoor,
    NavigatorOpenPuzzleDoor,
    NavigatorOpenExitDoor,
    NavigatorPlaceHeart,
    NavigatorGiveLifebuoy,

    // משאבים אצל המטייל
    TravellerPickedKey,
    TravellerPickedHeart,

    // פאזלים ודלתות
    PuzzleSolved,

    // סוף מבוך
    BothReachedExit,

    CustomEvent
}

[CreateAssetMenu(menuName = "Scriptable Objects/Tutorial Step", fileName = "NewTutorialStep")]
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
    [TextArea] public string travellerMessage;
    [TextArea] public string navigatorMessage;

    [Header("Unlock Controls")]
    public bool unlockMovement = false;
    public bool unlockCamera = false;

    [Header("Timing")]
    public bool completeOnCondition = true;
    public float minDuration = 0f;
    public float autoCompleteAfter = 0f;
    public float minHUDDuration = 0f;
    public float minWaitMessageDuration = 0f;

    [Header("Events")]
    public UnityEvent onStepStart;
    public UnityEvent onStepComplete;
}
