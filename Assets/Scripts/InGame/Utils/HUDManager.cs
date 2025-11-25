using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance;

    public HUD TravellerHUD;
    public HUD NavigatorHUD;


    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ודא שהתמונה כבויה בתחילת המשחק

    }

    // ---------------------------------------------------------
    // עדכון HUD לשני השחקנים
    // ---------------------------------------------------------
    public void UpdateHUDs()
    {
        if (TravellerHUD != null)
            TravellerHUD.UpdateHUD();

        if (NavigatorHUD != null)
            NavigatorHUD.UpdateHUD();
    }

    // ---------------------------------------------------------
    // הודעה לשני המסכים
    // ---------------------------------------------------------
    public void ShowMessageForBoth(string msg, float duration = 3f)
    {
        if (TravellerHUD != null)
            TravellerHUD.ShowMessage(msg, duration);

        if (NavigatorHUD != null)
            NavigatorHUD.ShowMessage(msg, duration);
    }

    // ---------------------------------------------------------
    // שינוי פונט/צבע לשני המסכים
    // ---------------------------------------------------------
    public void SetMessageAppearanceForBoth(Color color, TMP_FontAsset font)
    {
        if (TravellerHUD != null)
            TravellerHUD.SetMessageAppearance(color, font);

        if (NavigatorHUD != null)
            NavigatorHUD.SetMessageAppearance(color, font);
    }

    // ---------------------------------------------------------
    // פלאש בחיים
    // ---------------------------------------------------------
    public void FlashLifeIcons()
    {
        if (TravellerHUD != null)
            TravellerHUD.FlashLifeIcon();

        if (NavigatorHUD != null)
            NavigatorHUD.FlashLifeIcon();
    }

    // ---------------------------------------------------------
    // הודעה למטייל בלבד
    // ---------------------------------------------------------
    public void ShowMessageToTraveller(string msg, float duration = 3f)
    {
        if (TravellerHUD != null)
            TravellerHUD.ShowMessage(msg, duration);
    }

    // ---------------------------------------------------------
    // הודעה לנווט בלבד
    // ---------------------------------------------------------
    public void ShowMessageToNavigator(string msg, float duration = 3f)
    {
        if (NavigatorHUD != null)
            NavigatorHUD.ShowMessage(msg, duration);
    }

    // ---------------------------------------------------------
    // הצגת תמונת חידה מלאה אצל הנווט
    // ---------------------------------------------------------

    // ---------------------------------------------------------
    // הסתרת תמונת החידה אצל הנווט
    // ---------------------------------------------------------
}
