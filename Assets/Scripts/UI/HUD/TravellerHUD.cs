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
    public GameObject PuzzleSlot;
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

    private void EnsureOverlayImage()
    {
        if (damageOverlayImage != null)
        {
            if (damageOverlaySprite != null)
                damageOverlayImage.sprite = damageOverlaySprite;
            return;
        }

        // אם לא שייכת Image — ניצור אחד אוטומטית מעל הכל בתוך TravellerHUD
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

        // שיהיה מעל כל מה שנמצא תחת TravellerHUD
        go.transform.SetAsLastSibling();
    }

    private void ResetOverlayImmediate()
    {
        if (damageOverlayImage == null)
            return;

        // גם אם הקובץ “אדום עם alpha 255”, אנחנו מאפסים ל-0 בתחילת המשחק
        var c = damageOverlayImage.color;
        c.a = 0f;
        damageOverlayImage.color = c;

        damageOverlayImage.enabled = true; // נשאיר פעיל ונשחק רק עם alpha
    }

    public void UpdateShared(GameManager gm)
    {
        sharedBar?.UpdateValues(gm);
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
        // אדום עולה מהר
        yield return FadeOverlayAlpha(damageOverlayTargetAlpha, overlayFadeIn);

        // פייד אאוט למסך (אם יש)
        if (fadeGroup != null)
            yield return FadeCanvas(fadeGroup, 0f, 1f, fadeOut);

        if (redHoldSeconds > 0f)
            yield return new WaitForSeconds(redHoldSeconds);

        // אדום יורד
        yield return FadeOverlayAlpha(0f, overlayFadeOut);

        // פייד אין
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

    public void ShowMessage(string msg)
    {
        if (messageText == null)
            return;

        messageText.gameObject.SetActive(true);
        messageText.text = msg;
    }

    public void SetMessageColor(Color c)
    {
        if (messageText)
            messageText.color = c;
    }

    public void ShowPuzzle()
    {
        if (!PuzzleSlot)
            return;

        foreach (Transform child in PuzzleSlot.transform)
            child.gameObject.SetActive(true);
    }

    public void HidePuzzle()
    {
        if (!PuzzleSlot)
            return;

        foreach (Transform child in PuzzleSlot.transform)
            child.gameObject.SetActive(false);
    }

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

    public void Clear()
    {
        if (messageText == null)
            return;

        messageText.text = string.Empty;
    }
}
