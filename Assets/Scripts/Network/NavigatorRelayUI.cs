using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NavigatorRelayUI : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField codeInput; // "Code Input"

    [SerializeField]
    private Button startClientButton; // כפתור "StartClient"

    private void Awake()
    {
        if (codeInput != null)
        {
            codeInput.onValueChanged.AddListener(OnCodeChanged);
        }

        if (startClientButton != null)
        {
            startClientButton.interactable = false; // עד שלא הוזן קוד תקין
        }
    }

    private void OnDestroy()
    {
        if (codeInput != null)
        {
            codeInput.onValueChanged.RemoveListener(OnCodeChanged);
        }
    }

    private void OnCodeChanged(string newValue)
    {
        if (startClientButton == null)
            return;

        // כלל אצבע: קוד לא ריק ובדרך כלל 6 תווים, אפשר לשנות אם צריך
        startClientButton.interactable = IsCodeValid(newValue);
    }

    private bool IsCodeValid(string code)
    {
        code = code.Trim();
        // Relay בדרך כלל נותן join code באורך 6, אבל לא חובה.:contentReference[oaicite:6]{index=6}
        return !string.IsNullOrEmpty(code) && code.Length >= 6;
    }

    public async void OnStartClientClicked()
    {
        if (codeInput == null)
            return;

        string joinCode = codeInput.text.Trim();
        if (!IsCodeValid(joinCode))
        {
            Debug.LogWarning("[NavigatorRelayUI] Join code is invalid.");
            return;
        }

        await RelayManager.Instance.StartClientWithRelayAsync(joinCode);
    }
}
