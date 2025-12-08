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


        // 🔒 נועל כל הכפתורים עד שהנווט וה־manager ערוכים
        foreach (var b in actionButtons)
            b.interactable = false;

        yield return StartCoroutine(WaitForNavigator());

        Debug.Log("NavigatorHUD: Navigator is ready — buttons unlocked.");
    }

    private IEnumerator WaitForNavigator()
    {
        while (NavigatorInteractionManager.Instance == null)
            yield return null;

        var nav = NavigatorInteractionManager.Instance;

        while (!nav.IsSpawned)
            yield return null;

        while (!nav.IsOwner)
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
        if (messageText)
            messageText.text = msg;
    }

    public void SetMessageColor(Color c)
    {
        if (messageText)
            messageText.color = c;
    }

 


}
