using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MazeMates.Authentication
{
    public class LoginPanelUI : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private TMP_InputField usernameInput;

        [Header("Buttons")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;

        [Header("UI")]
        [SerializeField] private TMP_Text messageText;

        private void Start()
        {
            if (messageText != null) messageText.text = "";

            loginButton.onClick.AddListener(OnLoginClicked);
            registerButton.onClick.AddListener(OnRegisterClicked);

            if (UgsAuthManager.Instance != null)
                UgsAuthManager.Instance.AuthError += ShowMessage;
        }

        private void OnDestroy()
        {
            if (UgsAuthManager.Instance != null)
                UgsAuthManager.Instance.AuthError -= ShowMessage;
        }

        private async void OnLoginClicked()
        {
            SetInteractable(false);
            ShowMessage("Signing in...");

            var u = usernameInput.text?.Trim();
            var p = "MazeMates1234!";

            await UgsAuthManager.Instance.SignInWithUsernamePasswordAsync(u, p);

            // אם הצליח, AuthGateUI כבר יחליף למסך Lobby דרך האירוע SignedIn
            if (UgsAuthManager.Instance.IsSignedIn) ShowMessage("");
            else SetInteractable(true);
        }

        private async void OnRegisterClicked()
        {
            SetInteractable(false);
            ShowMessage("Creating account...");

            var u = usernameInput.text?.Trim();
            var p = "MazeMates1234!";

            await UgsAuthManager.Instance.RegisterWithUsernamePasswordAsync(u, p);

            if (UgsAuthManager.Instance.IsSignedIn) ShowMessage("");
            else SetInteractable(true);
        }

        private void SetInteractable(bool value)
        {
            if (loginButton != null) loginButton.interactable = value;
            if (registerButton != null) registerButton.interactable = value;
            if (usernameInput != null) usernameInput.interactable = value;
        }

        private void ShowMessage(string msg)
        {
            if (messageText != null) messageText.text = msg;
        }
    }
}
