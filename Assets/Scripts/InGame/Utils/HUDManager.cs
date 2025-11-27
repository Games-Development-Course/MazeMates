using UnityEngine;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance;

    public TravellerHUD Traveller;
    public NavigatorHUD Navigator;

    private void Awake()
    {
        Instance = this;
    }

    // ---------------------------------------------------
    // NEW API
    // ---------------------------------------------------

    public void ShowMessageForTraveller(string msg)
    {
        Traveller?.ShowMessage(msg);
    }

    public void ShowMessageForNavigator(string msg)
    {
        Navigator?.ShowMessage(msg);
    }

    public void ShowMessageForBoth(string msg)
    {
        Traveller?.ShowMessage(msg);
        Navigator?.ShowMessage(msg);
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

    // ---------------------------------------------------
    // BACKWARD COMPATIBILITY (OLD API)
    // ---------------------------------------------------

    // old: ShowMessageToTraveller
    public void ShowMessageToTraveller(string msg)
    {
        ShowMessageForTraveller(msg);
    }

    // old: ShowMessageToNavigator
    public void ShowMessageToNavigator(string msg)
    {
        ShowMessageForNavigator(msg);
    }

    // old: UpdateHUDs
    public void UpdateHUDs()
    {
        UpdateHUD();
    }

    // old: FlashLifeIcons
    public void FlashLifeIcons()
    {
        FlashTravellerLife();
    }

    // old: TravellerHUD property
    public TravellerHUD TravellerHUD => Traveller;

    // old: NavigatorHUD property
    public NavigatorHUD NavigatorHUD => Navigator;

    // old: SetMessageAppearanceForBoth(Color, float)
    public void SetMessageAppearanceForBoth(Color c, float duration)
    {
        Traveller?.SetMessageColor(c);
        Navigator?.SetMessageColor(c);

        StartCoroutine(HideMessagesAfter(duration));
    }

    private System.Collections.IEnumerator HideMessagesAfter(float t)
    {
        yield return new WaitForSeconds(t);
        Traveller?.ShowMessage("");
        Navigator?.ShowMessage("");
    }
}
