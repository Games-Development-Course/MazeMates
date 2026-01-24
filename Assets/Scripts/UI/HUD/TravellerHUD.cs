// File: Assets/Scripts/UI/HUD/TravellerHUD.cs
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TravellerHUD : MonoBehaviour
{
    [Header("Shared Bar (placed manually)")]
    public HUDShared sharedBar;
    public RectTransform barParent;

    [Header("Traveller UI")]
    public TMP_Text messageText;

    [Tooltip("Root object of the whole puzzle UI (the parent that contains everything).")]
    [SerializeField] private GameObject puzzleRoot;

    // ✅ NEW: Single root where we spawn EVERYTHING (BG + hints + tray + pieces)
    [Header("Puzzle Single Root (NEW)")]
    [Tooltip("All puzzle runtime UI will be created UNDER this RectTransform only.")]
    [SerializeField] private RectTransform puzzleSingleRoot;

    // ✅ Defaults for runtime-created spawner (set in Inspector)
    [System.Serializable]
    public class PuzzleSpawnerDefaults
    {
        [Header("Border")]
        public Sprite borderSprite;
        [Range(0f, 1f)] public float borderAlpha = 1f;

        [Header("Scale With Screen Size support")]
        public bool scaleOffsetsWithCanvas = true;

        [Header("BG Position (runtime)")]
        public Vector2 bgPosOffset = Vector2.zero;

        [Header("Tray Placement (optional)")]
        public bool snapTrayUnderBG = true;
        public float gapBetweenBgAndTray = 100f;

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
    }

    [Header("Puzzle Spawner Defaults (NEW)")]
    [SerializeField] private PuzzleSpawnerDefaults puzzleSpawnerDefaults = new PuzzleSpawnerDefaults();

    // ✅ Called by PuzzleDoor right after AddComponent<PuzzlePreviewSpawner>()
    public void ApplyPuzzleSpawnerDefaults(PuzzlePreviewSpawner s)
    {
        if (s == null) return;
        var d = puzzleSpawnerDefaults;
        if (d == null) return;

        s.borderSprite = d.borderSprite;
        s.borderAlpha = d.borderAlpha;

        s.scaleOffsetsWithCanvas = d.scaleOffsetsWithCanvas;

        s.bgPosOffset = d.bgPosOffset;

        s.snapTrayUnderBG = d.snapTrayUnderBG;
        s.gapBetweenBgAndTray = d.gapBetweenBgAndTray;

        s.trayPosOffset = d.trayPosOffset;

        s.trayBgAlpha = d.trayBgAlpha;

        s.trayHeight = d.trayHeight;

        s.trayPaddingLeft = d.trayPaddingLeft;
        s.trayPaddingRight = d.trayPaddingRight;
        s.trayPaddingTop = d.trayPaddingTop;
        s.trayPaddingBottom = d.trayPaddingBottom;
        s.traySpacingX = d.traySpacingX;

        s.trayBorderPaddingLeft = d.trayBorderPaddingLeft;
        s.trayBorderPaddingRight = d.trayBorderPaddingRight;
        s.trayBorderPaddingTop = d.trayBorderPaddingTop;
        s.trayBorderPaddingBottom = d.trayBorderPaddingBottom;

        s.trayBorderOffset = d.trayBorderOffset;
        s.trayBorderPPU = d.trayBorderPPU;

        s.bgBorderWrapRenderedSpriteArea = d.bgBorderWrapRenderedSpriteArea;
        s.bgBorderPadding = d.bgBorderPadding;
        s.bgBorderOffset = d.bgBorderOffset;
        s.bgBorderPPU = d.bgBorderPPU;
    }


    // (legacy fields - keep so nothing else breaks in your scene)
    [Header("Puzzle Containers (legacy - optional)")]
    [Tooltip("UI/Puzzle/PuzzleScreen/Border/Content")]
    [SerializeField] private RectTransform puzzleScreenContent;

    [Header("Puzzle Objects refs (legacy - optional)")]
    [SerializeField] private RectTransform puzzleObjectsPanel;   // PuzzleObjects (Panel)
    [SerializeField] private RectTransform puzzleObjectsBorder;  // PuzzleObjects/Border
    [SerializeField] private RectTransform puzzleObjectsContent; // PuzzleObjects/Border/Content (optional)

    public GameObject PuzzleSlot => puzzleRoot;

    // ✅ Use this everywhere from now on
    public RectTransform PuzzleSingleRoot => puzzleSingleRoot != null ? puzzleSingleRoot : puzzleScreenContent;

    // legacy getters (keep)
    public RectTransform PuzzleScreenContent => puzzleScreenContent;
    public RectTransform PuzzleObjectsContent => puzzleObjectsContent;
    public RectTransform PuzzleObjectsPanel => puzzleObjectsPanel;
    public RectTransform PuzzleObjectsBorder => puzzleObjectsBorder;

    [Header("Life Flash")]
    public Image[] lifeFlashIcons;

    [Header("Start Level Message")]
    [Tooltip("Message shown at the start of the level (leave empty to disable)")]
    public string startLevelMessage = "";
    public float startMessageDuration = 3f;
    public bool showStartMessageOnStart = true;
    [Tooltip("If true, TravellerHUD will subscribe to GameManager.OnLevelStarted instead of showing the message in Start().")]
    public bool useGameManagerEvent = true;

    [Header("Effects - Bomb Overlay")]
    [Tooltip("הספרייט האדום שלך (1920x1080)")]
    public Sprite damageOverlaySprite;

    [Tooltip("ה-UI Image שעליו נשים את הספרייט. אם לא תשייך, הסקריפט ייצור אחד אוטומטית.")]
    public Image damageOverlayImage;

    [Range(0f, 1f)]
    public float damageOverlayTargetAlpha = 0.65f;

    public float overlayFadeIn = 0.05f;
    public float overlayFadeOut = 0.10f;

    [Header("Optional Fade To Black")]
    public CanvasGroup fadeGroup; // CanvasGroup (alpha=0)

    private bool flashing = false;
    private Coroutine bombEffectCo;

    public void SyncPuzzleObjectsPanelToBorder()
    {
        if (!puzzleObjectsPanel || !puzzleObjectsBorder) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(puzzleObjectsBorder);

        if (puzzleObjectsContent)
            LayoutRebuilder.ForceRebuildLayoutImmediate(puzzleObjectsContent);

        puzzleObjectsPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, puzzleObjectsBorder.rect.height);
    }

    private void Awake()
    {
        if (!sharedBar)
            sharedBar = GetComponentInChildren<HUDShared>(true);
    }

    private Coroutine startMessageCo;
    private bool _subscribedToGM = false;

    private void Start()
    {
        if (!sharedBar)
            sharedBar = GetComponentInChildren<HUDShared>(true);

        EnsureOverlayImage();
        ResetOverlayImmediate();

        if (fadeGroup != null)
            fadeGroup.alpha = 0f;

        // Puzzle hidden by default
        if (puzzleRoot != null)
            puzzleRoot.SetActive(false);

        HUDManager.Instance?.UpdateHUD();

        if (!useGameManagerEvent)
        {
            if (showStartMessageOnStart && !string.IsNullOrEmpty(startLevelMessage))
            {
                if (startMessageCo != null)
                    StopCoroutine(startMessageCo);

                startMessageCo = StartCoroutine(ShowMessageRoutine(startLevelMessage, startMessageDuration));
            }
        }
    }

    public void ShowPuzzle()
    {
        if (puzzleRoot != null)
            puzzleRoot.SetActive(true);

        // (legacy) optional
        SyncPuzzleObjectsPanelToBorder();
    }

    private void OnEnable()
    {
        TrySubscribeToGameManager();
    }

    private void OnDisable()
    {
        if (_subscribedToGM && GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelStarted -= OnGameManagerLevelStarted;
            _subscribedToGM = false;
        }
    }

    private void TrySubscribeToGameManager()
    {
        if (!useGameManagerEvent) return;
        if (_subscribedToGM) return;
        if (GameManager.Instance == null) return;

        GameManager.Instance.OnLevelStarted += OnGameManagerLevelStarted;
        _subscribedToGM = true;
    }

    private void OnGameManagerLevelStarted()
    {
        if (startMessageCo != null)
            StopCoroutine(startMessageCo);

        startMessageCo = StartCoroutine(ShowMessageRoutine(startLevelMessage, startMessageDuration));
    }

    private IEnumerator ShowMessageRoutine(string msg, float duration)
    {
        ShowMessage(msg);

        if (duration > 0f)
            yield return new WaitForSeconds(duration);
        else
            yield return null;

        Clear();
        startMessageCo = null;
    }

    public void HidePuzzle()
    {
        if (puzzleRoot != null)
            puzzleRoot.SetActive(false);
    }

    /// <summary>
    /// ✅ NEW: Clears ONLY runtime puzzle content under the SINGLE ROOT.
    /// </summary>
    public void ClearPuzzleRuntimeContentSingleRoot()
    {
        ClearChildren(PuzzleSingleRoot);
    }

    /// <summary>
    /// (legacy) Clears runtime spawned puzzle content under the old split roots (if you still use them).
    /// </summary>
    public void ClearPuzzleRuntimeContent()
    {
        ClearChildren(puzzleScreenContent);
        ClearChildren(puzzleObjectsContent);
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var ch = parent.GetChild(i);
            if (ch != null)
                Destroy(ch.gameObject);
        }
    }

    // ---------------- Shared HUD ----------------

    public void UpdateShared(GameManager gm)
    {
        sharedBar?.UpdateValues(gm);
    }

    public void ShowMessage(string msg)
    {
        if (messageText == null) return;
        messageText.gameObject.SetActive(true);
        messageText.text = msg;
    }

    public void SetMessageColor(Color c)
    {
        if (messageText) messageText.color = c;
    }

    public void Clear()
    {
        if (messageText == null) return;
        messageText.text = string.Empty;
    }

    // ---------------- Bomb Overlay ----------------

    private void EnsureOverlayImage()
    {
        if (damageOverlayImage != null)
        {
            if (damageOverlaySprite != null)
                damageOverlayImage.sprite = damageOverlaySprite;
            return;
        }

        var go = new GameObject(
            "DamageOverlay_Auto",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        damageOverlayImage = go.GetComponent<Image>();
        damageOverlayImage.raycastTarget = false;
        damageOverlayImage.preserveAspect = false;

        if (damageOverlaySprite != null)
            damageOverlayImage.sprite = damageOverlaySprite;

        go.transform.SetAsLastSibling();
    }

    private void ResetOverlayImmediate()
    {
        if (damageOverlayImage == null)
            return;

        var c = damageOverlayImage.color;
        c.a = 0f;
        damageOverlayImage.color = c;

        damageOverlayImage.enabled = true;
    }

    public void PlayBombResetEffect(float redHoldSeconds, float fadeOut, float fadeIn)
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (bombEffectCo != null)
            StopCoroutine(bombEffectCo);

        bombEffectCo = StartCoroutine(BombResetEffectRoutine(redHoldSeconds, fadeOut, fadeIn));
    }

    private IEnumerator BombResetEffectRoutine(float redHoldSeconds, float fadeOut, float fadeIn)
    {
        yield return FadeOverlayAlpha(damageOverlayTargetAlpha, overlayFadeIn);

        if (fadeGroup != null)
            yield return FadeCanvas(fadeGroup, 0f, 1f, fadeOut);

        if (redHoldSeconds > 0f)
            yield return new WaitForSeconds(redHoldSeconds);

        yield return FadeOverlayAlpha(0f, overlayFadeOut);

        if (fadeGroup != null)
            yield return FadeCanvas(fadeGroup, 1f, 0f, fadeIn);

        bombEffectCo = null;
    }

    private IEnumerator FadeOverlayAlpha(float targetAlpha, float t)
    {
        if (damageOverlayImage == null)
            yield break;

        if (t <= 0f)
        {
            var c0 = damageOverlayImage.color;
            c0.a = targetAlpha;
            damageOverlayImage.color = c0;
            yield break;
        }

        float startA = damageOverlayImage.color.a;
        float time = 0f;

        while (time < t)
        {
            time += Time.deltaTime;
            float a = Mathf.Lerp(startA, targetAlpha, time / t);

            var c = damageOverlayImage.color;
            c.a = a;
            damageOverlayImage.color = c;

            yield return null;
        }

        var final = damageOverlayImage.color;
        final.a = targetAlpha;
        damageOverlayImage.color = final;
    }

    private IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float t)
    {
        if (cg == null)
            yield break;

        cg.alpha = from;

        if (t <= 0f)
        {
            cg.alpha = to;
            yield break;
        }

        float time = 0f;
        while (time < t)
        {
            time += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, time / t);
            yield return null;
        }

        cg.alpha = to;
    }

    // ---------------- Lives Flash ----------------

    public void FlashLives()
    {
        if (!gameObject.activeInHierarchy || flashing)
            return;

        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        flashing = true;

        for (int i = 0; i < 4; i++)
        {
            foreach (var icon in lifeFlashIcons)
                if (icon)
                    icon.enabled = !icon.enabled;

            yield return new WaitForSeconds(0.15f);
        }

        foreach (var icon in lifeFlashIcons)
            if (icon)
                icon.enabled = true;

        flashing = false;
    }
}
