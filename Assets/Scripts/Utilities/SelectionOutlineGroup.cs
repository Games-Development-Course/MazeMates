using UnityEngine;
using UnityEngine.UI;

public class SelectionOutlineGroup : MonoBehaviour
{
    [Header("Assign the 4 Outlines here (order = buttons order)")]
    [SerializeField] private Outline[] outlines;

    [Header("Optional: the selection buttons (to disable interaction after start)")]
    [SerializeField] private Button[] selectionButtons;

    [Header("Outline distance")]
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
            return;

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

    public void ClearSelection()
    {
        if (_locked) return;
        ApplySelection(-1);
    }

    /// <summary>
    /// ✅ חבר את זה ל-OnClick של כפתור "Start/התחל".
    /// נועל את הבחירה על מה שנבחר כרגע.
    /// </summary>
    public void OnStartPressed()
    {
        _locked = true;

        // Optional: also disable the selection buttons so it feels "locked"
        if (selectionButtons != null && selectionButtons.Length > 0)
        {
            for (int i = 0; i < selectionButtons.Length; i++)
            {
                if (selectionButtons[i] != null)
                    selectionButtons[i].interactable = false;
            }
        }
    }

    // (אופציונלי) אם תרצה לפתוח נעילה מקוד/דיבאג
    public void UnlockSelection()
    {
        _locked = false;

        if (selectionButtons != null && selectionButtons.Length > 0)
        {
            for (int i = 0; i < selectionButtons.Length; i++)
            {
                if (selectionButtons[i] != null)
                    selectionButtons[i].interactable = true;
            }
        }
    }

    public bool IsLocked => _locked;
    public int SelectedIndex => _selectedIndex;
}
