using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NavigatorHUD : MonoBehaviour
{
    [Header("Shared Bar")]
    public GameObject sharedBarPrefab;
    public RectTransform barParent;
    private HUDShared sharedBar;

    [Header("Navigator UI")]
    public TMP_Text messageText;
    public Image puzzleImage;

    private void Start()
    {
        if (sharedBarPrefab && barParent)
        {
            var inst = Instantiate(sharedBarPrefab, barParent);
            sharedBar = inst.GetComponent<HUDShared>();
        }

        if (puzzleImage)
            puzzleImage.gameObject.SetActive(false);
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

    public void ShowPuzzleImage(Sprite s)
    {
        if (!puzzleImage)
            return;

        puzzleImage.sprite = s;
        puzzleImage.gameObject.SetActive(true);
    }

    public void HidePuzzleImage()
    {
        if (puzzleImage)
            puzzleImage.gameObject.SetActive(false);
    }

    public Image PuzzleImage => puzzleImage;
}
