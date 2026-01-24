// File: Assets/Scripts/Gameplay/Doors/PuzzleDoor.cs
using System.Collections.Generic;
using UnityEngine;

// PuzzleDoor is a logic class (NOT MonoBehaviour).
// ✅ Builds the traveller UI from Puzzle SO ONLY, under ONE ROOT, using PuzzlePreviewSpawner.
public sealed class PuzzleDoor : IDoor
{
    private readonly DoorController controller;

    private TravellerHUD travellerHUD;

    private RectTransform runtimeRoot;
    private PuzzlePreviewSpawner spawner;
    private PuzzlePreviewUI previewUI;

    // pieceId -> draggable
    private readonly Dictionary<string, DraggablePiece> pieceById = new();

    private bool puzzleOpenLocal;
    private bool solvedLocal;

    public PuzzleDoor(DoorController controller)
    {
        this.controller = controller;
    }

    public bool IsOpen() => solvedLocal;

    // Called by DoorController (Traveller owner only)
    public void TryOpen()
    {
        if (puzzleOpenLocal) return;

        Puzzle puzzle = controller != null ? controller.PuzzleDef : null;
        if (puzzle == null)
        {
            Debug.LogError($"[PuzzleDoor] PuzzleDef is NULL on door '{controller?.name}'. MazeGenerator must assign a Puzzle SO via SetPuzzleDefinitionServer().");
            return;
        }

        travellerHUD = FindTravellerHUD();
        if (travellerHUD == null)
        {
            Debug.LogError("[PuzzleDoor] TravellerHUD not found.");
            return;
        }

        RectTransform singleRoot = travellerHUD.PuzzleSingleRoot;
        if (singleRoot == null)
        {
            Debug.LogError("[PuzzleDoor] TravellerHUD.PuzzleSingleRoot is NULL (assign it in Inspector).");
            return;
        }

        travellerHUD.ShowPuzzle();
        travellerHUD.ClearPuzzleRuntimeContentSingleRoot();

        BuildOrRebuildUsingSpawner(singleRoot, puzzle);

        // mark active door
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.activePuzzleDoor = controller;
            gm.inPuzzle = true;
        }

        puzzleOpenLocal = true;
        solvedLocal = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Called when stepping off pad / cancel
    public void ForceClosePuzzle()
    {
        controller?.ShowNavigatorPreviewOnScreen(null);
        CloseLocalUI();
    }

    // DraggablePiece calls this name (compat)
    public void PuzzleSolved()
    {
        CheckSolvedAndNotify();
    }

    // Optional: reveal a hint (enables hint Image(s) by targetId)
    public void RevealRandomHint()
    {
        Puzzle puzzle = controller != null ? controller.PuzzleDef : null;
        if (puzzle == null || previewUI == null) return;

        var notSnapped = new List<string>();
        foreach (var kv in pieceById)
        {
            if (kv.Value == null) continue;
            if (!kv.Value.IsSnapped())
                notSnapped.Add(kv.Key);
        }

        if (notSnapped.Count == 0) return;

        string pieceId = notSnapped[Random.Range(0, notSnapped.Count)];

        // find targetId in SO
        string targetId = null;
        if (puzzle.pieces != null)
        {
            for (int i = 0; i < puzzle.pieces.Length; i++)
            {
                if (puzzle.pieces[i].id == pieceId)
                {
                    targetId = puzzle.pieces[i].targetId;
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(targetId))
            previewUI.SetHintsActiveForTarget(targetId, true);
    }

    // ============================================================
    // Internals
    // ============================================================

    private void BuildOrRebuildUsingSpawner(RectTransform singleRoot, Puzzle puzzle)
    {
        // Create runtime root under the SINGLE ROOT
        var go = new GameObject("PuzzleRuntimeRoot", typeof(RectTransform));
        runtimeRoot = go.GetComponent<RectTransform>();
        runtimeRoot.SetParent(singleRoot, false);
        runtimeRoot.anchorMin = Vector2.zero;
        runtimeRoot.anchorMax = Vector2.one;
        runtimeRoot.offsetMin = Vector2.zero;
        runtimeRoot.offsetMax = Vector2.zero;

        // Add spawner (critical)
        spawner = runtimeRoot.gameObject.AddComponent<PuzzlePreviewSpawner>();
        spawner.autoRunInStart = false; // we control it manually

        // ✅ feed inspector defaults into the runtime spawner (border sprite + all params)
        travellerHUD.ApplyPuzzleSpawnerDefaults(spawner);


        // Canvas reference (scaleFactor etc)
        var canvas = singleRoot.GetComponentInParent<Canvas>();

        // Build into THIS root (not under canvas global)
        spawner.BuildInto(runtimeRoot, puzzle, canvas);
        spawner.ApplyNow(rebuildAll: true);

        previewUI = spawner.UI;
        if (previewUI == null)
        {
            Debug.LogError("[PuzzleDoor] Spawner built but UI is null.");
            return;
        }

        BindPiecesToTargets(puzzle);
    }

    private void BindPiecesToTargets(Puzzle puzzle)
    {
        pieceById.Clear();
        if (puzzle.pieces == null || previewUI == null || previewUI.trayContentRoot == null) return;

        // ✅ Root משותף לכל ה-UI של הפאזל (גם targets/hints וגם pieces)
        // אם באמת "הכל תחת Root אחד" אז זה previewUI.root
        RectTransform commonRoot = previewUI.root != null ? previewUI.root : previewUI.trayContentRoot;

        for (int i = 0; i < puzzle.pieces.Length; i++)
        {
            var pd = puzzle.pieces[i];
            if (string.IsNullOrEmpty(pd.id)) continue;

            RectTransform pieceRt = previewUI.trayContentRoot.Find($"Piece_{pd.id}") as RectTransform;
            if (pieceRt == null) continue;

            var cg = pieceRt.GetComponent<CanvasGroup>();
            if (cg == null) cg = pieceRt.gameObject.AddComponent<CanvasGroup>();

            var drag = pieceRt.GetComponent<DraggablePiece>();
            if (drag == null) drag = pieceRt.gameObject.AddComponent<DraggablePiece>();

            drag.rectTransform = pieceRt;
            drag.canvasGroup = cg;

            // ✅ הכי חשוב: לעבוד במרחב משותף אחד
            drag.commonRoot = commonRoot;

            // Target = ה-hint transform שמשמש גם כ-target
            if (!string.IsNullOrEmpty(pd.targetId) && previewUI.TryGetTargetRect(pd.targetId, out var targetRt))
                drag.target = targetRt;
            else
                drag.target = null;

            // ✅ לשמור "מיקום התחלה" רק אחרי שהכל נבנה/הוזז סופית
            drag.CaptureOriginalPos();

            pieceById[pd.id] = drag;
        }
    }

    private void CheckSolvedAndNotify()
    {
        if (solvedLocal) return;
        if (previewUI == null) return;

        foreach (var kv in pieceById)
        {
            if (kv.Value == null) return;
            if (!kv.Value.IsSnapped()) return;
        }

        solvedLocal = true;

        controller?.ShowNavigatorPreviewOnScreen(null);
        CloseLocalUI();

        Object.FindFirstObjectByType<TutorialManager>()?.NotifyPuzzleSolved();

        // open the physical door via server
        controller?.RequestOpenDoorServerRpc(controller.transform.position);
    }

    private void CloseLocalUI()
    {
        puzzleOpenLocal = false;

        if (travellerHUD != null)
        {
            travellerHUD.HidePuzzle();
            travellerHUD.ClearPuzzleRuntimeContentSingleRoot();
        }

        previewUI = null;
        spawner = null;
        runtimeRoot = null;
        pieceById.Clear();

        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.inPuzzle = false;
            if (gm.activePuzzleDoor == controller) gm.activePuzzleDoor = null;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static TravellerHUD FindTravellerHUD()
    {
        var hm = HUDManager.Instance;
        if (hm != null && hm.Traveller != null)
            return hm.Traveller;

        return Object.FindFirstObjectByType<TravellerHUD>(FindObjectsInactive.Include);
    }
}
