using System;
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

        [Header("Player Status UI")]
        [SerializeField] private TMP_Text playerStatusText;

        private void Start()
        {
            if (messageText != null) messageText.text = "";

            // Default: guest
            SetPlayerStatus(null, isGuest: true);

            loginButton.onClick.AddListener(OnLoginClicked);
            registerButton.onClick.AddListener(OnRegisterClicked);

            // ✅ Dynamic LTR/RTL for username input (typing behavior)
            if (usernameInput != null)
            {
                usernameInput.onValueChanged.AddListener(OnUsernameChanged);
                usernameInput.onSelect.AddListener(OnUsernameSelected);

                ApplyUsernameInputDirection(usernameInput.text);
            }

            if (UgsAuthManager.Instance != null)
                UgsAuthManager.Instance.AuthError += ShowMessage;
        }

        private void OnDestroy()
        {
            if (usernameInput != null)
            {
                usernameInput.onValueChanged.RemoveListener(OnUsernameChanged);
                usernameInput.onSelect.RemoveListener(OnUsernameSelected);
            }

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

            if (UgsAuthManager.Instance.IsSignedIn)
            {
                ShowMessage("");
                SetPlayerStatus(u, isGuest: false);
            }
            else
            {
                SetInteractable(true);
                SetPlayerStatus(null, isGuest: true);
            }
        }

        private async void OnRegisterClicked()
        {
            SetInteractable(false);
            ShowMessage("Creating account...");

            var u = usernameInput.text?.Trim();
            var p = "MazeMates1234!";

            await UgsAuthManager.Instance.RegisterWithUsernamePasswordAsync(u, p);

            if (UgsAuthManager.Instance.IsSignedIn)
            {
                ShowMessage("");
                SetPlayerStatus(u, isGuest: false);
            }
            else
            {
                SetInteractable(true);
                SetPlayerStatus(null, isGuest: true);
            }
        }

        public void SetGuestConnected()
        {
            SetPlayerStatus(null, isGuest: true);
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

        // -------------------- Username Input: keep normal typing --------------------

        private void OnUsernameChanged(string value)
        {
            ApplyUsernameInputDirection(value);
        }

        private void OnUsernameSelected(string _)
        {
            ApplyUsernameInputDirection(usernameInput.text);
            FixCaretForCurrentDirection();
        }

        private void ApplyUsernameInputDirection(string text)
        {
            if (usernameInput == null || usernameInput.textComponent == null) return;

            bool rtl = ContainsRTL(text);

            // Hebrew -> RTL + right align
            // English -> LTR + left align
            usernameInput.textComponent.isRightToLeftText = rtl;
            usernameInput.textComponent.alignment = rtl
                ? TextAlignmentOptions.MidlineRight
                : TextAlignmentOptions.MidlineLeft;

            if (usernameInput.placeholder is TMP_Text ph)
            {
                ph.isRightToLeftText = rtl;
                ph.alignment = usernameInput.textComponent.alignment;
            }
        }

        private void FixCaretForCurrentDirection()
        {
            if (usernameInput == null) return;

            bool rtl = usernameInput.textComponent != null && usernameInput.textComponent.isRightToLeftText;

            int pos = rtl
                ? (usernameInput.text != null ? usernameInput.text.Length : 0) // Hebrew: caret at end
                : 0; // English: caret far left

            usernameInput.caretPosition = pos;
            usernameInput.selectionAnchorPosition = pos;
            usernameInput.selectionFocusPosition = pos;

            usernameInput.ForceLabelUpdate();
        }

        // -------------------- Player Status: DO NOT change alignment --------------------

        private void SetPlayerStatus(string username, bool isGuest)
        {
            if (playerStatusText == null) return;

            string name = isGuest ? "אורח" : (string.IsNullOrWhiteSpace(username) ? "אורח" : username.Trim());

            // לא נוגעים ב-alignment בכלל. הוא נשאר Center כמו שהגדרת באינספקטור.
            // לא נוגעים גם ב-isRightToLeftText של ה-playerStatusText.

            string shownName = ContainsRTL(name) ? name : ReverseSimple(name);

            playerStatusText.text = $"שחקן {shownName} מחובר";
        }

        private static string ReverseSimple(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            char[] arr = s.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        private static bool ContainsRTL(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;

            foreach (char c in s)
            {
                if ((c >= '\u0590' && c <= '\u05FF') || // Hebrew
                    (c >= '\u0600' && c <= '\u06FF') || // Arabic
                    (c >= '\u0750' && c <= '\u077F') || // Arabic Supplement
                    (c >= '\u08A0' && c <= '\u08FF'))   // Arabic Extended-A
                {
                    return true;
                }
            }
            return false;
        }
    }
}
