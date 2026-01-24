// Assets/Scripts/Utilities/WrapLayoutGroup2Rows.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class WrapLayoutGroup2Rows : LayoutGroup
{
    [Header("Flow Settings")]
    [Tooltip("0 = no wrap limit (parent can grow to fit all children in one row). If >0, this is the wrap width limit (excluding padding).")]
    [SerializeField] private float maxRowWidth = 0f;

    [Min(1)]
    [SerializeField] private int maxRows = 2;

    [SerializeField] private float spacingX = 12f;
    [SerializeField] private float spacingY = 12f;

    [Header("Row Alignment")]
    [SerializeField] private bool centerEachRow = true;

    // ---------------- Unity Layout ----------------

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal(); // fills rectChildren
        float prefW = ComputePreferredWidth();
        SetLayoutInputForAxis(prefW, prefW, -1, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        float prefH = ComputePreferredHeight();
        SetLayoutInputForAxis(prefH, prefH, -1, 1);
    }

    public override void SetLayoutHorizontal() => LayoutChildren();
    public override void SetLayoutVertical() => LayoutChildren();

    // ---------------- Core ----------------

    private void LayoutChildren()
    {
        float widthLimit = ResolveWidthLimit(); // excludes padding

        // Build rows
        var rows = BuildRows(widthLimit);

        // Place rows
        float yTop = padding.top;

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];

            float rowStartX = padding.left;

            if (centerEachRow)
            {
                float free = widthLimit - row.width;
                if (free > 0f)
                    rowStartX += free * 0.5f;
            }

            float cursorX = rowStartX;

            for (int j = 0; j < row.items.Count; j++)
            {
                var child = row.items[j];
                var size = row.sizes[j];

                // stable layout item (top-left)
                child.anchorMin = new Vector2(0f, 1f);
                child.anchorMax = new Vector2(0f, 1f);
                child.pivot = new Vector2(0f, 1f);

                float childTop = yTop + (row.height - size.y) * 0.5f;

                SetChildAlongAxis(child, 0, cursorX);
                SetChildAlongAxis(child, 1, childTop);

                cursorX += size.x + spacingX;
            }

            yTop += row.height + spacingY;
        }
    }

    private List<Row> BuildRows(float widthLimit)
    {
        var rows = new List<Row>(maxRows);

        int rowIndex = 0;
        float rowHeight = 0f;
        Row cur = new Row();

        for (int i = 0; i < rectChildren.Count; i++)
        {
            var child = rectChildren[i];
            if (child == null) continue;

            Vector2 size = GetChildPreferredSize(child);

            float addWidth = (cur.count == 0) ? size.x : (spacingX + size.x);
            bool wouldExceed = (maxRowWidth > 0.01f) && (cur.count > 0) && (cur.width + addWidth > widthLimit);

            if (wouldExceed)
            {
                cur.height = rowHeight;
                rows.Add(cur);

                rowIndex++;
                if (rowIndex >= maxRows)
                {
                    SetChildOffscreen(child);
                    continue;
                }

                cur = new Row();
                rowHeight = 0f;
                addWidth = size.x;
            }

            cur.items.Add(child);
            cur.sizes.Add(size);

            cur.width += addWidth;
            cur.count++;

            rowHeight = Mathf.Max(rowHeight, size.y);
        }

        if (cur.count > 0 && rows.Count < maxRows)
        {
            cur.height = rowHeight;
            rows.Add(cur);
        }

        return rows;
    }

    private void SetChildOffscreen(RectTransform child)
    {
        child.anchorMin = new Vector2(0f, 1f);
        child.anchorMax = new Vector2(0f, 1f);
        child.pivot = new Vector2(0f, 1f);
        child.anchoredPosition = new Vector2(-10000f, -10000f);
    }

    /// <summary>
    /// Width limit used for wrapping (EXCLUDING padding).
    /// If maxRowWidth == 0 => no wrap limit => use "infinite".
    /// </summary>
    private float ResolveWidthLimit()
    {
        if (maxRowWidth > 0.01f)
            return maxRowWidth;

        // no wrap limit -> allow a single row to grow
        return 999999f;
    }

    private Vector2 GetChildPreferredSize(RectTransform child)
    {
        float w = LayoutUtility.GetPreferredWidth(child);
        float h = LayoutUtility.GetPreferredHeight(child);

        if (w <= 0.01f) w = child.rect.width;
        if (h <= 0.01f) h = child.rect.height;

        if (w <= 0.01f) w = Mathf.Abs(child.sizeDelta.x);
        if (h <= 0.01f) h = Mathf.Abs(child.sizeDelta.y);

        if (w <= 0.01f) w = 100f;
        if (h <= 0.01f) h = 100f;

        return new Vector2(w, h);
    }

    // ---------------- Preferred Size (THIS is what makes parent grow via ContentSizeFitter) ----------------

    private float ComputePreferredWidth()
    {
        float widthLimit = ResolveWidthLimit(); // excludes padding
        var rows = BuildRows(widthLimit);

        float widestRow = 0f;
        for (int i = 0; i < rows.Count; i++)
            widestRow = Mathf.Max(widestRow, rows[i].width);

        // parent width = widest row + padding
        return padding.left + widestRow + padding.right;
    }

    private float ComputePreferredHeight()
    {
        float widthLimit = ResolveWidthLimit(); // excludes padding
        var rows = BuildRows(widthLimit);

        float total = padding.top + padding.bottom;

        for (int i = 0; i < rows.Count; i++)
        {
            total += rows[i].height;
            if (i < rows.Count - 1)
                total += spacingY;
        }

        return total;
    }

    // ---------------- Dirty handling ----------------

    protected override void OnEnable()
    {
        base.OnEnable();
        SetDirty();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetDirty();
    }
#endif

    private void SetDirty()
    {
        if (!isActiveAndEnabled) return;
        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
    }

    private class Row
    {
        public readonly List<RectTransform> items = new();
        public readonly List<Vector2> sizes = new();
        public int count;
        public float width;
        public float height;
    }
}
