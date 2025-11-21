using UnityEngine;
using UnityEngine.UI;

public class PreventDeselect: MonoBehaviour
{
    private ToggleGroup group;
    private Toggle lastSelected;

    void Start()
    {
        group = GetComponent<ToggleGroup>();

        // מציאת הטוגל הראשון שנבחר ברגע הפעלת הסצנה
        foreach (var t in group.GetComponentsInChildren<Toggle>())
        {
            if (t.isOn)
            {
                lastSelected = t;
                break;
            }
        }

        // רישום לאירועים של כל הטוגלים
        foreach (var t in group.GetComponentsInChildren<Toggle>())
        {
            t.onValueChanged.AddListener((isOn) =>
            {
                // אם הטוגל הזה נדלק — צריך לעדכן
                if (isOn)
                {
                    lastSelected = t;
                }
                else
                {
                    // מניעת כיבוי הטוגל הנבחר לא ע"י טוגל אחר
                    if (lastSelected == t)
                    {
                        t.isOn = true;
                    }
                }
            });
        }
    }
}
