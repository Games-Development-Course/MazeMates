    using System.Collections;
    using TMPro;
    using UnityEngine;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance;

    public TravellerHUD Traveller;
    public NavigatorHUD Navigator;

    public float defaultMessageDuration = 2f;
    public float HideDuration = 0.6f;

    private void Awake()
    {
        Instance = this;
    }

    // ============================================
    // הודעות HUD רגילות (לא טוטוריאל)
    // ============================================

    private void ShowAndHide(string travellerMsg, string navigatorMsg, float duration)
    {
        var tm = FindFirstObjectByType<TutorialManager>();
        if (tm != null && tm.TutorialActive.Value)
            return; // בזמן טוטוריאל לא נוגעים בהודעות הרגילות

        if (!string.IsNullOrEmpty(travellerMsg) && Traveller != null)
            Traveller.ShowMessage(travellerMsg);

        if (!string.IsNullOrEmpty(navigatorMsg) && Navigator != null)
            Navigator.ShowMessage(navigatorMsg);

        StopAllCoroutines();
        StartCoroutine(HideMessagesAfter(duration));
    }

    private IEnumerator HideMessagesAfter(float t)
    {
        yield return new WaitForSeconds(t);

        float duration = 0.35f;
        float time = 0f;

        var travellerText = Traveller != null ? Traveller.messageText : null;
        var navigatorText = Navigator != null ? Navigator.messageText : null;

        Color tColor = travellerText != null ? travellerText.color : Color.white;
        Color nColor = navigatorText != null ? navigatorText.color : Color.white;

        while (time < duration)
        {
            float a = Mathf.Lerp(1f, 0f, time / duration);

            if (travellerText != null)
                travellerText.color = new Color(tColor.r, tColor.g, tColor.b, a);

            if (navigatorText != null)
                navigatorText.color = new Color(nColor.r, nColor.g, nColor.b, a);

            time += Time.deltaTime;
            yield return null;
        }

        if (travellerText != null)
        {
            travellerText.text = "";
            travellerText.color = new Color(tColor.r, tColor.g, tColor.b, 1f);
        }

        if (navigatorText != null)
        {
            navigatorText.text = "";
            navigatorText.color = new Color(nColor.r, nColor.g, nColor.b, 1f);
        }
    }

    public void ShowMessageForTraveller(string msg) => ShowAndHide(msg, null, defaultMessageDuration);
    public void ShowMessageForNavigator(string msg) => ShowAndHide(null, msg, defaultMessageDuration);
    public void ShowMessageForBoth(string msg) => ShowAndHide(msg, msg, defaultMessageDuration);

    // ============================================
    // ערכי משחק (חיים / מפתחות / משאבים)
    // ============================================

    public void UpdateHUD()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;

        if (Traveller != null)
            Traveller.UpdateShared(gm);

        if (Navigator != null)
            Navigator.UpdateShared(gm);
    }

    public void FlashTravellerLife()
    {
        if (Traveller != null)
            Traveller.FlashLives();
    }

    public void ShowPuzzle(Sprite navigatorSprite)
    {
        if (Traveller != null)
            Traveller.ShowPuzzle();
    }

    public void HidePuzzle()
    {
        if (Traveller != null)
            Traveller.HidePuzzle();
    }


    public void UpdateHUDs() => UpdateHUD();
    public void FlashLifeIcons() => FlashTravellerLife();

    public TravellerHUD TravellerHUD => Traveller;
    public NavigatorHUD NavigatorHUD => Navigator;

    public void SetMessageAppearanceForBoth(Color c, float dur)
    {
        if (Traveller != null)
            Traveller.SetMessageColor(c);

        if (Navigator != null)
            Navigator.SetMessageColor(c);

        StopAllCoroutines();
        StartCoroutine(HideMessagesAfter(dur));
    }

    public void ApplyState(int lives, int keys, int lifebuoys, int heartPlacements, int bombRemovals)
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;

        gm.lives = lives;
        gm.keys = keys;
        gm.lifebuoys = lifebuoys;
        gm.HeartPlacements = heartPlacements;
        gm.BombRemovals = bombRemovals;

        UpdateHUD();
    }
    public void NavWorldNotReady()
    {
        ShowMessageForNavigator("עולם המשחק לא מוכן");
    }

    public void NavNoDoorHere()
    {
        ShowMessageForNavigator("אין דלת כאן");
    }

    public void NavDoorRequiresPuzzle()
    {
        ShowMessageForNavigator("דלת זו דורשת לפתור חידה");
    }

    public void NavNoPuzzleDoorHere()
    {
        ShowMessageForNavigator("אין דלת חידה כאן");
    }

    public void NavResourcesNotReady()
    {
        ShowMessageForNavigator("מערכת משאבים לא מוכנה");
    }

    public void NavNoHeartsLeft()
    {
        ShowMessageForNavigator("לא נותרו לבבות");
    }

    public void NavNoTraveller()
    {
        ShowMessageForNavigator("אין מטייל במשחק");
    }

    public void NavNoBombAttempts()
    {
        ShowMessageForNavigator("לא נותרו ניסיונות להסרת פצצה");
    }

    public void NavNoBombsOnMap()
    {
        ShowMessageForNavigator("אין פצצות במפה");
    }

    public void NavNoBombFound()
    {
        ShowMessageForNavigator("לא נמצאה פצצה");
    }

    public void NavNoLifebuoys()
    {
        ShowMessageForNavigator("לא נותרו מצופי הצלה");
    }

    public void NavLifebuoyOnlyInPuzzle()
    {
        ShowMessageForNavigator("ניתן להשתמש במצוף רק כשהחידה פתוחה");
    }


}
