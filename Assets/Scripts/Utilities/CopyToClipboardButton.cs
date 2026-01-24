using UnityEngine;
using TMPro;

public class CopyToClipboardButton : MonoBehaviour
{
    [SerializeField] private TMP_Text feedbackText; // אופציונלי: "הועתק!"
    [SerializeField] private string valueToCopy;    // הלינק/קוד

    public void SetValue(string v) => valueToCopy = v;

    public void Copy()
    {
        GUIUtility.systemCopyBuffer = valueToCopy;

        if (feedbackText != null)
        {
            feedbackText.text = "הועתק ללוח ?";
            CancelInvoke(nameof(ClearFeedback));
            Invoke(nameof(ClearFeedback), 1.5f);
        }
    }

    private void ClearFeedback()
    {
        if (feedbackText != null) feedbackText.text = "";
    }
}
