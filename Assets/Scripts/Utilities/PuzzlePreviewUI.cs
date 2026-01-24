using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PuzzlePreviewUI : MonoBehaviour
{
    [Header("Assign")]
    public Puzzle puzzle;
    public RectTransform root;

    [Header("Runtime (read-only)")]
    public RectTransform backgroundRect;

    // ✅ The real drawn sprite area inside BG (preserve-aspect area)
    public RectTransform renderedAreaRect;

    // overlays sit on top of the *rendered area*, not the full BG rect
    public RectTransform overlaysRoot;
    public RectTransform hintsRoot;

    // ✅ OUTER tray (border wraps this)
    public RectTransform trayRoot;

    // ✅ INNER tray content (black panel + pieces live here)
    public RectTransform trayContentRoot;
    public Image trayBgImage;
    public PuzzleTrayLayout2Rows trayLayout;

    private readonly List<RectTransform> _pieces = new();
    private readonly List<RectTransform> _hints = new();

    // ✅ Hints lookup (for show/hide)
    private readonly Dictionary<string, RectTransform> _hintById = new();

    // ✅ Targets lookup (we use hint transforms as targets)
    // key = targetId (usually "Bird", "Cat"...)
    private readonly Dictionary<string, RectTransform> _targetByTargetId = new();

    // For multiple hints per target (if you ever add more than one)
    private readonly Dictionary<string, List<RectTransform>> _hintsByTargetId = new();

    public void Build()
    {
        Clear();

        if (root == null)
        {
            Debug.LogError("[PuzzlePreviewUI] root is NULL.");
            return;
        }
        if (puzzle == null)
        {
            Debug.LogError("[PuzzlePreviewUI] puzzle is NULL.");
            return;
        }

        // ===== BG =====
        var bgGO = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGO.transform.SetParent(root, false);

        backgroundRect = bgGO.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0.55f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.55f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(1100f, 620f);
        backgroundRect.anchoredPosition = Vector2.zero;

        var bgImg = bgGO.GetComponent<Image>();
        bgImg.preserveAspect = true;
        bgImg.color = Color.white;
        bgImg.sprite = (puzzle.backgroundImage != null) ? puzzle.backgroundImage : puzzle.originalImage;

        // ===== Rendered Area (the actual drawn sprite area inside BG when preserveAspect = true) =====
        renderedAreaRect = CreateRenderedArea("RenderedArea", backgroundRect, bgImg);

        // ===== Overlays (stretch over RenderedArea, NOT BG rect) =====
        overlaysRoot = CreateStretchRoot("Overlays", renderedAreaRect);
        hintsRoot = CreateStretchRoot("HintsRoot", overlaysRoot);

        // Make sure rects are up-to-date
        Canvas.ForceUpdateCanvases();
        Vector2 renderSize = (renderedAreaRect != null) ? renderedAreaRect.rect.size : backgroundRect.rect.size;

        // ===== Hints (Image with sprite; also serve as Targets via their RectTransform) =====
        // IMPORTANT:
        // - GameObject stays ACTIVE (so its RectTransform is available as a snap target)
        // - Image component starts DISABLED (so hint is not visible)
        if (puzzle.hints != null)
        {
            foreach (var h in puzzle.hints)
            {
                var go = new GameObject($"Hint_{h.id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(hintsRoot, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                Vector2 ns = h.normalizedSize;

                rt.sizeDelta = (ns.x <= 0.0001f || ns.y <= 0.0001f)
                    ? new Vector2(90f, 90f)
                    : new Vector2(renderSize.x * ns.x, renderSize.y * ns.y);

                rt.anchoredPosition = NormalizedToAnchored(h.normalizedPos, renderSize);

                var img = go.GetComponent<Image>();
                img.sprite = h.sprite;
                img.preserveAspect = true;
                img.color = Color.white;
                img.raycastTarget = false;

                // ✅ start OFF but keep transform alive
                img.enabled = false;

                _hints.Add(rt);

                if (!string.IsNullOrEmpty(h.id))
                    _hintById[h.id] = rt;

                // ✅ use hint transform as target transform
                if (!string.IsNullOrEmpty(h.targetId))
                {
                    // first hint for that target becomes the snap target
                    if (!_targetByTargetId.ContainsKey(h.targetId))
                        _targetByTargetId[h.targetId] = rt;

                    if (!_hintsByTargetId.TryGetValue(h.targetId, out var list))
                    {
                        list = new List<RectTransform>();
                        _hintsByTargetId[h.targetId] = list;
                    }
                    list.Add(rt);
                }
            }
        }

        // ===== TRAY OUTER (no Image here!) =====
        var trayOuterGO = new GameObject("TrayOuter", typeof(RectTransform));
        trayOuterGO.transform.SetParent(root, false);

        trayRoot = trayOuterGO.GetComponent<RectTransform>();
        trayRoot.anchorMin = new Vector2(0.5f, 0.12f);
        trayRoot.anchorMax = new Vector2(0.5f, 0.12f);
        trayRoot.pivot = new Vector2(0.5f, 0.5f);
        trayRoot.sizeDelta = new Vector2(900f, 180f); // spawner overrides
        trayRoot.anchoredPosition = Vector2.zero;

        // ===== TRAY CONTENT (black panel + layout + pieces) =====
        var trayContentGO = new GameObject(
            "TrayContent",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(PuzzleTrayLayout2Rows)
        );
        trayContentGO.transform.SetParent(trayRoot, false);

        trayContentRoot = trayContentGO.GetComponent<RectTransform>();
        trayContentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        trayContentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        trayContentRoot.pivot = new Vector2(0.5f, 0.5f);
        trayContentRoot.sizeDelta = new Vector2(860f, 160f); // spawner overrides
        trayContentRoot.anchoredPosition = Vector2.zero;

        trayBgImage = trayContentGO.GetComponent<Image>();
        trayBgImage.sprite = null;
        trayBgImage.color = new Color(0f, 0f, 0f, 0.15f); // spawner overrides
        trayBgImage.raycastTarget = false;

        trayLayout = trayContentGO.GetComponent<PuzzleTrayLayout2Rows>();
        trayLayout.maxRows = 1;
        trayLayout.centerRow = true;
        trayLayout.autoSizeWidth = true;
        trayLayout.fixedHeight = trayContentRoot.sizeDelta.y;

        // ===== Pieces =====
        Canvas.ForceUpdateCanvases();
        renderSize = (renderedAreaRect != null) ? renderedAreaRect.rect.size : backgroundRect.rect.size;

        if (puzzle.pieces != null)
        {
            foreach (var p in puzzle.pieces)
            {
                var pieceGO = new GameObject($"Piece_{p.id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pieceGO.transform.SetParent(trayContentRoot, false);

                var rt = pieceGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                Vector2 ns = p.normalizedSize;
                rt.sizeDelta = (ns.x <= 0.0001f || ns.y <= 0.0001f)
                    ? new Vector2(140f, 120f)
                    : new Vector2(renderSize.x * ns.x, renderSize.y * ns.y);

                var img = pieceGO.GetComponent<Image>();
                img.sprite = p.sprite;
                img.preserveAspect = true;
                img.color = Color.white;
                img.raycastTarget = true;

                _pieces.Add(rt);
            }
        }

        trayLayout.Rebuild();
    }

    // =========================
    // API: Hints + Targets
    // =========================

    // Hint "on/off" by hintId (enables/disables the Image component only)
    public bool SetHintActive(string hintId, bool on)
    {
        if (string.IsNullOrEmpty(hintId)) return false;

        if (_hintById.TryGetValue(hintId, out var rt) && rt != null)
        {
            var img = rt.GetComponent<Image>();
            if (img != null) img.enabled = on;
            return true;
        }
        return false;
    }

    // Hint(s) "on/off" by targetId (enables/disables the Image component only)
    public int SetHintsActiveForTarget(string targetId, bool on)
    {
        if (string.IsNullOrEmpty(targetId)) return 0;
        if (!_hintsByTargetId.TryGetValue(targetId, out var list) || list == null) return 0;

        int changed = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var rt = list[i];
            if (rt == null) continue;

            var img = rt.GetComponent<Image>();
            if (img != null) img.enabled = on;

            changed++;
        }
        return changed;
    }

    public void HideAllHints()
    {
        foreach (var kv in _hintById)
        {
            if (kv.Value == null) continue;
            var img = kv.Value.GetComponent<Image>();
            if (img != null) img.enabled = false;
        }
    }

    // ✅ Use hint RectTransform as the target transform (snap target)
    public bool TryGetTargetRect(string targetId, out RectTransform targetRt)
    {
        targetRt = null;
        if (string.IsNullOrEmpty(targetId)) return false;
        return _targetByTargetId.TryGetValue(targetId, out targetRt) && targetRt != null;
    }

    // =========================
    // Cleanup
    // =========================

    public void Clear()
    {
        if (root == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var go = root.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        backgroundRect = null;
        renderedAreaRect = null;

        overlaysRoot = null;
        hintsRoot = null;

        trayRoot = null;
        trayContentRoot = null;
        trayBgImage = null;
        trayLayout = null;

        _pieces.Clear();
        _hints.Clear();

        _hintById.Clear();
        _targetByTargetId.Clear();
        _hintsByTargetId.Clear();
    }

    private static RectTransform CreateStretchRoot(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    // Creates a centered rect that matches the actually-rendered sprite area inside BG when preserveAspect is on.
    private static RectTransform CreateRenderedArea(string name, RectTransform bgRect, Image bgImg)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(bgRect, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        // If no sprite or preserveAspect off -> match BG rect
        if (bgImg == null || bgImg.sprite == null || !bgImg.preserveAspect)
        {
            rt.sizeDelta = bgRect.rect.size;
            return rt;
        }

        Vector2 bgSize = bgRect.rect.size;
        float rw = bgSize.x;
        float rh = bgSize.y;

        float sw = bgImg.sprite.rect.width;
        float sh = bgImg.sprite.rect.height;
        float spriteAspect = (sh <= 0.0001f) ? 1f : (sw / sh);
        float rectAspect = (rh <= 0.0001f) ? spriteAspect : (rw / rh);

        float renderW, renderH;
        if (rectAspect > spriteAspect)
        {
            // rect is wider than sprite -> full height, narrower width (pillarbox)
            renderH = rh;
            renderW = rh * spriteAspect;
        }
        else
        {
            // rect is taller -> full width, shorter height (letterbox)
            renderW = rw;
            renderH = rw / spriteAspect;
        }

        rt.sizeDelta = new Vector2(renderW, renderH);
        return rt;
    }

    // normalized 0..1 center -> anchored position in overlay space (centered)
    private static Vector2 NormalizedToAnchored(Vector2 normalizedPos, Vector2 areaSize)
    {
        float x = (normalizedPos.x - 0.5f) * areaSize.x;
        float y = (normalizedPos.y - 0.5f) * areaSize.y;
        return new Vector2(x, y);
    }
}
