using UnityEngine;
using UnityEngine.UI;

public class PuzzleDoor : IDoor
{
    private bool solved = false;
    private DoorController controller;

    private Canvas canvas;
    private DraggablePiece[] pieces;
    private Transform[] targets;

    private Sprite puzzleSprite;

    private GameObject runtimeNavigatorImage;   // ה-Image שנוצר בזמן ריצה

    public PuzzleDoor(
        DoorController controller,
        Canvas canvas,
        DraggablePiece[] pieces,
        Transform[] targets,
        Sprite puzzleSprite
    )
    {
        this.controller = controller;
        this.canvas = canvas;
        this.pieces = pieces;
        this.targets = targets;
        this.puzzleSprite = puzzleSprite;

        canvas.enabled = false;

        // ==== יצירה אוטומטית של UI Image מתחת ל-NavigatorHUD ====
        if (puzzleSprite != null)
        {
            // מוצא את ההורה (NavigatorHUD)
            HUDManager hud = HUDManager.Instance;
            RectTransform parent = hud.NavigatorHUD.GetComponent<RectTransform>();

            runtimeNavigatorImage = new GameObject("RuntimePuzzleImage");
            runtimeNavigatorImage.transform.SetParent(parent, false);

            Image img = runtimeNavigatorImage.AddComponent<Image>();
            img.sprite = puzzleSprite;
            img.color = Color.white;
            img.preserveAspect = true;

            RectTransform rt = img.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            runtimeNavigatorImage.SetActive(false);
        }

        // Setup of pieces
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].target = targets[i];
            pieces[i].puzzle = controller;
        }
    }

    public bool IsOpen() => solved;

    public void TryOpen()
    {
        if (solved) return;

        canvas.gameObject.SetActive(true);
        canvas.enabled = true;

        if (runtimeNavigatorImage != null)
            runtimeNavigatorImage.SetActive(true);

        GameManager.Instance.inPuzzle = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void PuzzleSolved()
    {
        foreach (var p in pieces)
            if (!p.IsSnapped())
                return;

        solved = true;

        canvas.gameObject.SetActive(false);

        if (runtimeNavigatorImage != null)
            runtimeNavigatorImage.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        controller.StartOpeningDoor(controller.openAngle);
    }

    public void ForceClosePuzzle()
    {
        canvas.gameObject.SetActive(false);

        if (runtimeNavigatorImage != null)
            runtimeNavigatorImage.SetActive(false);
    }

    public void ForceSolveAndOpen()
    {
        canvas.gameObject.SetActive(false);

        if (runtimeNavigatorImage != null)
            runtimeNavigatorImage.SetActive(false);

        solved = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        controller.StartOpeningDoor(controller.openAngle);
    }
}
