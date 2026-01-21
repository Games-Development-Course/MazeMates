using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[ExecuteAlways]
public sealed class PlayerInfoHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform bubble;  // PlayerInfoPanel (האב שמכיל Border+Text)
    [SerializeField] private TMP_Text text;         // Text (TMP)

    [Header("Sizing (like BubbleAutoSize)")]
    [SerializeField] private float maxWidth = 480f;
    [SerializeField] private float minWidth = 0f;
    [SerializeField] private Vector2 padding = new Vector2(20f, 14f);

    [Header("Name Limit")]
    [SerializeField] private int maxNameChars = 10;

    private const char LTR = '\u202A';
    private const char POP = '\u202C';

    private bool _bound;

    private void OnEnable()
    {
        // במצב Play נתחבר ל-Network
        if (Application.isPlaying) Bind();
    }

    private void OnDisable()
    {
        if (Application.isPlaying) Unbind();
    }

    private void LateUpdate()
    {
        if (!bubble || !text) return;

        // בפליי: תן ל-Network לקבוע את הטקסט ואז סייזינג
        if (Application.isPlaying)
            RefreshTextFromNetIfPossible();

        // תמיד: סייזינג כמו BubbleAutoSize (גם באדיטור)
        ApplyAutoSize();
    }

    private void Bind()
    {
        if (_bound) return;
        var cfg = GameConfigNet.Instance;
        if (cfg == null || !cfg.IsSpawned) return;

        cfg.HostName.OnValueChanged += OnChanged;
        cfg.ClientName.OnValueChanged += OnChanged;
        _bound = true;
    }

    private void Unbind()
    {
        if (!_bound) return;
        var cfg = GameConfigNet.Instance;
        if (cfg != null && cfg.IsSpawned)
        {
            cfg.HostName.OnValueChanged -= OnChanged;
            cfg.ClientName.OnValueChanged -= OnChanged;
        }
        _bound = false;
    }

    private void OnChanged(FixedString32Bytes _, FixedString32Bytes __)
    {
        RefreshTextFromNetIfPossible();
        ApplyAutoSize();
    }

    private void RefreshTextFromNetIfPossible()
    {
        var nm = NetworkManager.Singleton;
        var cfg = GameConfigNet.Instance;
        if (nm == null || cfg == null) return;

        bool iAmHost = nm.IsServer; // אצלך: Host=מטייל, Client=נווט

        string rawName = iAmHost ? cfg.HostName.Value.ToString() : cfg.ClientName.Value.ToString();

        // אם אין שם — ריק באמת (כדי להתכווץ ל-0)
        if (!HasRealText(rawName))
        {
            text.text = string.Empty;
            return;
        }

        // חיתוך לשם מקסימלי
        string trimmedName = rawName.Trim();
        trimmedName = Truncate(trimmedName, maxNameChars);

        string role = iAmHost ? "מטייל" : "נווט";

        string nameForUI = ContainsHebrew(trimmedName)
            ? trimmedName
            : $"{LTR}{trimmedName}{POP}";

        text.text = $"שחקן: {nameForUI}\nתפקיד: {role}";
    }

    private void ApplyAutoSize()
    {
        // כמו BubbleAutoSize: "ריק" => כיווץ ל-0
        bool hasText = HasRealText(text.text);

        if (!hasText)
        {
            bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
            return;
        }

        float innerMax = Mathf.Max(0f, maxWidth - padding.x * 2f);

        // מכריחים ריפרש למידות של TMP
        text.ForceMeshUpdate();

        // נותנים ל-TMP רוחב פנימי בשביל wrapping
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, innerMax);

        Vector2 pref = text.GetPreferredValues(text.text, innerMax, 0f);

        float w = Mathf.Clamp(pref.x + padding.x * 2f, minWidth, maxWidth);
        float h = pref.y + padding.y * 2f;

        bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
    }

    private static string Truncate(string s, int maxChars)
    {
        if (string.IsNullOrEmpty(s) || maxChars <= 0) return string.Empty;
        return (s.Length <= maxChars) ? s : s.Substring(0, maxChars);
    }

    private static bool HasRealText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim('\u200F', '\u200E', '\u202A', '\u202B', '\u202C');
        return s.Length > 0;
    }

    private static bool ContainsHebrew(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '\u0590' && c <= '\u05FF')
                return true;
        }
        return false;
    }
}
