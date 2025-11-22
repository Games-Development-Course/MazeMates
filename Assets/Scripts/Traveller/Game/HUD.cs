using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public static HUD Instance;

    [Header("UI References")]
    public TMP_Text livesText;
    public TMP_Text keysText;

    [Header("Default Values")]
    public int defaultLives = 0;
    public int defaultKeys = 0;

    [Header("Icons")]
    public Image lifeIcon;

    [Header("Messages")]
    public TMP_Text messageText;


    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        messageText.gameObject.SetActive(false);

        GameManager.Instance.lives = defaultLives;
        GameManager.Instance.keys = defaultKeys;

        UpdateHUD();
    }

    public void UpdateHUD()
    {
        livesText.text = "x " + GameManager.Instance.lives;
        keysText.text = "x " + GameManager.Instance.keys;
    }

    // ---- שלב 2: שינוי צבע ופונט ----
    public void SetMessageAppearance(Color color, TMP_FontAsset font)
    {
        if (font != null)
            messageText.font = font;

        // משנה את צבע הטקסט ברמה בסיסית
        messageText.color = color;

        // משנה את החומר האמיתי של ה-TMP (החשוב!)
        var mat = messageText.fontMaterial;

        if (mat.HasProperty("_FaceColor"))
            mat.SetColor("_FaceColor", color);

        if (mat.HasProperty("_OutlineColor"))
            mat.SetColor("_OutlineColor", Color.clear); // אם לא רוצה outline
    }

    // ---------------------------------

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
    public void HidePuzzle()
    {
        if (GameManager.Instance.inPuzzle == false)
        {
            // כאן לא סוגרים Canvas כי הדלת עצמה מנהלת אותו
        }

        // אם תרצה להוסיף UI — זה המקום
    }



    // אפקט הבהוב
    IEnumerator Flash()
    {
        // שומרים את מצב הלב (אלפא + צבע)
        Color original = lifeIcon.color;

        for (int i = 0; i < 5; i++)
        {
            // כיבוי מוחלט — הלב נעלם
            lifeIcon.color = new Color(original.r, original.g, original.b, 0f);
            yield return new WaitForSeconds(0.2f);

            // חזרה מלאה לאוריג'ינל — הלב מופיע שוב
            lifeIcon.color = original;
            yield return new WaitForSeconds(0.2f);
        }
    }


    public void FlashLifeIcon()
    {
        StartCoroutine(Flash());
    }
}
