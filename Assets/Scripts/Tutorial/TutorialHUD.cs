using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class TutorialHUD : MonoBehaviour
{
    [Header("Tutorial Message")]
    [SerializeField] private TextMeshProUGUI messageText;   // הטקסט עצמו
    [SerializeField] private GameObject messageRoot;        // הבועה (Container עם Image)
    [SerializeField] private Image bubbleImage;             // ה-Image של הבועה (מומלץ)

    [SerializeField] private float messageDuration = 999f;
    [SerializeField] private bool hideOnStartButKeepActive = true; // מסתיר בלי לכבות SetActive

    private Color defaultColor;

    private void Awake()
    {
        WireIfNeeded();

        if (messageText != null)
            defaultColor = messageText.color;

        if (hideOnStartButKeepActive)
            SetVisible(false);
    }

    private void OnEnable()
    {
        WireIfNeeded();
    }

    private void WireIfNeeded()
    {
        if (messageText == null)
            messageText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (messageRoot == null && messageText != null)
        {
            // קח את ההורה הראשון שיש עליו Image (הבועה)
            var img = messageText.GetComponentInParent<Image>(true);
            messageRoot = (img != null) ? img.gameObject : messageText.gameObject;
        }

        if (bubbleImage == null && messageRoot != null)
            bubbleImage = messageRoot.GetComponent<Image>();
    }

    private void SetVisible(bool visible)
    {
        WireIfNeeded();

        if (messageRoot == null) return;

        // אל תכבה SetActive כדי לא לשבור רפרנסים/לייאאוט, רק תסתיר
        var cg = messageRoot.GetComponent<CanvasGroup>();
        if (cg == null) cg = messageRoot.AddComponent<CanvasGroup>();

        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;

        if (bubbleImage != null)
            bubbleImage.enabled = true;

        if (messageText != null)
            messageText.enabled = true;
    }

    public void ShowMessage(string message)
    {
        WireIfNeeded();
        if (messageText == null || messageRoot == null) return;

        messageText.isRightToLeftText = true;
        messageText.alignment = TextAlignmentOptions.Right;
        messageText.enableWordWrapping = true;

        messageText.text = message ?? "";

        // קריטי: להדליק הכל
        messageRoot.SetActive(true);
        if (bubbleImage != null) bubbleImage.enabled = true;
        messageText.enabled = true;

        SetVisible(true);

        StopAllCoroutines();
        StartCoroutine(ForceLayoutNextFrame());
    }

    private IEnumerator ForceLayoutNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (messageText != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(messageText.rectTransform);
        if (messageRoot != null)
        {
            var rt = messageRoot.GetComponent<RectTransform>();
            if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    public void ShowSuccess(string message = "מעולה!")
    {
        ShowMessage(message);
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
        {
            messageText.text = "";
            messageText.color = defaultColor;
        }

        if (hideOnStartButKeepActive)
            SetVisible(false);
        else if (messageRoot != null)
            messageRoot.SetActive(false);
    }
}
