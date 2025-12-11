using UnityEngine;
using TMPro;
using System.Collections;

public class TutorialHUD : MonoBehaviour
{
    [Header("Tutorial Message")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private GameObject messageRoot; // הבועה עצמה (Image + Layout)

    [SerializeField] private float messageDuration = 999f;

    private Color defaultColor;

    private void Awake()
    {
        // Auto-find TMP
        if (messageText == null)
            messageText = GetComponentInChildren<TextMeshProUGUI>(true);

        // Auto-find bubble root
        if (messageRoot == null)
        {
            if (messageText != null)
                messageRoot = messageText.transform.parent.gameObject;
            else
                Debug.LogWarning("TutorialHUD: messageRoot לא הוגדר!");
        }

        if (messageText != null)
            defaultColor = messageText.color;

        // אל תציג את החלון בהתחלה
        if (messageRoot != null)
            messageRoot.SetActive(false);
    }

    public void ShowMessage(string message)
    {
        if (messageText == null) return;

        // הפעלה
        messageRoot.SetActive(true);

        messageText.isRightToLeftText = true;
        messageText.alignment = TextAlignmentOptions.Right;
        messageText.color = defaultColor;

        messageText.text = message;

        StopAllCoroutines();
        StartCoroutine(HideAfterDelay());
    }

    public void ShowSuccess(string message = "מעולה!")
    {
        if (messageText == null) return;

        messageRoot.SetActive(true);

        messageText.isRightToLeftText = true;
        messageText.alignment = TextAlignmentOptions.Right;

        // צבע הצלחה
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
