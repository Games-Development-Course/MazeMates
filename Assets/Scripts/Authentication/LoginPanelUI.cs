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

        [Header("Auth Message Panel")]
        [SerializeField] private AuthMessageUI authMessageUI;

        [Header("Roots (Auth/Lobby switch)")]
        [SerializeField] private GameObject authRoot;
        [SerializeField] private GameObject lobbyRoot;

        [Header("Lobby status UI (optional)")]
        [Tooltip("אפשר להשאיר ריק. אם ריק/לא נכון - הסקריפט יחפש בתוך lobbyRoot בזמן ריצה.")]
        [SerializeField] private PlayerStatusUI lobbyPlayerStatusUI;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;

        private bool _wired;

        private void Awake() => WireOnce();

        private void Start()
        {
            authMessageUI?.Hide();
        }

        private void OnEnable()
        {
            ResetForLogoutUI();
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
            if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);

            if (usernameInput != null)
            {
                usernameInput.onValueChanged.AddListener(_ => ApplyUsernameInputDirection(usernameInput.text));
                usernameInput.onSelect.AddListener(_ => { ApplyUsernameInputDirection(usernameInput.text); FixCaretForCurrentDirection(); });
                ApplyUsernameInputDirection(usernameInput.text);
            }
        }

        public void ResetForLogoutUI()
        {
            SetInteractable(true);
            authMessageUI?.Hide();

            if (usernameInput != null)
            {
                usernameInput.text = "";
                ApplyUsernameInputDirection(usernameInput.text);
                FixCaretForCurrentDirection();
            }

            // לא חובה, אבל בסדר:
            ResolveLobbyStatusUI()?.Clear();
        }

        private async void OnLoginClicked()
        {
            string u = usernameInput != null ? usernameInput.text?.Trim() : null;
            string p = "MazeMates1234!";

            authMessageUI?.ShowInfo("מתחבר...");
            SetInteractable(false);

            bool ok = await UgsAuthManager.Instance.SignInWithUsernamePasswordAsync(u, p);
            if (!ok)
            {
                SetInteractable(true);
                authMessageUI?.ShowError(TranslateAuthError(UgsAuthManager.Instance.LastErrorRaw));
                if (verboseLogs) Debug.Log($"[Login] FAIL raw=\n{UgsAuthManager.Instance.LastErrorRaw}");
                return;
            }

            authMessageUI?.ShowSuccess("התחברת בהצלחה!");

            // ✅ קודם מדליקים לובי
            if (lobbyRoot != null) lobbyRoot.SetActive(true);
            if (authRoot != null) authRoot.SetActive(false);

            // ✅ מחכים פריים כדי ש-OnEnable של הלובי ירוץ
            await System.Threading.Tasks.Task.Yield();

            // ✅ ואז מעדכנים סטטוס
            var status = ResolveLobbyStatusUI();
            if (status != null) status.SetUser(u);
            else Debug.LogError("[Login] PlayerStatusUI not found under lobbyRoot + not assigned.");

            if (verboseLogs) Debug.Log($"[Login] OK | status={(status ? status.name : "<NULL>")} lobbyRootActive={lobbyRoot?.activeInHierarchy}");

            var relay = FindFirstObjectByType<RelayUIController>();
            if (relay != null) relay.ResetLobbyUiToIdle();
        }

        private async void OnRegisterClicked()
        {
            string u = usernameInput != null ? usernameInput.text?.Trim() : null;
            string p = "MazeMates1234!";

            authMessageUI?.ShowInfo("יוצר משתמש...");
            SetInteractable(false);

            bool ok = await UgsAuthManager.Instance.RegisterWithUsernamePasswordAsync(u, p);
            if (!ok)
            {
                SetInteractable(true);
                authMessageUI?.ShowError(TranslateAuthError(UgsAuthManager.Instance.LastErrorRaw));
                if (verboseLogs) Debug.Log($"[Register] FAIL raw=\n{UgsAuthManager.Instance.LastErrorRaw}");
                return;
            }

            authMessageUI?.ShowSuccess("נרשמת בהצלחה!");

            if (lobbyRoot != null) lobbyRoot.SetActive(true);
            if (authRoot != null) authRoot.SetActive(false);

            await System.Threading.Tasks.Task.Yield();

            var status = ResolveLobbyStatusUI();
            if (status != null) status.SetUser(u);
            else Debug.LogError("[Register] PlayerStatusUI not found under lobbyRoot + not assigned.");

            if (verboseLogs) Debug.Log($"[Register] OK | status={(status ? status.name : "<NULL>")} lobbyRootActive={lobbyRoot?.activeInHierarchy}");

            var relay = FindFirstObjectByType<RelayUIController>();
            if (relay != null) relay.ResetLobbyUiToIdle();
        }

        private PlayerStatusUI ResolveLobbyStatusUI()
        {
            // אם מחובר נכון - נשאיר
            if (lobbyPlayerStatusUI != null) return lobbyPlayerStatusUI;

            // אחרת נחפש בתוך lobbyRoot (כולל לא פעילים)
            if (lobbyRoot != null)
            {
                lobbyPlayerStatusUI = lobbyRoot.GetComponentInChildren<PlayerStatusUI>(true);
                if (verboseLogs)
                    Debug.Log($"[LoginPanelUI] ResolveLobbyStatusUI -> {(lobbyPlayerStatusUI ? lobbyPlayerStatusUI.name : "<NULL>")}");
            }

            return lobbyPlayerStatusUI;
        }

        private void SetInteractable(bool value)
        {
            if (loginButton != null) loginButton.interactable = value;
            if (registerButton != null) registerButton.interactable = value;
            if (usernameInput != null) usernameInput.interactable = value;
        }

        // -------------------- Username Input --------------------

        private void ApplyUsernameInputDirection(string text)
        {
            if (usernameInput == null || usernameInput.textComponent == null) return;

            bool rtl = ContainsRTL(text);

            usernameInput.textComponent.isRightToLeftText = rtl;
            usernameInput.textComponent.alignment = rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;

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
            int pos = rtl ? (usernameInput.text != null ? usernameInput.text.Length : 0) : 0;

            usernameInput.caretPosition = pos;
            usernameInput.selectionAnchorPosition = pos;
            usernameInput.selectionFocusPosition = pos;
            usernameInput.ForceLabelUpdate();
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
                    return true;
            }
            return false;
        }

        // -------------------- Translation: ONLY the cases you asked --------------------

        private static string TranslateAuthError(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "הפעולה נכשלה. נסה שוב.";

            string m = raw.ToUpperInvariant();

            if (m.Contains("WRONG_USERNAME_PASSWORD"))
                return "שם משתמש או סיסמה שגויים.";

            if (m.Contains("INVALID_USERNAME"))
                return "שם משתמש לא תקין. מותר אותיות באנגלית/ספרות וסימנים, בין 3 ל־10 תווים.";

            if (m.Contains("ENTITY_EXISTS"))
                return "שם המשתמש כבר קיים. נסה שם אחר או התחבר.";

            return "הפעולה נכשלה. נסה שוב.";
        }
    }
}
