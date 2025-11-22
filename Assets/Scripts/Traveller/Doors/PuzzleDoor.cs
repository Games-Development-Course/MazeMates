using UnityEngine;

public class PuzzleDoor : IDoor
{
    private bool solved = false;
    private DoorController controller;

    private Canvas canvas;
    private DraggablePiece[] pieces;
    private Transform[] targets;

    public PuzzleDoor(DoorController controller, Canvas canvas, DraggablePiece[] pieces, Transform[] targets)
    {
        this.controller = controller;
        this.canvas = canvas;
        this.pieces = pieces;
        this.targets = targets;

        canvas.enabled = false;

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

        GameManager.Instance.inPuzzle = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void PuzzleSolved()
    {
        foreach (var p in pieces)
        {
            if (!p.IsSnapped())
                return;
        }

        solved = true;
        canvas.gameObject.SetActive(false);
        canvas.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        controller.StartOpeningDoor(controller.openAngle);
    }

    // משמש ליציאה מהחידה אם השחקן זז
    public void ForceClosePuzzle()
    {
        canvas.gameObject.SetActive(false);
        canvas.enabled = false;

    }
}
