using UnityEngine;

public class StartupPanelsFlow : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject secondWindowPanel;     // "פתוח חלון נוסף?"
    [SerializeField] private GameObject instructionsPanel;     // Instructions popup

    private void Awake()
    {
        // בתחילת המשחק: קודם שואלים על חלון נוסף
        if (secondWindowPanel != null) secondWindowPanel.SetActive(true);

        // ואת ההוראות לא מציגים עדיין
        if (instructionsPanel != null) instructionsPanel.SetActive(false);
    }

    // לקרוא לזה בסוף כפתור "לא"
    public void OnNoClicked()
    {
        if (secondWindowPanel != null) secondWindowPanel.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(true);
    }

    // לקרוא לזה בסוף כפתור "כן"
    public void OnYesClicked()
    {
        // פה לא פותחים חלון חדש כדי לא להסתבך עם popup blockers:
        // את הפתיחה עצמה נשאיר לסקריפט של הכפתור "כן" (window.open) באותו OnClick.
        if (secondWindowPanel != null) secondWindowPanel.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(true);
    }
}
