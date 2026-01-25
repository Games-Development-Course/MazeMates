using UnityEngine;
using TMPro;

public class CopyToClipboardButton : MonoBehaviour
{
    [Header("Copy Source (optional)")]
    [Tooltip("If set, we copy text from this TMP component (preferred over valueToCopy).")]
    [SerializeField] private TMP_Text sourceTMP;

    [Tooltip("Alternative: copy from an input field (preferred over valueToCopy).")]
    [SerializeField] private TMP_InputField sourceInput;

    [Header("Feedback (optional)")]
    [SerializeField] private TMP_Text feedbackText; // אופציונלי: "הועתק!"

    [Header("Fallback Value")]
    [Tooltip("Used only if no sourceTMP/sourceInput is assigned.")]
    [SerializeField] private string valueToCopy;    // הלינק/קוד

    public void SetValue(string v) => valueToCopy = v;

    public void Copy()
    {
        string textToCopy = GetTextToCopy();
        if (string.IsNullOrEmpty(textToCopy))
            return;

        GUIUtility.systemCopyBuffer = textToCopy;

        if (feedbackText != null)
        {
            feedbackText.text = "הועתק ללוח!";
            CancelInvoke(nameof(ClearFeedback));
            Invoke(nameof(ClearFeedback), 1.5f);
        }
    }

    private string GetTextToCopy()
    {
        if (sourceInput != null)
            return sourceInput.text?.Trim();

        if (sourceTMP != null)
            return sourceTMP.text?.Trim();

        return valueToCopy?.Trim();
    }

    private void ClearFeedback()
    {
        if (feedbackText != null) feedbackText.text = "";
    }
}
