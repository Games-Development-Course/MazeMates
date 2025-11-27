using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TravellerHUD : MonoBehaviour
{
    [Header("Shared Bar")]
    public GameObject sharedBarPrefab;
    public RectTransform barParent;
    private HUDShared sharedBar;

    [Header("Traveller UI")]
    public TMP_Text messageText;
    public GameObject PuzzleSlot;
    public Image[] lifeFlashIcons;

    private bool flashing = false;

    private void Start()
    {
        if (sharedBarPrefab && barParent)
        {
            var inst = Instantiate(sharedBarPrefab, barParent);
            sharedBar = inst.GetComponent<HUDShared>();
        }

        if (PuzzleSlot)
            PuzzleSlot.SetActive(false);
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
        if (PuzzleSlot)
            PuzzleSlot.SetActive(true);
    }

    public void HidePuzzle()
    {
        if (PuzzleSlot)
            PuzzleSlot.SetActive(false);
    }

    public void FlashLives()
    {
        if (!gameObject.activeInHierarchy || flashing) return;
        StartCoroutine(FlashRoutine());
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        flashing = true;

        for (int i = 0; i < 4; i++)
        {
            foreach (var icon in lifeFlashIcons)
                if (icon) icon.enabled = !icon.enabled;

            yield return new WaitForSeconds(0.15f);
        }

        foreach (var icon in lifeFlashIcons)
            if (icon) icon.enabled = true;

        flashing = false;
    }
}
