using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class PuzzlePrefabExporterWindow : EditorWindow
{
    [Header("Assign")]
    public Puzzle puzzleAsset;

    [Tooltip("The Image you authored on (usually OriginalImage's Image). If preserveAspect is ON, we export using the rendered sprite area.")]
    public Image referenceImage;

    [Tooltip("Root containing Hint rects (children are RectTransforms). These also serve as targets.")]
    public Transform hintsRoot;

    [Tooltip("Root containing Piece rects (children are RectTransforms).")]
    public Transform piecesRoot;

    [Header("Optional: auto-fill images from prefab")]
    public Image originalImageInPrefab;
    public Image backgroundImageInPrefab;

    [MenuItem("MazeMates/Puzzle Exporter")]
    public static void Open() => GetWindow<PuzzlePrefabExporterWindow>("Puzzle Exporter");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Export from existing prefab layout to Puzzle (normalized coords)", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        puzzleAsset = (Puzzle)EditorGUILayout.ObjectField("Puzzle Asset", puzzleAsset, typeof(Puzzle), false);
        referenceImage = (Image)EditorGUILayout.ObjectField("Reference Image (authored on)", referenceImage, typeof(Image), true);

        hintsRoot = (Transform)EditorGUILayout.ObjectField("Hints Root (also Targets)", hintsRoot, typeof(Transform), true);
        piecesRoot = (Transform)EditorGUILayout.ObjectField("Pieces Root", piecesRoot, typeof(Transform), true);

        EditorGUILayout.Space(6);
        originalImageInPrefab = (Image)EditorGUILayout.ObjectField("Original Image (optional)", originalImageInPrefab, typeof(Image), true);
        backgroundImageInPrefab = (Image)EditorGUILayout.ObjectField("Background Image (optional)", backgroundImageInPrefab, typeof(Image), true);

        EditorGUILayout.Space(12);

        using (new EditorGUI.DisabledScope(puzzleAsset == null || referenceImage == null))
        {
            if (GUILayout.Button("EXPORT -> Puzzle Asset", GUILayout.Height(32)))
                Export();
        }

        EditorGUILayout.HelpBox(
            "Hints are exported as both visual hints and snap targets (by targetId).\n" +
            "If preserveAspect is ON on the reference image, we export relative to the rendered sprite area.",
            MessageType.Info
        );
    }

    private void Export()
    {
        if (puzzleAsset == null || referenceImage == null)
        {
            Debug.LogError("[PuzzleExporter] Missing puzzleAsset or referenceImage.");
            return;
        }

        Undo.RecordObject(puzzleAsset, "Export Puzzle Data");

        if (originalImageInPrefab != null) puzzleAsset.originalImage = originalImageInPrefab.sprite;
        if (backgroundImageInPrefab != null) puzzleAsset.backgroundImage = backgroundImageInPrefab.sprite;

        // ✅ We don't export targets anymore
        puzzleAsset.targets = null;

        // Hints (also targets)
        if (hintsRoot != null)
        {
            var hdefs = new List<Puzzle.HintDef>();

            foreach (Transform ch in hintsRoot)
            {
                if (ch == null) continue;
                if (ch is not RectTransform rt) continue;

                string baseId = BaseIdFromName(ch.name);
                var (npos, nsize) = ComputeNormalizedRect(referenceImage, rt);
                Sprite hintSprite = TryExtractSprite(ch);

                hdefs.Add(new Puzzle.HintDef
                {
                    id = baseId,
                    targetId = baseId,        // ✅ this is what pieces should snap to
                    sprite = hintSprite,
                    normalizedPos = npos,
                    normalizedSize = nsize
                });
            }

            puzzleAsset.hints = hdefs.ToArray();
        }

        // Pieces
        if (piecesRoot != null)
        {
            var pdefs = new List<Puzzle.PieceDef>();

            foreach (Transform ch in piecesRoot)
            {
                if (ch == null) continue;
                if (ch is not RectTransform rt) continue;

                string baseId = BaseIdFromName(ch.name);
                Sprite pieceSprite = TryExtractSprite(ch);

                var (_, nsize) = ComputeNormalizedRect(referenceImage, rt);

                pdefs.Add(new Puzzle.PieceDef
                {
                    id = baseId,
                    sprite = pieceSprite,
                    normalizedSize = nsize,
                    targetId = baseId // ✅ must match hint.targetId
                });
            }

            puzzleAsset.pieces = pdefs.ToArray();
        }

        EditorUtility.SetDirty(puzzleAsset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PuzzleExporter] Exported '{puzzleAsset.name}' | Hints={puzzleAsset.hints?.Length ?? 0} Pieces={puzzleAsset.pieces?.Length ?? 0} (Targets disabled)");
    }

    private static string BaseIdFromName(string n)
    {
        string s = n;
        s = s.Replace("_Target", "").Replace("_Hint", "").Replace("_Piece", "");
        s = s.Replace("Target", "").Replace("Hint", "").Replace("Piece", "");
        return s.Trim('_', ' ');
    }

    private static Sprite TryExtractSprite(Transform t)
    {
        var img = t.GetComponentInChildren<Image>(true);
        if (img != null && img.sprite != null) return img.sprite;

        var sr = t.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null && sr.sprite != null) return sr.sprite;

        return null;
    }

    // Uses rendered area when preserveAspect is ON
    private static (Vector2 npos, Vector2 nsize) ComputeNormalizedRect(Image referenceImage, RectTransform child)
    {
        // child corners in world
        Vector3[] childCorners = new Vector3[4];
        child.GetWorldCorners(childCorners);

        Vector2 childMin = childCorners[0];
        Vector2 childMax = childCorners[2];
        Vector2 childSize = childMax - childMin;
        Vector2 childCenter = (childMin + childMax) * 0.5f;

        // reference bounds in world (rendered area if preserveAspect)
        Vector2 refMinW, refMaxW;

        if (referenceImage != null && referenceImage.sprite != null && referenceImage.preserveAspect)
            GetRenderedSpriteWorldBounds(referenceImage, out refMinW, out refMaxW);
        else
            GetRectWorldBounds(referenceImage.rectTransform, out refMinW, out refMaxW);

        Vector2 refSizeW = refMaxW - refMinW;
        if (refSizeW.x <= 0.0001f) refSizeW.x = 1f;
        if (refSizeW.y <= 0.0001f) refSizeW.y = 1f;

        Vector2 npos = new Vector2(
            (childCenter.x - refMinW.x) / refSizeW.x,
            (childCenter.y - refMinW.y) / refSizeW.y
        );

        Vector2 nsize = new Vector2(
            childSize.x / refSizeW.x,
            childSize.y / refSizeW.y
        );

        npos.x = Mathf.Clamp01(npos.x);
        npos.y = Mathf.Clamp01(npos.y);

        nsize.x = Mathf.Max(0f, nsize.x);
        nsize.y = Mathf.Max(0f, nsize.y);

        return (npos, nsize);
    }

    private static void GetRectWorldBounds(RectTransform rt, out Vector2 worldMin, out Vector2 worldMax)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        worldMin = corners[0];
        worldMax = corners[2];
    }

    private static void GetRenderedSpriteWorldBounds(Image img, out Vector2 worldMin, out Vector2 worldMax)
    {
        var rt = img.rectTransform;
        Rect r = rt.rect;

        float rw = r.width;
        float rh = r.height;

        float spriteW = img.sprite.rect.width;
        float spriteH = img.sprite.rect.height;

        float spriteAspect = (spriteH <= 0f) ? 1f : (spriteW / spriteH);
        float rectAspect = (rh <= 0f) ? spriteAspect : (rw / rh);

        float renderW, renderH;
        if (rectAspect > spriteAspect)
        {
            renderH = rh;
            renderW = rh * spriteAspect;
        }
        else
        {
            renderW = rw;
            renderH = rw / spriteAspect;
        }

        Vector2 centerLocal = r.center;
        Vector2 half = new Vector2(renderW * 0.5f, renderH * 0.5f);

        Vector3 bl = rt.TransformPoint(new Vector3(centerLocal.x - half.x, centerLocal.y - half.y, 0f));
        Vector3 tr = rt.TransformPoint(new Vector3(centerLocal.x + half.x, centerLocal.y + half.y, 0f));

        worldMin = bl;
        worldMax = tr;
    }
}
