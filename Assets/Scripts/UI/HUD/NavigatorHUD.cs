using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class NavigatorHUD : MonoBehaviour
{
    [Header("Shared Bar (manual reference)")]
    public HUDShared sharedBar;
    public RectTransform barParent;

    [Header("Navigator UI")]
    public TMP_Text messageText;

    [Header("Buttons to Lock Before Ready")]
    public Button[] actionButtons;

    private IEnumerator Start()
    {
        if (!sharedBar)
            sharedBar = GetComponentInChildren<HUDShared>(true);

        foreach (var b in actionButtons)
            b.interactable = false;

        yield return StartCoroutine(WaitForNavigator());

        Debug.Log("NavigatorHUD: Navigator is ready — buttons unlocked.");
    }

    private IEnumerator WaitForNavigator()
    {
        // מחכים ש־NavigatorActions.Instance יהיה מוכן
        while (NavigatorActions.Instance == null)
            yield return null;

        // נותנים פריים אחד "אקסטרה" ביטחון אחרי הספון
        yield return null;

        foreach (var b in actionButtons)
            b.interactable = true;
    }

    // ============================================
    // HUD API
    // ============================================

    public void UpdateShared(GameManager gm)
    {
        sharedBar?.UpdateValues(gm);
    }

    public void ShowMessage(string msg)
    {
        if (messageText == null)
            return;

        messageText.gameObject.SetActive(true);
        messageText.text = msg;
    }

    public void SetMessageColor(Color c)
    {
        if (messageText)
            messageText.color = c;
    }

    public void Clear()
    {
        if (messageText == null)
            return;

        messageText.text = string.Empty;
        // לא מכבים את האובייקט
    }
}
