using UnityEngine;

public class BombStepHelper : MonoBehaviour
{
    public BombStopZone bombZone;
    public GameObject tutorialCollider; // הילד של RemoveBomb

    private bool travellerReachedPoint = false;
    private bool bombRemoved = false;

    // נקרא ב-OnStepStart של שלב הפצצה
    public void OnBombStepStart()
    {
        // תת שלב 1: תחילת ההליכה של המטייל
        travellerReachedPoint = false;
        bombRemoved = false;

        // הנווט עדיין לא יכול ללחוץ על הכפתור
        tutorialCollider.SetActive(true);

        // מפעיל את ה־BombStopZone
        bombZone.EnableZone();
    }

    // נקרא מתוך BombStopZone כאשר המטייל נכנס
    public void OnTravellerReachedBombPoint()
    {
        travellerReachedPoint = true;

        // כבה את BombStopZone
        bombZone.DisableZone();

        // עבור לתת־שלב 2 — אפשר לניווט להתחיל לעבוד:
        tutorialCollider.SetActive(false);

        // עדכון HUD
        HUDManager.Instance.ShowTraveller("היזהר! פצצה מולך. בקש מהנווט להסיר אותה.");
        HUDManager.Instance.ShowNavigator("עמוד על הכפתור השחור כדי להסיר את הפצצה.");
    }

    // נקרא כאשר הנווט דורך על RemoveBomb
    public void OnNavigatorRemovedBomb()
    {
        if (!travellerReachedPoint) return;

        bombRemoved = true;

        // לאפשר למטייל להמשיך
        GameManager.Instance.travellerMove.SetFrozen(false);

        // להחזיר את ה-TutorialCollider כדי שלא ילחצו שוב
        tutorialCollider.SetActive(true);

        // הודעות HUD
        HUDManager.Instance.ShowTraveller("הפצצה הוסרה בהצלחה! המשך להתקדם.");
        HUDManager.Instance.ShowNavigator("הסרת את הפצצה בהצלחה.");

        TutorialManager.Instance.CompleteStep(); // סיום השלב
    }
}
