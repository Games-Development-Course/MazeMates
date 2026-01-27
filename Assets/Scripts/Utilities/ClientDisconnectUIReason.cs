// Assets/Scripts/Utilities/ClientDisconnectUIReason.cs
using TMPro;
using UnityEngine;
using MazeMates.Authentication;

public class ClientDisconnectUIReason : MonoBehaviour
{
    [SerializeField] private TMP_Text reasonText;

    private void OnEnable()
    {
        // ✅ רק נרשמים לאירוע
        if (UgsAuthManager.Instance != null)
            UgsAuthManager.Instance.AuthError += OnAuthError;
    }

    private void OnDisable()
    {
        if (UgsAuthManager.Instance != null)
            UgsAuthManager.Instance.AuthError -= OnAuthError;
    }

    private void OnAuthError(string msg)
    {
        if (reasonText != null)
            reasonText.text = msg ?? "";

        Debug.LogWarning($"[ClientDisconnectUIReason] AuthError: {msg}");
    }

    // אם אתה רוצה להציג "סיבה לניתוק" באופן ידני:
    public void SetReason(string msg)
    {
        if (reasonText != null)
            reasonText.text = msg ?? "";
    }
}
