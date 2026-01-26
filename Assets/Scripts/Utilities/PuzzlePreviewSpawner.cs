// File: Assets/Scripts/UI/Puzzle/PuzzlePreviewSpawner.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PuzzlePreviewSpawner : MonoBehaviour
{
    public Puzzle puzzle;

    [Header("Canvas (optional; used for scaleFactor)")]
    public Canvas canvas;

    [Header("Auto Run (Editor helper)")]
    public bool autoRunInStart = false;

    [Header("Border")]
    public Sprite borderSprite;
    [Range(0f, 1f)] public float borderAlpha = 1f;

    [Header("Scale With Screen Size support")]
    [Tooltip("If true: gap + offsets are multiplied by Canvas.scaleFactor (CanvasScaler Scale With Screen Size).")]
    public bool scaleOffsetsWithCanvas = true;

    [Header("BG Position (runtime)")]
    public Vector2 bgPosOffset = Vector2.zero;

    [Header("Tray Placement (optional)")]
    public bool snapTrayUnderBG = true;

    [Tooltip("Gap between BG bottom and Tray top (reference pixels).")]
    public float gapBetweenBgAndTray = 0f;

    [Header("Tray Position (runtime)")]
    public Vector2 trayPosOffset = Vector2.zero;

    [Header("Tray Visual")]
    [Range(0f, 1f)] public float trayBgAlpha = 0.35f;

    [Header("Tray Sizing")]
    public float trayHeight = 180f;

    [Header("Tray Layout (single row)")]
    public float trayPaddingLeft = 24f;
    public float trayPaddingRight = 24f;
    public float trayPaddingTop = 0f;
    public float trayPaddingBottom = 0f;
    public float traySpacingX = 14f;

    [Header("Tray Border Padding (inside BG)")]
    public float trayBorderPaddingLeft = 0f;
    public float trayBorderPaddingRight = 0f;
    public float trayBorderPaddingTop = 0f;
    public float trayBorderPaddingBottom = 0f;

    public Vector2 trayBorderOffset = Vector2.zero;
    public float trayBorderPPU = 1f;

    [Header("BG Border (optional)")]
    public bool bgBorderWrapRenderedSpriteArea = true;
    public Vector2 bgBorderPadding = Vector2.zero;
    public Vector2 bgBorderOffset = Vector2.zero;
    public float bgBorderPPU = 1f;

    [Header("Close Button (Sprite)")]
    public bool addCloseButton = true;

    public Sprite closeBtnSprite;               // ✅ הספרייט של ה-X שלך
    [Range(0f, 1f)] public float closeBtnAlpha = 1f;

    public Vector2 closeBtnOffset = new Vector2(-12f, -12f);
    public Vector2 closeBtnSize = new Vector2(48f, 48f);

    public bool closeBtnPreserveAspect = true;
    public bool closeBtnSetNativeSize = false;  // אם true -> יתעלם מ-sizeDelta וייקח מהספרייט

    // ✅ expose UI
    public PuzzlePreviewUI UI => _ui;

    private PuzzlePreviewUI _ui;

    // Our tray structure
    private RectTransform _trayOuter;
    private RectTransform _trayBgRT;
    private Image _trayBgImg;
    private RectTransform _borderTrayRT;
    private Image _borderTrayImg;
    private RectTransform _trayContentRT;
    private PuzzleTrayLayout2Rows _trayLayout;

    // =========================================================
    // ✅ Public API (used by PuzzleDoor)
    // =========================================================

    // Build everything INTO the provided root (no global canvas root creation)
    private void EnsureCloseButtonOnBorderBG()
    {
        if (_ui == null || _ui.root == null) return;

        var borderBG = _ui.root.Find("Border_BG") as RectTransform;
        if (borderBG == null) return;

        // ליצור/למצוא כפתור
        Transform t = borderBG.Find("Btn_ClosePuzzle");
        RectTransform btnRT;
        Image img;
        Button btn;

        if (t == null)
        {
            var go = new GameObject("Btn_ClosePuzzle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(borderBG, false);

            btnRT = go.GetComponent<RectTransform>();
            img = go.GetComponent<Image>();
            btn = go.GetComponent<Button>();

            btn.onClick.AddListener(() =>
            {
                var gm = GameManager.Instance;
                gm?.activePuzzleDoor?.GetPuzzle()?.ForceClosePuzzle();
            });
        }
        else
        {
            btnRT = t as RectTransform;
            img = t.GetComponent<Image>();
            btn = t.GetComponent<Button>();
            if (img == null) img = t.gameObject.AddComponent<Image>();
            if (btn == null) btn = t.gameObject.AddComponent<Button>();
        }

        // ✅ עיגון לימין-עליון של ה-Border_BG
        btnRT.anchorMin = new Vector2(1f, 1f);
        btnRT.anchorMax = new Vector2(1f, 1f);
        btnRT.pivot = new Vector2(1f, 1f);

        // גודל/מיקום
        btnRT.sizeDelta = closeBtnSize;
        btnRT.anchoredPosition = closeBtnOffset;

        // ✅ ספרייט
        img.sprite = closeBtnSprite;
        img.preserveAspect = closeBtnPreserveAspect;
        img.raycastTarget = true;

        var c = Color.white;
        c.a = Mathf.Clamp01(closeBtnAlpha);
        img.color = c;

        // אם רוצים native size של הספרייט
        if (closeBtnSetNativeSize && closeBtnSprite != null)
            img.SetNativeSize();

        btnRT.SetAsLastSibling(); // מעל הכל
    }

    public void BuildInto(RectTransform root, Puzzle puzzle, Canvas canvasOverride = null)
    {
        this.puzzle = puzzle;
        this.canvas = canvasOverride != null ? canvasOverride : (canvas != null ? canvas : root.GetComponentInParent<Canvas>());

        // ensure UI component on that root
        _ui = root.GetComponent<PuzzlePreviewUI>();
        if (_ui == null) _ui = root.gameObject.AddComponent<PuzzlePreviewUI>();

        _ui.puzzle = this.puzzle;
        _ui.root = root;

        _ui.Build();
    }

    // Apply layout/borders now
    public void ApplyNow(bool rebuildAll)
    {
        if (_ui == null) return;
        if (rebuildAll) _ui.Build();

        EnsureTrayStructure();
        ApplyRuntimeAll();
        RebuildBorders();
        EnforceStableUILayers();
    }

    private void Start()
    {
        if (!autoRunInStart) return;

        // editor/debug usage only
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("[PuzzlePreviewSpawner] No Canvas found.");
            return;
        }

        // If someone placed it manually on a root, use this transform as root
        RectTransform root = transform as RectTransform;
        if (root == null)
        {
            Debug.LogError("[PuzzlePreviewSpawner] Put this on a UI RectTransform root.");
            return;
        }

        BuildInto(root, puzzle, canvas);
        ApplyNow(rebuildAll: false);
    }

    private float UIScaleFactor()
    {
        if (!scaleOffsetsWithCanvas) return 1f;
        if (canvas == null) return 1f;
        return Mathf.Max(0.0001f, canvas.scaleFactor);
    }

    // =========================================================
    // ✅ Enforce tray hierarchy:
    // TrayOuter
    //   - TrayBG (Image)
    //       - Border_Tray (Image)
    //   - TrayContent (RectTransform + PuzzleTrayLayout2Rows)  <-- only pieces
    // =========================================================
    private void EnsureTrayStructure()
    {
        if (_ui == null) return;
        if (_ui.trayRoot == null)
        {
            Debug.LogError("[PuzzlePreviewSpawner] PuzzlePreviewUI didn't create trayRoot.");
            return;
        }

        _trayOuter = _ui.trayRoot;

        // ---- TrayBG ----
        Transform bgT = _trayOuter.Find("TrayBG");
        if (bgT == null)
        {
            var bgGO = new GameObject("TrayBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGO.transform.SetParent(_trayOuter, false);
            bgT = bgGO.transform;
        }

        _trayBgRT = bgT as RectTransform;
        _trayBgImg = bgT.GetComponent<Image>();
        if (_trayBgImg == null) _trayBgImg = bgT.gameObject.AddComponent<Image>();

        _trayBgRT.anchorMin = Vector2.zero;
        _trayBgRT.anchorMax = Vector2.one;
        _trayBgRT.pivot = new Vector2(0.5f, 0.5f);
        _trayBgRT.offsetMin = Vector2.zero;
        _trayBgRT.offsetMax = Vector2.zero;

        // ---- Border_Tray as child of TrayBG ----
        Transform borderT = _trayBgRT.Find("Border_Tray");
        if (borderT == null)
        {
            var bgo = new GameObject("Border_Tray", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgo.transform.SetParent(_trayBgRT, false);
            borderT = bgo.transform;
        }

        _borderTrayRT = borderT as RectTransform;
        _borderTrayImg = borderT.GetComponent<Image>();
        if (_borderTrayImg == null) _borderTrayImg = borderT.gameObject.AddComponent<Image>();

        // ---- TrayContent ----
        // Prefer PreviewUI's trayContentRoot if exists
        if (_ui.trayContentRoot != null)
        {
            _trayContentRT = _ui.trayContentRoot;
        }
        else
        {
            Transform contentT = _trayOuter.Find("TrayContent");
            if (contentT == null)
            {
                var cgo = new GameObject("TrayContent", typeof(RectTransform));
                cgo.transform.SetParent(_trayOuter, false);
                contentT = cgo.transform;
            }
            _trayContentRT = contentT as RectTransform;
            _ui.trayContentRoot = _trayContentRT;
        }

        _trayContentRT.anchorMin = new Vector2(0.5f, 0.5f);
        _trayContentRT.anchorMax = new Vector2(0.5f, 0.5f);
        _trayContentRT.pivot = new Vector2(0.5f, 0.5f);
        _trayContentRT.anchoredPosition = Vector2.zero;

        // Layout on content
        _trayLayout = _trayContentRT.GetComponent<PuzzleTrayLayout2Rows>();
        if (_trayLayout == null) _trayLayout = _trayContentRT.gameObject.AddComponent<PuzzleTrayLayout2Rows>();

        // drive outer size from layout (wrap)
        _trayLayout.outerRectToResize = _trayOuter;
        _trayLayout.outerExtraSize = Vector2.zero;

        // ignore BG subtree
        _trayLayout.ignoreSubtreeRoot = _trayBgRT;

        // If content has an Image from older builds, disable its visuals (BG handles visuals now)
        var contentImg = _trayContentRT.GetComponent<Image>();
        if (contentImg != null)
        {
            var c = contentImg.color;
            c.a = 0f;
            contentImg.color = c;
            contentImg.raycastTarget = false;
        }
    }

    private void ApplyRuntimeAll()
    {
        if (_ui == null) return;
        if (_trayOuter == null) return;

        float s = UIScaleFactor();

        Vector2 bgOffset = bgPosOffset * s;
        Vector2 trayOffset = trayPosOffset * s;
        float gap = gapBetweenBgAndTray * s;

        // BG offset
        if (_ui.backgroundRect != null)
            _ui.backgroundRect.anchoredPosition = bgOffset;

        // tray outer anchors in root space
        _trayOuter.anchorMin = new Vector2(0.5f, 0.5f);
        _trayOuter.anchorMax = new Vector2(0.5f, 0.5f);
        _trayOuter.pivot = new Vector2(0.5f, 0.5f);

        // Tray BG color
        if (_trayBgImg != null)
        {
            _trayBgImg.sprite = null;
            _trayBgImg.color = new Color(0f, 0f, 0f, Mathf.Clamp01(trayBgAlpha));
            _trayBgImg.raycastTarget = false;
        }

        // Layout params
        if (_trayLayout != null)
        {
            _trayLayout.maxRows = 1;
            _trayLayout.autoSizeWidth = true;
            _trayLayout.centerRow = true;

            _trayLayout.fixedHeight = Mathf.Max(1f, trayHeight * s);

            _trayLayout.paddingLeft = trayPaddingLeft * s;
            _trayLayout.paddingRight = trayPaddingRight * s;
            _trayLayout.paddingTop = trayPaddingTop * s;
            _trayLayout.paddingBottom = trayPaddingBottom * s;
            _trayLayout.spacingX = traySpacingX * s;

            _trayLayout.Rebuild();
        }

        // Place tray under BG if requested
        Vector2 pos = trayOffset;
        if (snapTrayUnderBG && _ui.backgroundRect != null && _ui.root != null)
        {
            Canvas.ForceUpdateCanvases();

            float bgBottomY = GetBottomY_InRootSpace(_ui.backgroundRect, _ui.root);
            float trayHalfH = _trayOuter.rect.height * 0.5f;

            pos.y = bgBottomY - gap - trayHalfH;
        }

        _trayOuter.anchoredPosition = pos;

        CreateOrUpdateBorder_BG("Border_BG", _ui.backgroundRect, bgBorderPadding, bgBorderOffset, bgBorderPPU);

        if (addCloseButton)
            EnsureCloseButtonOnBorderBG();

        Canvas.ForceUpdateCanvases();
    }

    private void RebuildBorders()
    {
        if (_ui == null || _ui.root == null) return;
        if (borderSprite == null) return;

        Canvas.ForceUpdateCanvases();

        // BG border
        if (_ui.backgroundRect != null)
            CreateOrUpdateBorder_BG("Border_BG", _ui.backgroundRect, bgBorderPadding, bgBorderOffset, bgBorderPPU);

        // Tray border (child of TrayBG)
        if (_borderTrayRT != null && _borderTrayImg != null)
        {
            _borderTrayRT.anchorMin = Vector2.zero;
            _borderTrayRT.anchorMax = Vector2.one;
            _borderTrayRT.pivot = new Vector2(0.5f, 0.5f);

            _borderTrayRT.offsetMin = new Vector2(trayBorderPaddingLeft, trayBorderPaddingBottom);
            _borderTrayRT.offsetMax = new Vector2(-trayBorderPaddingRight, -trayBorderPaddingTop);
            _borderTrayRT.anchoredPosition = trayBorderOffset;

            ApplyBorderStyleRobust(_borderTrayImg, trayBorderPPU);

            _borderTrayRT.SetAsLastSibling();
        }
    }

    private void EnforceStableUILayers()
    {
        if (_ui == null || _ui.root == null) return;

        // Root order: BG -> Border_BG -> TrayOuter
        int idx = 0;
        if (_ui.backgroundRect != null) _ui.backgroundRect.SetSiblingIndex(idx++);
        var borderBG = _ui.root.Find("Border_BG");
        if (borderBG != null) borderBG.SetSiblingIndex(idx++);
        if (_trayOuter != null) _trayOuter.SetSiblingIndex(idx++);

        // In tray: BG behind, content above, border above all
        if (_trayBgRT != null) _trayBgRT.SetSiblingIndex(0);
        if (_trayContentRT != null) _trayContentRT.SetSiblingIndex(1);
        if (_borderTrayRT != null) _borderTrayRT.SetAsLastSibling();
    }

    // =========================
    // BG Border helper
    // =========================
    private void CreateOrUpdateBorder_BG(string name, RectTransform bgRect, Vector2 uniformPadding, Vector2 offset, float ppuMult)
    {
        RectTransform root = _ui.root;

        Vector2 min, max;

        if (bgBorderWrapRenderedSpriteArea)
        {
            var img = bgRect.GetComponent<Image>();
            bool canUse = img != null && img.sprite != null && img.preserveAspect && bgRect.rect.width > 1f && bgRect.rect.height > 1f;

            if (canUse)
                GetRenderedSpriteBoundsInRoot(img, root, out min, out max);
            else
                GetRectTransformBoundsInRoot(bgRect, root, out min, out max);
        }
        else
        {
            GetRectTransformBoundsInRoot(bgRect, root, out min, out max);
        }

        min -= uniformPadding;
        max += uniformPadding;

        Vector2 size = max - min;
        Vector2 center = (min + max) * 0.5f + offset;

        RectTransform brt; Image bimg;
        GetOrCreateBorderGO(root, name, out brt, out bimg);

        brt.anchorMin = new Vector2(0.5f, 0.5f);
        brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = size;
        brt.anchoredPosition = center;

        ApplyBorderStyleRobust(bimg, ppuMult);
    }

    private void ApplyBorderStyleRobust(Image img, float ppuMult)
    {
        img.sprite = borderSprite;
        img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(borderAlpha));
        img.raycastTarget = false;
        img.pixelsPerUnitMultiplier = Mathf.Max(0.001f, ppuMult);

        Vector4 b = borderSprite != null ? borderSprite.border : Vector4.zero;
        bool hasSlicing = (b.x + b.y + b.z + b.w) > 0.01f;

        if (hasSlicing)
        {
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
        }
        else
        {
            img.type = Image.Type.Simple;
        }
    }

    // =========================
    // Helpers
    // =========================
    private float GetBottomY_InRootSpace(RectTransform rt, RectTransform root)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        float minY = float.PositiveInfinity;
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = root.InverseTransformPoint(corners[i]);
            if (p.y < minY) minY = p.y;
        }
        return minY;
    }

    private void GetOrCreateBorderGO(RectTransform root, string name, out RectTransform brt, out Image img)
    {
        Transform t = root.Find(name);
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root, false);
            brt = go.GetComponent<RectTransform>();
            img = go.GetComponent<Image>();
        }
        else
        {
            brt = t.GetComponent<RectTransform>();
            img = t.GetComponent<Image>();
            if (img == null) img = t.gameObject.AddComponent<Image>();
        }
    }

    private void GetRectTransformBoundsInRoot(RectTransform rt, RectTransform root, out Vector2 min, out Vector2 max)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < 4; i++)
        {
            Vector2 p = root.InverseTransformPoint(corners[i]);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
    }
    [Header("Live Tuning (Play Mode)")]
    public bool liveUpdateInPlayMode = true;

    [Tooltip("How often to re-apply while playing (seconds). 0 = every frame.")]
    [Min(0f)] public float liveUpdateInterval = 0.05f;

    private float _nextLiveApplyTime;

    // a cheap “fingerprint” of the fields that affect layout
    private int _lastHash;

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (!liveUpdateInPlayMode) return;
        if (_ui == null) return;

        if (liveUpdateInterval > 0f && Time.unscaledTime < _nextLiveApplyTime)
            return;

        int h = ComputeLiveHash();
        if (h == _lastHash)
        {
            _nextLiveApplyTime = Time.unscaledTime + liveUpdateInterval;
            return;
        }

        _lastHash = h;
        _nextLiveApplyTime = Time.unscaledTime + liveUpdateInterval;

        // re-apply runtime layout without rebuilding whole UI
        ApplyNow(rebuildAll: false);
    }

    private int ComputeLiveHash()
    {
        // include anything you want to respond to instantly
        HashCode hc = new HashCode();

        hc.Add(puzzle);

        hc.Add(scaleOffsetsWithCanvas);
        hc.Add(bgPosOffset);
        hc.Add(snapTrayUnderBG);
        hc.Add(gapBetweenBgAndTray);
        hc.Add(trayPosOffset);

        hc.Add(trayBgAlpha);
        hc.Add(trayHeight);

        hc.Add(trayPaddingLeft);
        hc.Add(trayPaddingRight);
        hc.Add(trayPaddingTop);
        hc.Add(trayPaddingBottom);
        hc.Add(traySpacingX);

        hc.Add(trayBorderPaddingLeft);
        hc.Add(trayBorderPaddingRight);
        hc.Add(trayBorderPaddingTop);
        hc.Add(trayBorderPaddingBottom);
        hc.Add(trayBorderOffset);
        hc.Add(trayBorderPPU);

        hc.Add(bgBorderWrapRenderedSpriteArea);
        hc.Add(bgBorderPadding);
        hc.Add(bgBorderOffset);
        hc.Add(bgBorderPPU);

        hc.Add(borderSprite);
        hc.Add(borderAlpha);

        hc.Add(addCloseButton);
        hc.Add(closeBtnSprite);
        hc.Add(closeBtnAlpha);
        hc.Add(closeBtnOffset);
        hc.Add(closeBtnSize);
        hc.Add(closeBtnPreserveAspect);
        hc.Add(closeBtnSetNativeSize);

        return hc.ToHashCode();
    }
    private void GetRenderedSpriteBoundsInRoot(Image img, RectTransform root, out Vector2 min, out Vector2 max)
    {
        RectTransform rt = img.rectTransform;
        Rect r = rt.rect;

        float rw = r.width;
        float rh = r.height;

        float spriteW = img.sprite.rect.width;
        float spriteH = img.sprite.rect.height;
        float spriteAspect = (spriteH <= 0f) ? 1f : (spriteW / spriteH);

        float rectAspect = (rh <= 0f) ? spriteAspect : (rw / rh);

        float renderW, renderH;
        if (rectAspect > spriteAspect) { renderH = rh; renderW = rh * spriteAspect; }
        else { renderW = rw; renderH = rw / spriteAspect; }

        Vector2 centerLocal = r.center;
        Vector2 half = new Vector2(renderW * 0.5f, renderH * 0.5f);

        Vector3 bl = rt.TransformPoint(new Vector3(centerLocal.x - half.x, centerLocal.y - half.y, 0f));
        Vector3 tl = rt.TransformPoint(new Vector3(centerLocal.x - half.x, centerLocal.y + half.y, 0f));
        Vector3 tr = rt.TransformPoint(new Vector3(centerLocal.x + half.x, centerLocal.y + half.y, 0f));
        Vector3 br = rt.TransformPoint(new Vector3(centerLocal.x + half.x, centerLocal.y - half.y, 0f));

        Vector2 p0 = root.InverseTransformPoint(bl);
        Vector2 p1 = root.InverseTransformPoint(tl);
        Vector2 p2 = root.InverseTransformPoint(tr);
        Vector2 p3 = root.InverseTransformPoint(br);

        min = Vector2.Min(Vector2.Min(p0, p1), Vector2.Min(p2, p3));
        max = Vector2.Max(Vector2.Max(p0, p1), Vector2.Max(p2, p3));
    }
}
