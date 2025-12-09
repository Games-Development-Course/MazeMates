using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TravellerHUD : MonoBehaviour
{
    [Header("Shared Bar (placed manually)")]
    public HUDShared sharedBar;
    public RectTransform barParent;

    [Header("Traveller UI")]
    public TMP_Text messageText;
    public GameObject PuzzleSlot;
    public Image[] lifeFlashIcons;

    private bool flashing = false;

    private void Start()
    {
        if (!sharedBar)
            sharedBar = GetComponentInChildren<HUDShared>(true);
    }

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

    public void ShowPuzzle()
    {
        if (!PuzzleSlot)
            return;

        foreach (Transform child in PuzzleSlot.transform)
            child.gameObject.SetActive(true);
    }

    public void HidePuzzle()
    {
        if (!PuzzleSlot)
            return;

        foreach (Transform child in PuzzleSlot.transform)
            child.gameObject.SetActive(false);
    }

    public void FlashLives()
    {
        if (!gameObject.activeInHierarchy || flashing)
            return;

        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        flashing = true;

        for (int i = 0; i < 4; i++)
        {
            foreach (var icon in lifeFlashIcons)
                if (icon)
                    icon.enabled = !icon.enabled;

            yield return new WaitForSeconds(0.15f);
        }

        foreach (var icon in lifeFlashIcons)
            if (icon)
                icon.enabled = true;

        flashing = false;
    }

    public void Clear()
    {
        if (messageText == null)
            return;

        messageText.text = string.Empty;
        // לא מכבים את האובייקט – כדי שהודעות עתידיות ייראו
    }
}
