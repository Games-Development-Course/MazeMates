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
    public Image lifeIcon;  // האייקון של הלב (UI Image)

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

    //  אפקט הבהוב לאחר פגיעה
    public void FlashLifeIcon()
    {
        StartCoroutine(Flash());
    }
    public void ShowMessage(string msg, float duration = 6f)
    {
        StopAllCoroutines();        // כדי שלא יישארו הודעות ישנות
        StartCoroutine(ShowMessageRoutine(msg, duration));
    }

    private IEnumerator ShowMessageRoutine(string msg, float duration)
    {
        messageText.text = msg;
        messageText.gameObject.SetActive(true);

        yield return new WaitForSeconds(duration);

        messageText.gameObject.SetActive(false);
    }


    IEnumerator Flash()
    {
        Color original = lifeIcon.color;

        for (int i = 0; i < 3; i++)
        {
            lifeIcon.color = Color.red;
            yield return new WaitForSeconds(0.1f);

            lifeIcon.color = original;
            yield return new WaitForSeconds(0.1f);
        }
    }
}
