// PuzzleDoor.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PuzzleDoor : IDoor
{
    private bool solved = false;

    private DoorController controller;

    private GameObject puzzleInstance;   // runtime instance

    private DraggablePiece[] pieces;
    private RectTransform[] targetSlots;

    // תמונות ה-Hints (מתחת ל- Hints)
    private Image[] hintImages;

    public PuzzleDoor(DoorController controller)
    {
        this.controller = controller;
    }

    // ---------------------------------------------------------
    private void InstantiatePuzzle()
    {
        Debug.Log("INSTANTIATE PUZZLE for Traveller!");

        if (controller.puzzlePrefab == null)
        {
            Debug.LogError("PuzzleDoor: puzzlePrefab is NULL on " + controller.name);
            return;
        }

        GameObject slot = HUDManager.Instance.TravellerHUD.PuzzleSlot;
        Debug.Log("PuzzleSlot = " + slot);

        puzzleInstance = Object.Instantiate(controller.puzzlePrefab, slot.transform);

        puzzleInstance.transform.localPosition = Vector3.zero;
        puzzleInstance.transform.localScale = Vector3.one;

        // ===== שליפות לפי ההיררכיה שלך =====
        Transform piecesParent = puzzleInstance.transform.Find("Pieces");
        Transform targetsParent = puzzleInstance.transform.Find("Targets");
        Transform hintsParent = puzzleInstance.transform.Find("Hints");

        if (piecesParent == null || targetsParent == null)
        {
            Debug.LogError("PuzzleDoor: Pieces or Targets parent missing on puzzle prefab " + controller.puzzlePrefab.name);
            return;
        }

        // כל החלקים הנגררים
        pieces = piecesParent.GetComponentsInChildren<DraggablePiece>(true);

        // כל ה-Slots (למעט האבא עצמו)
        targetSlots = targetsParent
            .GetComponentsInChildren<RectTransform>(true)
            .Where(t => t.gameObject != targetsParent.gameObject)
            .ToArray();

        // מיפוי piece → target (בהנחה שהסדר בהיררכיה תואם)
        for (int i = 0; i < pieces.Length; i++)
        {
            int idx = Mathf.Min(i, targetSlots.Length - 1);
            pieces[i].target = targetSlots[idx];
        }

        // ===== HINTS =====
        if (hintsParent != null)
        {
            var allHints = hintsParent
                .GetComponentsInChildren<Image>(true)
                .Where(img => img.gameObject != hintsParent.gameObject)
                .ToArray();

            hintImages = new Image[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                if (i < allHints.Length)
                    hintImages[i] = allHints[i];
            }

            foreach (var img in hintImages)
            {
                if (img != null)
                    img.enabled = false;
            }
        }
        else
        {
            Debug.LogWarning("PuzzleDoor: no 'Hints' child found under puzzle prefab " + controller.puzzlePrefab.name);
            hintImages = new Image[0];
        }

        Debug.Log("Puzzle instance created: " + puzzleInstance);
    }

    // ---------------------------------------------------------
    public bool IsOpen() => solved;

    public void TryOpen()
    {
        Debug.Log("TRY OPEN PUZZLE DOOR! (Traveller should see puzzle UI)");

        if (solved)
            return;

        if (puzzleInstance == null)
            InstantiatePuzzle();

        HUDManager.Instance.TravellerHUD.ShowPuzzle();
        puzzleInstance.SetActive(true);

        // לוודא שיש לנו Sprite למסך:
        if (controller.navigatorPreview == null)
        {
            // ניסיון נוסף לגבות מה-OriginalImage במופע שרק יצרנו
            Transform original = puzzleInstance.transform.Find("OriginalImage");
            if (original != null)
            {
                var img = original.GetComponentInChildren<Image>();
                if (img != null && img.sprite != null)
                    controller.navigatorPreview = img.sprite;
            }
        }

        // שליחת התמונה למסך הטלוויזיה (או רעש אם אין)
        controller.ShowNavigatorPreviewOnScreen(controller.navigatorPreview);

        GameManager.Instance.inPuzzle = true;
        GameManager.Instance.activePuzzleDoor = controller;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // ---------------------------------------------------------
    public void PuzzleSolved()
    {
        foreach (var p in pieces)
            if (!p.IsSnapped())
                return;

        solved = true;

        HUDManager.Instance.TravellerHUD.HidePuzzle();
        puzzleInstance.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        GameManager.Instance.inPuzzle = false;
        GameManager.Instance.activePuzzleDoor = null;

        // כשנפתרה החידה נחזור ל־noise במסך
        controller.ShowNavigatorPreviewOnScreen(null);

        // ⭐ הטוטוריאל צריך לדעת שהחידה נפתרה
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        tm?.NotifyPuzzleSolved();

        // פותחים את הדלת עצמה
        controller.RequestOpenDoorRpc();
    }

    // ---------------------------------------------------------
    // נדרש על ידי PadTrigger
    // ---------------------------------------------------------
    public void ForceClosePuzzle()
    {
        if (puzzleInstance != null)
            puzzleInstance.SetActive(false);

        HUDManager.Instance.TravellerHUD.HidePuzzle();

        GameManager.Instance.inPuzzle = false;
        GameManager.Instance.activePuzzleDoor = null;

        // גם כאן נחזור ל-noise
        controller.ShowNavigatorPreviewOnScreen(null);
    }

    // ---------------------------------------------------------
    // LIFEBOUY SUPPORT – בחירת Hint רנדומלי שלא הושלם עדיין
    // ---------------------------------------------------------
    public void RevealRandomHint()
    {
        if (puzzleInstance == null)
            InstantiatePuzzle();

        if (pieces == null || pieces.Length == 0)
            return;

        if (hintImages == null || hintImages.Length == 0)
            return;

        List<int> available = new List<int>();

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == null)
                continue;

            if (pieces[i].IsSnapped())
                continue;

            if (i >= hintImages.Length)
                continue;

            var img = hintImages[i];
            if (img == null)
                continue;

            if (img.enabled)
                continue;

            available.Add(i);
        }

        if (available.Count == 0)
        {
            Debug.Log("PuzzleDoor.RevealRandomHint: no available hints (maybe puzzle almost/fully solved).");
            return;
        }

        int idx = Random.Range(0, available.Count);
        int chosen = available[idx];

        var chosenImg = hintImages[chosen];
        if (chosenImg != null)
        {
            chosenImg.enabled = true;
            Debug.Log("PuzzleDoor.RevealRandomHint: enabled hint index " + chosen);

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.StartCoroutine(
                    DisableHintAfterSeconds(chosenImg, 3f)
                );
            }
        }
    }

    private IEnumerator DisableHintAfterSeconds(Image img, float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (img != null)
            img.enabled = false;
    }
}
