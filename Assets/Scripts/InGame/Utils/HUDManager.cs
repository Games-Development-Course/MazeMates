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

        // ��� ������� ����� ������ �����
    }

    // ---------------------------------------------------------
    // ����� HUD ���� �������
    // ---------------------------------------------------------
    public void UpdateHUDs()
    {
        if (TravellerHUD != null)
            TravellerHUD.UpdateHUD();

        if (NavigatorHUD != null)
            NavigatorHUD.UpdateHUD();
    }

    // ---------------------------------------------------------
    // ����� ���� ������
    // ---------------------------------------------------------
    public void ShowMessageForBoth(string msg, float duration = 3f)
    {
        if (TravellerHUD != null)
            TravellerHUD.ShowMessage(msg, duration);

        if (NavigatorHUD != null)
            NavigatorHUD.ShowMessage(msg, duration);
    }

    // ---------------------------------------------------------
    // ����� ����/��� ���� ������
    // ---------------------------------------------------------
    public void SetMessageAppearanceForBoth(Color color, TMP_FontAsset font)
    {
        if (TravellerHUD != null)
            TravellerHUD.SetMessageAppearance(color, font);

        if (NavigatorHUD != null)
            NavigatorHUD.SetMessageAppearance(color, font);
    }

    // ---------------------------------------------------------
    // ���� �����
    // ---------------------------------------------------------
    public void FlashLifeIcons()
    {
        if (TravellerHUD != null)
            TravellerHUD.FlashLifeIcon();

        if (NavigatorHUD != null)
            NavigatorHUD.FlashLifeIcon();
    }

    // ---------------------------------------------------------
    // ����� ������ ����
    // ---------------------------------------------------------
    public void ShowMessageToTraveller(string msg, float duration = 3f)
    {
        if (TravellerHUD != null)
            TravellerHUD.ShowMessage(msg, duration);
    }

    // ---------------------------------------------------------
    // ����� ����� ����
    // ---------------------------------------------------------
    public void ShowMessageToNavigator(string msg, float duration = 3f)
    {
        if (NavigatorHUD != null)
            NavigatorHUD.ShowMessage(msg, duration);
    }

    // ---------------------------------------------------------
    // ���� ����� ���� ���� ��� �����
    // ---------------------------------------------------------

    // ---------------------------------------------------------
    // ����� ����� ����� ��� �����
    // ---------------------------------------------------------
}
