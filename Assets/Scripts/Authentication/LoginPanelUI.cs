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

        [Header("Roots (Auth/Lobby switch)")]
        [SerializeField] private GameObject authRoot;
        [SerializeField] private GameObject lobbyRoot;


        private bool _wired;

        private void Awake()
        {
            WireOnce();
        }

        private void Start()
        {
            // UI initial state
            if (messageText != null) messageText.text = "";
            SetPlayerStatus(null, isGuest: true);
        }

        private void OnEnable()
        {
            // טוב שיש, אבל לא מסתמכים רק על זה (כי לפעמים AuthRoot לא נכבה)
            ResetForLogoutUI();
            HookAuthEvents(true);
        }

        private void OnDisable()
        {
            HookAuthEvents(false);
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
            if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);

            if (usernameInput != null)
            {
                usernameInput.onValueChanged.AddListener(OnUsernameChanged);
                usernameInput.onSelect.AddListener(OnUsernameSelected);
                ApplyUsernameInputDirection(usernameInput.text);
            }
        }

        private void HookAuthEvents(bool enable)
        {
            if (UgsAuthManager.Instance == null) return;

            if (enable)
            {
                UgsAuthManager.Instance.AuthError -= ShowMessage;
                UgsAuthManager.Instance.AuthError += ShowMessage;

                UgsAuthManager.Instance.SignedOut -= HandleSignedOut;
                UgsAuthManager.Instance.SignedOut += HandleSignedOut;
            }
            else
            {
                UgsAuthManager.Instance.AuthError -= ShowMessage;
                UgsAuthManager.Instance.SignedOut -= HandleSignedOut;
            }
        }

        private void HandleSignedOut()
        {
            ResetForLogoutUI();
        }

        // ✅ תקרא לזה תמיד אחרי Logout (גם אם OnEnable לא רץ)
        public void ResetForLogoutUI()
        {
            SetInteractable(true);
            ShowMessage("");

            if (usernameInput != null)
            {
                usernameInput.text = "";
                ApplyUsernameInputDirection(usernameInput.text);
                FixCaretForCurrentDirection();
            }

            SetPlayerStatus(null, isGuest: true);
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
                // לא מחזירים interactable כאן בכוונה (כי עוברים ללובי)
                if (lobbyRoot != null) lobbyRoot.SetActive(true);
                if (authRoot != null) authRoot.SetActive(false);

                var relay = FindFirstObjectByType<RelayUIController>();
                if (relay != null)
                    relay.ResetLobbyUiToIdle();

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
                if (lobbyRoot != null) lobbyRoot.SetActive(true);
                if (authRoot != null) authRoot.SetActive(false);

                var relay = FindFirstObjectByType<RelayUIController>();
                if (relay != null)
                    relay.ResetLobbyUiToIdle();

            }
            else
            {
                SetInteractable(true);
                SetPlayerStatus(null, isGuest: true);
            }
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

        // -------------------- Username Input --------------------

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
                ? (usernameInput.text != null ? usernameInput.text.Length : 0)
                : 0;

            usernameInput.caretPosition = pos;
            usernameInput.selectionAnchorPosition = pos;
            usernameInput.selectionFocusPosition = pos;
            usernameInput.ForceLabelUpdate();
        }

        // -------------------- Player Status --------------------

        private void SetPlayerStatus(string username, bool isGuest)
        {
            if (playerStatusText == null) return;

            string name = isGuest ? "אורח" : (string.IsNullOrWhiteSpace(username) ? "אורח" : username.Trim());
            string shownName = ContainsRTL(name) ? name : ReverseSimple(name);

            playerStatusText.text = $"{shownName} מחובר";
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
                if ((c >= '\u0590' && c <= '\u05FF') ||
                    (c >= '\u0600' && c <= '\u06FF') ||
                    (c >= '\u0750' && c <= '\u077F') ||
                    (c >= '\u08A0' && c <= '\u08FF'))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
