using UnityEngine;
using TMPro;

public class TutorialHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;

    public void ShowMessage(string message)
    {
        if (messageText == null)
            return;

        messageText.text = message;
        messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    public void Clear()
    {
        if (messageText == null)
            return;

        messageText.text = string.Empty;
        messageText.gameObject.SetActive(false);
    }
}
