using System.Collections;
using TMPro;
using UnityEngine;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance;

    public TravellerHUD Traveller;
    public NavigatorHUD Navigator;

    [Header("Message Settings")]
    public float defaultMessageDuration = 2f;
    public float HideDuration = 0.6f;

    private void Awake()
    {
        Instance = this;
    }

    // ====================================================================
    // INTERNAL — SHOW MESSAGE + Hide
    // ====================================================================

    private void ShowAndHide(string travellerMsg, string navigatorMsg, float duration)
    {
        if (!string.IsNullOrEmpty(travellerMsg))
            Traveller?.ShowMessage(travellerMsg);

        if (!string.IsNullOrEmpty(navigatorMsg))
            Navigator?.ShowMessage(navigatorMsg);

        StopAllCoroutines();
        StartCoroutine(HideMessagesAfter(duration));
    }

    private IEnumerator HideMessagesAfter(float t)
    {
        yield return new WaitForSeconds(t);

        // Hide out
        float duration = 0.35f;
        float time = 0f;

        var travellerText = Traveller?.messageText; // TMP/Text component
        var navigatorText = Navigator?.messageText;

        Color tColor = travellerText != null ? travellerText.color : Color.white;
        Color nColor = navigatorText != null ? navigatorText.color : Color.white;

        while (time < duration)
        {
            float a = Mathf.Lerp(1f, 0f, time / duration);

            if (travellerText)
                travellerText.color = new Color(tColor.r, tColor.g, tColor.b, a);
            if (navigatorText)
                navigatorText.color = new Color(nColor.r, nColor.g, nColor.b, a);

            time += Time.deltaTime;
            yield return null;
        }

        if (travellerText)
            travellerText.text = "";
        if (navigatorText)
            navigatorText.text = "";

        // Restore alpha for next message
        if (travellerText)
            travellerText.color = new Color(tColor.r, tColor.g, tColor.b, 1f);
        if (navigatorText)
            navigatorText.color = new Color(nColor.r, nColor.g, nColor.b, 1f);
    }

    // ====================================================================
    // NEW API
    // ====================================================================

    public void ShowMessageForTraveller(string msg)
    {
        ShowAndHide(msg, null, defaultMessageDuration);
    }

    public void ShowMessageForNavigator(string msg)
    {
        ShowAndHide(null, msg, defaultMessageDuration);
    }

    public void ShowMessageForBoth(string msg)
    {
        ShowAndHide(msg, msg, defaultMessageDuration);
    }

    public void UpdateHUD()
    {
        var gm = GameManager.Instance;

        Traveller?.UpdateShared(gm);
        Navigator?.UpdateShared(gm);
    }

    public void FlashTravellerLife()
    {
        Traveller?.FlashLives();
    }

    public void ShowPuzzle(Sprite navigatorSprite)
    {
        Traveller?.ShowPuzzle();
        Navigator?.ShowPuzzleImage(navigatorSprite);
    }

    public void HidePuzzle()
    {
        Traveller?.HidePuzzle();
        Navigator?.HidePuzzleImage();
    }

    // ====================================================================
    // OLD API COMPAT
    // ====================================================================

    public void ShowMessageToTraveller(string msg) => ShowMessageForTraveller(msg);

    public void ShowMessageToNavigator(string msg) => ShowMessageForNavigator(msg);

    public void UpdateHUDs() => UpdateHUD();

    public void FlashLifeIcons() => FlashTravellerLife();

    public TravellerHUD TravellerHUD => Traveller;
    public NavigatorHUD NavigatorHUD => Navigator;

    public void SetMessageAppearanceForBoth(Color c, float dur)
    {
        Traveller?.SetMessageColor(c);
        Navigator?.SetMessageColor(c);

        StopAllCoroutines();
        StartCoroutine(HideMessagesAfter(dur));
    }
}
