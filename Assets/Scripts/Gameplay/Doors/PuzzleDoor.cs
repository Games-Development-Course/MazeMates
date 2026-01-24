// File: Assets/Scripts/Gameplay/Doors/PuzzleDoor.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PuzzleDoor : IDoor
{
    private bool solved = false;
    private readonly DoorController controller;

    // We no longer keep a root under PuzzleSlot.
    // Instead, we move specific puzzle UI groups into fixed HUD containers.
    private Transform movedOriginal;
    private Transform movedTargets;
    private Transform movedHints;

    // Runtime moved piece objects (direct children under ObjectsContent)
    private readonly List<GameObject> movedPieceObjects = new();

    private DraggablePiece[] pieces;
    private RectTransform[] targetSlots;

    private Image[] hintImages;

    private bool uiBuilt;

    public PuzzleDoor(DoorController controller)
    {
        this.controller = controller;
    }

    // ---------------------------------------------------------
    private void InstantiatePuzzleIntoHUD()
    {
        Debug.Log("INSTANTIATE PUZZLE for Traveller!");

        if (controller == null)
        {
            Debug.LogError("PuzzleDoor: controller is NULL");
            return;
        }

        if (controller.puzzlePrefab == null)
        {
            Debug.LogError("PuzzleDoor: puzzlePrefab is NULL on " + controller.name);
            return;
        }

        if (HUDManager.Instance == null || HUDManager.Instance.TravellerHUD == null)
        {
            Debug.LogError("PuzzleDoor: HUDManager.Instance or TravellerHUD is NULL");
            return;
        }

        var hud = HUDManager.Instance.TravellerHUD;

        RectTransform screenContent = hud.PuzzleScreenContent;
        RectTransform objectsContent = hud.PuzzleObjectsContent;

        if (screenContent == null || objectsContent == null)
        {
            Debug.LogError("PuzzleDoor: PuzzleScreenContent / PuzzleObjectsContent not assigned on TravellerHUD");
            return;
        }

        // Clear ONLY runtime content (not the borders/background)
        hud.ClearPuzzleRuntimeContent();
        movedPieceObjects.Clear();

        // Create a TEMP instance just to grab its children, then destroy it
        GameObject tempRoot = Object.Instantiate(controller.puzzlePrefab);
        tempRoot.name = controller.puzzlePrefab.name + "(TEMP)";

        // Find expected children on the puzzle prefab instance
        Transform original = tempRoot.transform.Find("OriginalImage");
        Transform piecesParent = tempRoot.transform.Find("Pieces");
        Transform targetsParent = tempRoot.transform.Find("Targets");
        Transform hintsParent = tempRoot.transform.Find("Hints");

        if (piecesParent == null || targetsParent == null)
        {
            Debug.LogError($"PuzzleDoor: Pieces or Targets parent missing on puzzle prefab {controller.puzzlePrefab.name}");
            Object.Destroy(tempRoot);
            return;
        }

        // Detach from temp root first (so destroying root won't delete them)
        if (original != null) original.SetParent(null, false);
        piecesParent.SetParent(null, false);
        targetsParent.SetParent(null, false);
        if (hintsParent != null) hintsParent.SetParent(null, false);

        // Now we can destroy the temp root safely
        Object.Destroy(tempRoot);

        // Re-parent into the fixed Traveller UI hierarchy
        // Original image + Targets + Hints -> Screen Content
        if (original != null)
        {
            original.SetParent(screenContent, false);
            NormalizeRectTransformFillParent(original as RectTransform);
            movedOriginal = original;
        }
        else
        {
            Debug.LogWarning($"PuzzleDoor: no 'OriginalImage' found on puzzle prefab {controller.puzzlePrefab.name}");
            movedOriginal = null;
        }

        targetsParent.SetParent(screenContent, false);
        NormalizeRectTransformFillParent(targetsParent as RectTransform);
        movedTargets = targetsParent;

        if (hintsParent != null)
        {
            hintsParent.SetParent(screenContent, false);
            NormalizeRectTransformFillParent(hintsParent as RectTransform);
            movedHints = hintsParent;
        }
        else
        {
            movedHints = null;
        }

        // ✅ Pieces CHILDREN -> Objects Content (so layout sees each piece directly)
        MoveChildrenTo(objectsContent, piecesParent, movedPieceObjects);

        // Destroy the empty wrapper
        Object.Destroy(piecesParent.gameObject);
        piecesParent = null;

        // Collect pieces after moving (now they are direct children of objectsContent)
        pieces = objectsContent.GetComponentsInChildren<DraggablePiece>(true);

        // Targets
        targetSlots = targetsParent
            .GetComponentsInChildren<RectTransform>(true)
            .Where(t => t.gameObject != targetsParent.gameObject)
            .ToArray();

        for (int i = 0; i < pieces.Length; i++)
        {
            int idx = Mathf.Clamp(i, 0, targetSlots.Length - 1);
            pieces[i].target = (targetSlots.Length > 0) ? targetSlots[idx] : null;
        }

        // HINTS
        if (movedHints != null)
        {
            var allHints = movedHints
                .GetComponentsInChildren<Image>(true)
                .Where(img => img != null && img.transform != movedHints)
                .ToArray();

            hintImages = new Image[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                if (i < allHints.Length)
                    hintImages[i] = allHints[i];
            }

            foreach (var img in hintImages)
                if (img != null) img.enabled = false;
        }
        else
        {
            hintImages = new Image[0];
        }
        hud.SyncPuzzleObjectsPanelToBorder();
        uiBuilt = true;
        Debug.Log("Puzzle UI moved into Traveller HUD containers.");
    }

    /// <summary>
    /// Moves all children of oldParent into newParent, WITHOUT stretching/resizing them.
    /// Adds moved objects to movedList for tracking.
    /// </summary>
    private static void MoveChildrenTo(RectTransform newParent, Transform oldParent, List<GameObject> movedList)
    {
        if (newParent == null || oldParent == null) return;

        // iterate backwards because we're changing parent while iterating
        for (int i = oldParent.childCount - 1; i >= 0; i--)
        {
            Transform ch = oldParent.GetChild(i);
            ch.SetParent(newParent, false);

            if (movedList != null)
                movedList.Add(ch.gameObject);

            // Make it behave like a layout item (position controlled by your WrapLayoutGroup2Rows)
            if (ch is RectTransform rt)
            {
                // Don't stretch! keep its sizeDelta.
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                // Keep pivot as authored; Wrap script may override pivot if you coded it that way.
            }
        }
    }

    private static void NormalizeRectTransformFillParent(RectTransform rt)
    {
        if (rt == null) return;

        // Fill parent (for Original/Targets/Hints containers)
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
    }

    // ---------------------------------------------------------
    public bool IsOpen() => solved;

    public void TryOpen()
    {
        Debug.Log($"[PUZZLE] TryOpen called on door {controller.name} | solved={solved}");

        if (solved)
            return;

        if (HUDManager.Instance == null || HUDManager.Instance.TravellerHUD == null)
        {
            Debug.LogError("[PUZZLE] HUDManager.Instance או TravellerHUD הם NULL – אי אפשר להציג את הפאזל");
            return;
        }

        if (controller.puzzlePrefab == null)
        {
            Debug.LogError("[PUZZLE] puzzlePrefab is NULL על הדלת – אין מה ליצור");
            return;
        }

        if (!uiBuilt)
            InstantiatePuzzleIntoHUD();

        if (!uiBuilt)
        {
            Debug.LogError("[PUZZLE] uiBuilt=false אחרי InstantiatePuzzleIntoHUD – בדוק היררכיה בפריפאב");
            return;
        }

        HUDManager.Instance.TravellerHUD.ShowPuzzle();

        // Tutorial notify
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        tm?.NotifyNavigatorOpenedPuzzleDoor();

        // Cache preview locally if missing
        if (controller.navigatorPreview == null)
        {
            Image img = null;

            if (movedOriginal != null)
                img = movedOriginal.GetComponentInChildren<Image>(true);

            if (img == null && HUDManager.Instance.TravellerHUD.PuzzleScreenContent != null)
                img = HUDManager.Instance.TravellerHUD.PuzzleScreenContent.GetComponentInChildren<Image>(true);

            if (img != null && img.sprite != null)
                controller.navigatorPreview = img.sprite;
        }

        GameManager.Instance.inPuzzle = true;
        GameManager.Instance.activePuzzleDoor = controller;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // ---------------------------------------------------------
    public void PuzzleSolved()
    {
        if (pieces == null || pieces.Length == 0)
            return;

        foreach (var p in pieces)
            if (p != null && !p.IsSnapped())
                return;

        solved = true;

        HUDManager.Instance.TravellerHUD.HidePuzzle();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        GameManager.Instance.inPuzzle = false;
        GameManager.Instance.activePuzzleDoor = null;

        controller.ShowNavigatorPreviewOnScreen(null);

        var tm = Object.FindFirstObjectByType<TutorialManager>();
        tm?.NotifyPuzzleSolved();

        Vector3 openerPos = controller.transform.position;
        var gm = GameManager.Instance;
        if (gm != null && gm.traveller != null)
            openerPos = gm.traveller.transform.position;

        controller.RequestOpenDoorServerRpc(openerPos);
    }

    // ---------------------------------------------------------
    // Required by PadTrigger
    // ---------------------------------------------------------
    public void ForceClosePuzzle()
    {
        if (HUDManager.Instance != null && HUDManager.Instance.TravellerHUD != null)
            HUDManager.Instance.TravellerHUD.HidePuzzle();

        GameManager.Instance.inPuzzle = false;
        GameManager.Instance.activePuzzleDoor = null;

        controller.ShowNavigatorPreviewOnScreen(null);
    }

    // ---------------------------------------------------------
    public void RevealRandomHint()
    {
        if (!uiBuilt)
            InstantiatePuzzleIntoHUD();

        if (pieces == null || pieces.Length == 0)
            return;

        if (hintImages == null || hintImages.Length == 0)
            return;

        List<int> available = new List<int>();

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == null) continue;
            if (pieces[i].IsSnapped()) continue;
            if (i >= hintImages.Length) continue;

            var img = hintImages[i];
            if (img == null) continue;
            if (img.enabled) continue;

            available.Add(i);
        }

        if (available.Count == 0)
            return;

        int chosen = available[Random.Range(0, available.Count)];
        var chosenImg = hintImages[chosen];

        if (chosenImg != null)
        {
            chosenImg.enabled = true;

            if (HUDManager.Instance != null)
                HUDManager.Instance.StartCoroutine(DisableHintAfterSeconds(chosenImg, 3f));
        }
    }

    private IEnumerator DisableHintAfterSeconds(Image img, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (img != null) img.enabled = false;
    }
}
