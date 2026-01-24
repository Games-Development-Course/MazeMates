using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PuzzleTrayLayout2Rows : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("Keep 1 for single row tray.")]
    public int maxRows = 1;

    [Tooltip("Auto size THIS rect width to fit items + padding + spacing.")]
    public bool autoSizeWidth = true;

    [Tooltip("If true: height is computed from the tallest piece (+ padding). fixedHeight becomes MIN height.")]
    public bool heightFromTallestPiece = true;

    [Tooltip("Minimum height for THIS rect (and outer). If heightFromTallestPiece=true -> acts as MIN.")]
    public float fixedHeight = 180f;

    [Header("Also drive an OUTER tray rect (recommended)")]
    [Tooltip("Assign the tray root RectTransform here, so BG+Border wrap exactly.")]
    public RectTransform outerRectToResize;

    [Tooltip("Extra size added to outer rect (x,y). Usually 0.")]
    public Vector2 outerExtraSize = Vector2.zero;

    [Tooltip("Ignore children inside this subtree (e.g. BG image + border).")]
    public RectTransform ignoreSubtreeRoot;

    [Header("Padding (inside content)")]
    public float paddingLeft = 24f;
    public float paddingRight = 24f;
    public float paddingTop = 0f;
    public float paddingBottom = 0f;

    [Header("Spacing")]
    public float spacingX = 14f;

    [Header("Align")]
    public bool centerRow = true;

    private RectTransform _rt;
    private bool _dirty;
    private bool _rebuilding;
    private bool _suppressDimChange;

    private readonly List<RectTransform> _items = new();

    private void Awake()
    {
        _rt = transform as RectTransform;
        _dirty = true;
    }

    private void OnEnable()
    {
        _rt = transform as RectTransform;
        _dirty = true;
    }

    private void OnTransformChildrenChanged() => _dirty = true;

    private void OnRectTransformDimensionsChange()
    {
        if (_suppressDimChange) return;
        _dirty = true;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (!_dirty) return;
        Rebuild();
    }

    public void Rebuild()
    {
        if (!Application.isPlaying) return;
        if (_rebuilding) return;

        _rebuilding = true;
        _dirty = false;

        if (_rt == null) _rt = transform as RectTransform;
        if (_rt == null)
        {
            _rebuilding = false;
            return;
        }

        // Make sure rect sizes are up-to-date before measuring
        Canvas.ForceUpdateCanvases();

        CollectItems();

        // ---- width ----
        float piecesW = 0f;
        for (int i = 0; i < _items.Count; i++)
        {
            float w = GetItemWidth(_items[i]);
            piecesW += w;
            if (i > 0) piecesW += spacingX;
        }

        float requiredW = Mathf.Max(1f, paddingLeft + piecesW + paddingRight);

        // ---- height (NEW) ----
        float tallestH = 0f;
        if (heightFromTallestPiece)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                float h = GetItemHeight(_items[i]);
                if (h > tallestH) tallestH = h;
            }
        }

        float heightByPieces = Mathf.Max(1f, paddingTop + tallestH + paddingBottom);

        // If heightFromTallestPiece -> height is max(fixedHeight, heightByPieces)
        float requiredH = heightFromTallestPiece
            ? Mathf.Max(1f, Mathf.Max(fixedHeight, heightByPieces))
            : Mathf.Max(1f, fixedHeight);

        // Size THIS content container
        Vector2 sd = _rt.sizeDelta;
        float targetW = autoSizeWidth ? requiredW : sd.x;
        float targetH = requiredH;

        if (Mathf.Abs(sd.x - targetW) > 0.01f || Mathf.Abs(sd.y - targetH) > 0.01f)
        {
            _suppressDimChange = true;
            _rt.sizeDelta = new Vector2(targetW, targetH);
            _suppressDimChange = false;
        }

        // Also size OUTER tray root so BG+Border wrap exactly
        if (outerRectToResize != null)
        {
            Vector2 osd = outerRectToResize.sizeDelta;
            Vector2 want = new Vector2(targetW + outerExtraSize.x, targetH + outerExtraSize.y);

            if (Mathf.Abs(osd.x - want.x) > 0.01f || Mathf.Abs(osd.y - want.y) > 0.01f)
                outerRectToResize.sizeDelta = want;
        }

        Canvas.ForceUpdateCanvases();

        // Layout items centered inside THIS container
        LayoutSingleRow(piecesW, requiredW);

        _rebuilding = false;
    }

    private void CollectItems()
    {
        _items.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            var t = transform.GetChild(i);
            if (!t.gameObject.activeSelf) continue;

            if (ignoreSubtreeRoot != null && (t == ignoreSubtreeRoot || t.IsChildOf(ignoreSubtreeRoot)))
                continue;

            var rt = t as RectTransform;
            if (rt == null) continue;

            _items.Add(rt);
        }
    }

    private void LayoutSingleRow(float piecesW, float requiredW)
    {
        float y = 0f;

        float startX = -requiredW * 0.5f + paddingLeft;

        if (centerRow)
        {
            float trayW = _rt.rect.width;
            float extra = trayW - requiredW;
            if (extra > 0f) startX += extra * 0.5f;
        }

        float cursor = startX;

        for (int i = 0; i < _items.Count; i++)
        {
            var c = _items[i];
            float w = GetItemWidth(c);

            // center anchors/pivot for consistent placement
            c.anchorMin = new Vector2(0.5f, 0.5f);
            c.anchorMax = new Vector2(0.5f, 0.5f);
            c.pivot = new Vector2(0.5f, 0.5f);

            float xCenter = cursor + w * 0.5f;
            c.anchoredPosition = new Vector2(xCenter, y);

            cursor += w + spacingX;
        }
    }

    private static float GetItemWidth(RectTransform rt)
    {
        // Use actual width for stable visual spacing
        float w = rt.rect.width;
        if (w > 0.01f) return w;

        // fallback
        var le = rt.GetComponent<LayoutElement>();
        if (le != null && le.preferredWidth > 0.01f) return le.preferredWidth;

        float pw = LayoutUtility.GetPreferredWidth(rt);
        if (pw > 0.01f) return pw;

        return Mathf.Max(1f, rt.sizeDelta.x);
    }

    private static float GetItemHeight(RectTransform rt)
    {
        // Use actual height for stable visual height
        float h = rt.rect.height;
        if (h > 0.01f) return h;

        // fallback
        var le = rt.GetComponent<LayoutElement>();
        if (le != null && le.preferredHeight > 0.01f) return le.preferredHeight;

        float ph = LayoutUtility.GetPreferredHeight(rt);
        if (ph > 0.01f) return ph;

        return Mathf.Max(1f, rt.sizeDelta.y);
    }
}
