using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TravellerHUD : MonoBehaviour
{
    [Header("Shared Bar (placed manually)")]
    public HUDShared sharedBar; // ← במקום prefab
    public RectTransform barParent;

    [Header("Traveller UI")]
    public TMP_Text messageText;
    public GameObject PuzzleSlot;
    public Image[] lifeFlashIcons;

    private bool flashing = false;

    private void Start()
    {
        // אם לא הוגדר ידנית — מחפש לבד בתוך ההיררכיה
        if (!sharedBar)
            sharedBar = GetComponentInChildren<HUDShared>(true);
    }

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

    public void ShowPuzzle()
    {
        if (!PuzzleSlot)
            return;

        Transform child = PuzzleSlot.transform.Find("PuzzleChicken");
        if (child != null)
            child.gameObject.SetActive(true);
    }

    public void HidePuzzle()
    {
        if (!PuzzleSlot)
            return;

        Transform child = PuzzleSlot.transform.Find("PuzzleChicken");
        if (child != null)
            child.gameObject.SetActive(false);
    }

    public void FlashLives()
    {
        if (!gameObject.activeInHierarchy || flashing)
            return;

        StartCoroutine(FlashRoutine());
    }

    private System.Collections.IEnumerator FlashRoutine()
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
}
