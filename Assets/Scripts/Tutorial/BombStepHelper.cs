using UnityEngine;

public class BombStepHelper : MonoBehaviour
{
    [Header("Colliders")]
    public GameObject targetCollider;   // ה-trigger של שלב 4-1
    public GameObject tutorialCollider; // ה-TutorialCollider שמתחת RemoveBomb

    [Header("Camera Target")]
    public Transform lookTarget;        // המיקום של הפצצה

    private TutorialManager tutorial;

    private void Awake()
    {
        tutorial = FindFirstObjectByType<TutorialManager>();
    }

    // ============================================================
    // שלב 4-1 – המטייל הולך עד ה-TargetCollider
    // ============================================================

    // נקרא מ-OnStepStart של Step 4-1
    public void EnableTargetCollider()
    {
        if (targetCollider != null)
            targetCollider.SetActive(true);

        // הכפתור עדיין נעול עד שלב 4-2
        if (tutorialCollider != null)
            tutorialCollider.SetActive(true);

        // ❌ לא נוגעים במערך נעילות כאן — הכל נשלט ע"י ה-Step
        // שלב 4-1 חייב להגדיר בעצמו במאפיינים:
        // travellerLockMovement = false
        // travellerLockCamera   = false
        // navigatorLockMovement = false
        // navigatorLockCamera   = false
    }

    // נקרא מ-OnStepComplete של Step 4-1
    public void DisableTargetCollider()
    {
        if (targetCollider != null)
            targetCollider.SetActive(false);

        // ה-tutorialCollider ירד רק בתחילת שלב 4-2
    }

    // ============================================================
    // שלב 4-2 – המטייל רואה את הפצצה
    // ============================================================

    // נקרא מ-OnStepStart של Step 4-2
    public void OnBombStep2Start()
    {
        if (tutorial == null) return;

        // 🔥 1. לשחרר את ה-TutorialCollider (לתת לניווט אפשרות לעלות על הכפתור)
        if (tutorialCollider != null)
            tutorialCollider.SetActive(false);

        // ❌ לא נוגעים בנעילות כאן – הנעילות מוגדרות על ה-Step עצמו

        // 2. מסובב את המצלמה של המטייל לכיוון הפצצה
        RotateTravellerCameraToBomb();

        // 3. HUD
        tutorial.travellerHUD?.ShowMessage(
            "היזהר! פצצה מולך.\nבקש מהנווט להסיר אותה."
        );

        tutorial.navigatorHUD?.ShowMessage(
            "עמוד על הכפתור השחור כדי להסיר את הפצצה."
        );
    }

    private void RotateTravellerCameraToBomb()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.travellerCam == null || lookTarget == null)
            return;

        var camTransform = gm.travellerCam.transform;

        Vector3 dir = lookTarget.position - camTransform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.01f)
            camTransform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    // ============================================================
    // אחרי שהנווט דורך על RemoveBomb
    // ============================================================

    public void OnBombRemovedSuccess()
    {
        if (tutorial == null) return;

        // 🔥 לא משחררים ידנית — השלב הבא צריך להגדיר Unlock בעצמו ב-TutorialStep

        tutorial.travellerHUD?.ShowSuccess("הפצצה הוסרה בהצלחה! המשך להתקדם.");
        tutorial.navigatorHUD?.ShowSuccess("הסרת את הפצצה בהצלחה!");
    }
}
