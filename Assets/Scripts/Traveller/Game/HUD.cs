using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text livesText;
    public TMP_Text keysText;
    public TMP_Text messageText;
    public TMP_Text lifebuoyText;

    [Header("Icons")]
    public Image lifeIcon;
    public Image lifebuoyIcon;   //  הוסף כאן ספרייט של גלגל הצלה

    private void Start()
    {
        if (messageText != null)
            messageText.gameObject.SetActive(false);

        UpdateHUD();
    }

    // ---------------------------------------------------------
    // UPDATE HUD (LIVES + KEYS)
    // ---------------------------------------------------------
    public void UpdateHUD()
    {
        livesText.text = "x " + GameManager.Instance.lives;
        keysText.text = "x " + GameManager.Instance.keys;
        lifebuoyText.text = "x " + GameManager.Instance.lifebuoys;

        if (lifebuoyIcon != null)
            lifebuoyIcon.enabled =true;
    }

    // ---------------------------------------------------------
    // MESSAGE APPEARANCE
    // ---------------------------------------------------------
    public void SetMessageAppearance(Color color, TMP_FontAsset font)
    {
        if (font != null)
            messageText.font = font;

        messageText.color = color;

        var mat = messageText.fontMaterial;

        if (mat.HasProperty("_FaceColor"))
            mat.SetColor("_FaceColor", color);

        if (mat.HasProperty("_OutlineColor"))
            mat.SetColor("_OutlineColor", Color.clear);
    }

    // ---------------------------------------------------------
    // SHOW MESSAGE
    // ---------------------------------------------------------
    public void ShowMessage(string msg, float duration = 3f)
    {
        StopAllCoroutines();
        StartCoroutine(ShowMessageRoutine(msg, duration));
    }

    private IEnumerator ShowMessageRoutine(string msg, float duration)
    {
        messageText.text = msg;
        messageText.gameObject.SetActive(true);

        yield return new WaitForSeconds(duration);

        messageText.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------
    // FLASH LIFE ICON
    // ---------------------------------------------------------
    public void FlashLifeIcon()
    {
        StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        if (lifeIcon == null)
            yield break;

        Color original = lifeIcon.color;

        for (int i = 0; i < 5; i++)
        {
            lifeIcon.color = new Color(original.r, original.g, original.b, 0f);
            yield return new WaitForSeconds(0.2f);

            lifeIcon.color = original;
            yield return new WaitForSeconds(0.2f);
        }
    }
}
