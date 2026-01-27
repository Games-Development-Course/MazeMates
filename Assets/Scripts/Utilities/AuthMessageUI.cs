using TMPro;
using UnityEngine;

namespace MazeMates.Authentication
{
    public class AuthMessageUI : MonoBehaviour
    {
        public enum MessageKind { Info, Success, Error }

        [Header("UI")]
        [Tooltip("הפאנל/אובייקט שצריך להידלק. אם ריק, נשתמש ב-gameObject של הסקריפט.")]
        [SerializeField] private GameObject root;

        [Tooltip("TMP שמציג את ההודעה")]
        [SerializeField] private TMP_Text label;

        [Header("Colors")]
        [SerializeField] private Color infoColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color successColor = new Color(0.2f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color errorColor = new Color(0.95f, 0.2f, 0.2f, 1f);

        [Header("Behavior")]
        [Tooltip("אם true: כשמציגים הודעה, מכבים אוטומטית אחרי זמן. 0 = לא מכבה.")]
        [SerializeField] private float autoHideSeconds = 0f;

        private float _hideAt;

        private void Awake()
        {
            Hide();
        }

        private void Update()
        {
            if (autoHideSeconds > 0f && IsVisible() && Time.unscaledTime >= _hideAt)
                Hide();
        }

        public void ShowInfo(string msg) => Show(msg, MessageKind.Info);
        public void ShowSuccess(string msg) => Show(msg, MessageKind.Success);
        public void ShowError(string msg) => Show(msg, MessageKind.Error);

        public void Show(string msg, MessageKind kind)
        {
            if (string.IsNullOrWhiteSpace(msg))
            {
                Hide();
                return;
            }

            SetVisible(true);

            if (label != null)
            {
                label.text = msg;
                label.color = kind switch
                {
                    MessageKind.Success => successColor,
                    MessageKind.Error => errorColor,
                    _ => infoColor
                };

                // הודעות בעברית – תמיד RTL נוח יותר פה.
                // אם תרצה אוטומטי לפי הטקסט, תגיד ואשנה.
                label.isRightToLeftText = true;
                label.alignment = TextAlignmentOptions.MidlineRight;
            }
            else
            {
                Debug.LogWarning("[AuthMessageUI] label is NULL");
            }

            if (autoHideSeconds > 0f)
                _hideAt = Time.unscaledTime + autoHideSeconds;

            Debug.Log($"[AuthMessageUI] Show kind={kind} root={(GetRoot()?.name ?? "NULL")} active={IsVisible()} msg={msg}");
        }

        public void Hide()
        {
            SetVisible(false);
            if (label != null) label.text = "";
        }

        public bool IsVisible()
        {
            var r = GetRoot();
            return r != null && r.activeSelf;
        }

        private void SetVisible(bool v)
        {
            var r = GetRoot();
            if (r != null) r.SetActive(v);
        }

        private GameObject GetRoot() => root != null ? root : gameObject;
    }
}
