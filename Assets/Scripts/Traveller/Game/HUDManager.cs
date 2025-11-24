using TMPro;
using UnityEngine;

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
    }

    // ---------------------------------------------------------
    // עדכון HUD לשניהם
    // ---------------------------------------------------------
    public void UpdateHUDs()
    {
        TravellerHUD?.UpdateHUD();
        NavigatorHUD?.UpdateHUD();
    }

    // ---------------------------------------------------------
    // הודעה לשניהם
    // ---------------------------------------------------------
    public void ShowMessageForBoth(string msg, float duration = 3f)
    {
        TravellerHUD?.ShowMessage(msg, duration);
        NavigatorHUD?.ShowMessage(msg, duration);
    }

    // ---------------------------------------------------------
    // שינוי צבע/פונט לשניהם
    // ---------------------------------------------------------
    public void SetMessageAppearanceForBoth(Color color, TMP_FontAsset font)
    {
        if (TravellerHUD != null)
            TravellerHUD.SetMessageAppearance(color, font);

        if (NavigatorHUD != null)
            NavigatorHUD.SetMessageAppearance(color, font);
    }

    // ---------------------------------------------------------
    // פלאש לשניהם
    // ---------------------------------------------------------
    public void FlashLifeIcons()
    {
        TravellerHUD?.FlashLifeIcon();
        NavigatorHUD?.FlashLifeIcon();
    }

    // ---------------------------------------------------------
    // אופציונלי — פונקציות להפרדה עתידית (אם תרצה)
    // ---------------------------------------------------------
    public void ShowMessageToTraveller(string msg, float duration = 3f)
    {
        TravellerHUD?.ShowMessage(msg, duration);
    }

    public void ShowMessageToNavigator(string msg, float duration = 3f)
    {
        NavigatorHUD?.ShowMessage(msg, duration);
    }
}
