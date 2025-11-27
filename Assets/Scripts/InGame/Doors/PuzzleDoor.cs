using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PuzzleDoor : IDoor
{
    private bool solved = false;

    private DoorController controller;

    private GameObject puzzleInstance;
    private Sprite navigatorImageSprite;

    private DraggablePiece[] pieces;
    private RectTransform[] targetSlots;

    private Image navigatorImage;

    public PuzzleDoor(DoorController controller, GameObject prefab, Sprite preview)
    {
        this.controller = controller;
        this.navigatorImageSprite = preview;
        this.puzzleInstance = prefab;
    }

    private void InstantiatePuzzle()
    {
        GameObject slot = HUDManager.Instance.TravellerHUD.PuzzleSlot;

        GameObject instance = Object.Instantiate(puzzleInstance, slot.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localScale = Vector3.one;

        puzzleInstance = instance;

        Transform piecesParent = instance.transform.Find("Pieces");
        Transform targetsParent = instance.transform.Find("Targets");

        pieces = piecesParent.GetComponentsInChildren<DraggablePiece>(true);

        // סינון החוצה את RectTransform של האבא עצמו
        targetSlots = targetsParent
            .GetComponentsInChildren<RectTransform>(true)
            .Where(t => t.gameObject != targetsParent.gameObject)
            .ToArray();

        navigatorImage = HUDManager.Instance.NavigatorHUD.PuzzleImage;

        // מיפוי אחד לאחד
        for (int i = 0; i < pieces.Length; i++)
            pieces[i].target = targetSlots[i];
    }

    public bool IsOpen() => solved;

    public void TryOpen()
    {
        if (solved)
            return;

        if (puzzleInstance == null || puzzleInstance.transform.parent == null)
            InstantiatePuzzle();

        HUDManager.Instance.TravellerHUD.ShowPuzzle();
        puzzleInstance.SetActive(true);

        if (navigatorImage)
        {
            navigatorImage.sprite = navigatorImageSprite;
            navigatorImage.enabled = true;
            navigatorImage.gameObject.SetActive(true);
        }

        GameManager.Instance.inPuzzle = true;
        GameManager.Instance.activePuzzleDoor = controller;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void PuzzleSolved()
    {
        foreach (var p in pieces)
            if (!p.IsSnapped())
                return;

        solved = true;

        HUDManager.Instance.TravellerHUD.HidePuzzle();

        puzzleInstance.SetActive(false);
        if (navigatorImage)
        {
            navigatorImage.enabled = false;
            navigatorImage.gameObject.SetActive(true);
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        GameManager.Instance.inPuzzle = false;
        GameManager.Instance.activePuzzleDoor = null;

        controller.StartOpeningDoor(controller.openAngle);
    }

    public void ForceClosePuzzle()
    {
        HUDManager.Instance.NavigatorHUD.HidePuzzleImage();
        HUDManager.Instance.TravellerHUD.HidePuzzle();
        if (navigatorImage)
        {
            navigatorImage.enabled = false;
            navigatorImage.gameObject.SetActive(true);
        }
    }
}
