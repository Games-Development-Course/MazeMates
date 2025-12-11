using UnityEngine;
using UnityEngine.Events;

public enum TutorialConditionType
{
    None,

    // שליטה בסיסית
    TravellerMoved,
    NavigatorMoved,
    BothMoved,

    TravellerLookedAround,
    NavigatorLookedAround,
    BothLookedAround,

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

    // פאזלים
    TravellerPlacedPuzzlePiece,
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
    [TextArea] public string description;

    [Header("Logic")]
    public TutorialConditionType conditionType = TutorialConditionType.None;

    [Header("HUD Messages")]
    [TextArea] public string travellerMessage;
    [TextArea] public string navigatorMessage;

    // ===========================================================
    // NEW: explicit lock flags per role
    // ===========================================================
    [Header("Traveller Lock Settings")]
    public bool travellerLockMovement = true;
    public bool travellerLockCamera = true;

    [Header("Navigator Lock Settings")]
    public bool navigatorLockMovement = true;
    public bool navigatorLockCamera = true;

    [Header("Traveller Rotation / Look")]
    public bool rotateTravellerOnStepStart = false;
    public string travellerLookTargetId;        // למשל "DoorLook"

    // ===========================================================
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
