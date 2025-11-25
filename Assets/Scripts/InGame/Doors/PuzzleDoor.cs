using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PuzzleDoor : IDoor
{
    private bool solved = false;
    private DoorController controller;

    private Canvas canvas;
    private DraggablePiece[] pieces;
    private Transform[] targets;

    private GameObject travellerOriginal;

    // UI image for navigator
    private Image navigatorPreviewImage;

    public PuzzleDoor(
        DoorController controller,
        Canvas canvas,
        DraggablePiece[] pieces,
        Transform[] targets,
        GameObject originalObj
    )
    {
        this.controller = controller;
        this.canvas = canvas;
        this.pieces = pieces;
        this.targets = targets;
        this.travellerOriginal = originalObj;

        canvas.enabled = false;
        travellerOriginal.SetActive(false);

        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].target = targets[i];
            pieces[i].puzzle = controller;
        }
    }

    public bool IsOpen() => solved;

    public void TryOpen()
    {
        if (solved)
            return;

        // Traveller UI
        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        travellerOriginal.SetActive(true);

        // Create navigator preview
        if (navigatorPreviewImage == null)
        {
            GameObject imgObj = new GameObject("NavigatorPuzzlePreview");
            imgObj.transform.SetParent(HUDManager.Instance.NavigatorHUD.transform, false);

            navigatorPreviewImage = imgObj.AddComponent<Image>();
            navigatorPreviewImage.sprite = controller.puzzleOriginalSprite;
            navigatorPreviewImage.preserveAspect = true;

            RectTransform rt = navigatorPreviewImage.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(450, 450);
        }

        navigatorPreviewImage.gameObject.SetActive(true);

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
        travellerOriginal.SetActive(false);

        if (navigatorPreviewImage != null)
            navigatorPreviewImage.gameObject.SetActive(false);

        RefreshNavigatorCamera();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        controller.StartOpeningDoor(controller.openAngle);
    }

    public void ForceClosePuzzle()
    {
        canvas.gameObject.SetActive(false);
        travellerOriginal.SetActive(false);

        if (navigatorPreviewImage != null)
            navigatorPreviewImage.gameObject.SetActive(false);

        RefreshNavigatorCamera();
        GameManager.Instance.inPuzzle = false;
    }

    public void ForceSolveAndOpen()
    {
        ForceClosePuzzle();

        solved = true;

        controller.StartOpeningDoor(controller.openAngle);
    }

    public void RevealRandomHint()
    {
        if (controller.hintOverlaysParent == null)
            return;

        int count = controller.hintOverlaysParent.childCount;
        if (count == 0)
            return;

        Transform hint = controller.hintOverlaysParent.GetChild(Random.Range(0, count));
        hint.gameObject.SetActive(true);

        GameManager.Instance.StartCoroutine(HideHint(hint.gameObject));
    }

    private IEnumerator HideHint(GameObject obj)
    {
        yield return new WaitForSeconds(2f);
        obj.SetActive(false);
    }

    private void RefreshNavigatorCamera()
    {
        var navCam = GameObject.Find("NavigatorCamera")?.GetComponent<Camera>();
        if (navCam != null)
        {
            navCam.enabled = false;
            navCam.enabled = true;
        }
    }
}
