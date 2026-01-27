using UnityEngine;
using UnityEngine.UI;

public class SelectionOutlineGroup : MonoBehaviour
{
    [Header("Assign the 4 Outlines here (order = buttons order)")]
    [SerializeField] private Outline[] outlines;

    [Header("Outline distances")]
    [SerializeField] private Vector2 normalDistance = new Vector2(4f, 4f);
    [SerializeField] private Vector2 selectedDistance = new Vector2(9f, 9f);

    [Header("Optional: select one by default (-1 = none)")]
    [SerializeField] private int defaultSelectedIndex = 0;

    private int _selectedIndex = -1;
    private bool _locked = false;

    private void Awake()
    {
        ApplySelection(defaultSelectedIndex);
    }

    public void SelectIndex(int index)
    {
        if (_locked) return;   
        ApplySelection(index);
    }

    private void ApplySelection(int index)
    {
        if (outlines == null || outlines.Length == 0)
        {
            return;
        }
        if (index < -1) index = -1;
        if (index >= outlines.Length) index = outlines.Length - 1;

        _selectedIndex = index;

        for (int i = 0; i < outlines.Length; i++)
        {
            var o = outlines[i];
            if (o == null) continue;

            o.effectDistance = (i == _selectedIndex) ? selectedDistance : normalDistance;
        }
    }

    // Optional helper if you ever want to clear selection from code
    public void ClearSelection()
    {
        if (_locked) return;
        ApplySelection(-1);
    }

    // ✅ חבר את זה לכפתור "נעל בחירה"
    public void LockSelection()
    {
        _locked = true;
    }

    // (אופציונלי) אם תרצה כפתור "פתח נעילה"
    public void UnlockSelection()
    {
        _locked = false;
    }

    // (אופציונלי) כדי לדעת בקוד אם נעול
    public bool IsLocked => _locked;
}
