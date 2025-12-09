using UnityEngine;
using TMPro;
using System.Collections;

public class TutorialHUD : MonoBehaviour
{
    [Header("Tutorial Message")]
    [SerializeField] private TextMeshProUGUI messageText;

    [SerializeField] private GameObject messageRoot;

    [SerializeField] private float messageDuration = 999f;

    private Color defaultColor;

    private void Awake()
    {
        // ניסיון אוטומטי למצוא את הרכיבים
        if (messageText == null)
            messageText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (messageRoot == null && messageText != null)
            messageRoot = messageText.gameObject;

        if (messageText != null)
            defaultColor = messageText.color;   // שמירת הצבע המקורי
    }

    /// <summary>
    /// הצגת הודעה רגילה
    /// </summary>
    public void ShowMessage(string message)
    {
        if (messageText == null)
            return;

        // הפעלה
        messageRoot.SetActive(true);

        // RTL + ALIGN RIGHT
        messageText.isRightToLeftText = true;
        messageText.alignment = TextAlignmentOptions.Right;

        // צבע רגיל
        messageText.color = defaultColor;

        // טקסט
        messageText.text = message;

        StopAllCoroutines();
        StartCoroutine(HideAfterDelay());
    }

    /// <summary>
    /// הצגת הודעת הצלחה (מעולה!)
    /// </summary>
    public void ShowSuccess(string message = "מעולה!")
    {
        if (messageText == null)
            return;

        messageRoot.SetActive(true);

        messageText.isRightToLeftText = true;
        messageText.alignment = TextAlignmentOptions.Right;

        // ירוק יפה
        messageText.color = new Color(0.2f, 1f, 0.2f);

        messageText.text = message;

        StopAllCoroutines();
        StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(messageDuration);
        Clear();
    }

    public void Clear()
    {
        if (messageText != null)
            messageText.text = "";

        if (messageRoot != null)
            messageRoot.SetActive(false);
    }
}
