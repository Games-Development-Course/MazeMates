using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class BubbleAutoSize : MonoBehaviour
{
    public RectTransform bubble;          // Message
    public TMP_Text text;                 // Text (TMP)

    [Header("Sizing")]
    public float maxWidth = 480f;
    public float minWidth = 80f;

    // padding per side (left/right) and (top/bottom)
    public Vector2 padding = new Vector2(40f, 28f);

    void LateUpdate()
    {
        if (!bubble || !text) return;

        // Treat whitespace + invisible marks as empty
        string s = text.text;
        bool hasText = !string.IsNullOrWhiteSpace(s) && s.Trim('\u200F', '\u200E', '\u202A', '\u202B', '\u202C').Length > 0;

        // Toggle ALL visuals (Images etc.) under the bubble, but keep the TMP text component enabled
        var graphics = bubble.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics)
        {
            if (g == null) continue;
            if (g == text) continue;              // keep text component itself
            g.enabled = hasText;                  // hide background/border images
        }

        // Optional: collapse bubble if no text
        if (!hasText)
        {
            bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
            return;
        }

        // Max inner width for wrapping (inside padding)
        float innerMax = Mathf.Max(0f, maxWidth - padding.x * 2f);

        // Force TMP to wrap based on innerMax
        var tr = text.rectTransform;
        tr.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, innerMax);

        // Measure preferred size within innerMax
        Vector2 pref = text.GetPreferredValues(text.text, innerMax, 0);

        // Bubble size = text size + padding
        float w = Mathf.Clamp(pref.x + padding.x * 2f, minWidth, maxWidth);
        float h = pref.y + padding.y * 2f;

        bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
    }
}
