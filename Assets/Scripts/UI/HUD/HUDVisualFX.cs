
// /Assets/Scripts/HUD/HUDVisualFX.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HUDVisualFX : MonoBehaviour
{
    public static HUDVisualFX Instance { get; private set; }

    [Header("Wiring")]
    [Tooltip("The HUD Canvas. Overlay is simplest.")]
    [SerializeField] private Canvas canvas;

    [Tooltip("Full-screen RectTransform under the Canvas to hold flying icons.")]
    [SerializeField] private RectTransform flyLayer;

    [Tooltip("World camera used to convert world -> screen. If null, uses Camera.main.")]
    [SerializeField] private Camera worldCamera;

    [Header("Defaults (tuned for ~960x600)")]
    [SerializeField] private Vector2 defaultIconSize = new Vector2(28f, 28f);
    [SerializeField] private float defaultDuration = 0.45f;

    [Tooltip("Arc height as % of flyLayer height (0.1 = 10%).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float arcHeightPercent = 0.16f;

    [Tooltip("Start scale of flying icon.")]
    [SerializeField] private float startScale = 1.0f;

    [Tooltip("End scale of flying icon.")]
    [SerializeField] private float endScale = 0.55f;

    [Tooltip("Fade out near the end (0 = no fade).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float fadeOutTailPercent = 0.15f;

    [Tooltip("Use unscaled time (recommended for UI).")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Impact")]
    [SerializeField] private bool punchTargetOnArrival = true;
    [SerializeField] private float punchScale = 1.15f;
    [SerializeField] private float punchTime = 0.10f;

    [Header("Pooling")]
    [SerializeField] private int prewarm = 8;

    private readonly Queue<FlyIcon> pool = new Queue<FlyIcon>(32);

    private Camera UiCamera =>
        (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (flyLayer == null && canvas != null)
        {
            var existing = canvas.GetComponentInChildren<RectTransform>(true);
            flyLayer = existing;
        }

        Prewarm();
    }

    private void Prewarm()
    {
        for (int i = pool.Count; i < prewarm; i++)
            pool.Enqueue(CreateFlyIcon());
    }

    private FlyIcon CreateFlyIcon()
    {
        var go = new GameObject("FlyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(flyLayer != null ? flyLayer : transform as RectTransform, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;

        var cg = go.GetComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        go.SetActive(false);
        return new FlyIcon(go, rt, img, cg);
    }

    private FlyIcon Rent()
    {
        var icon = pool.Count > 0 ? pool.Dequeue() : CreateFlyIcon();
        icon.Go.SetActive(true);
        icon.Cg.alpha = 1f;
        icon.Rt.localScale = Vector3.one;
        return icon;
    }

    private void Return(FlyIcon icon)
    {
        icon.Go.SetActive(false);
        pool.Enqueue(icon);
    }

    /// <summary>
    /// Spawn an icon at a world position and fly it to a target HUD RectTransform.
    /// </summary>
    public void PlayFlyFromWorldToTarget(
        Sprite sprite,
        Vector3 worldPosition,
        RectTransform target,
        float? duration = null,
        Vector2? iconSize = null,
        Camera overrideWorldCamera = null
    )
    {
        if (sprite == null || target == null || flyLayer == null || canvas == null)
            return;

        var wc = overrideWorldCamera != null ? overrideWorldCamera : (worldCamera != null ? worldCamera : Camera.main);
        if (wc == null)
            return;

        var screenStart = wc.WorldToScreenPoint(worldPosition);
        if (screenStart.z < 0f)
            return;

        if (!TryScreenToLocal(flyLayer, screenStart, out var startLocal))
            return;

        var screenEnd = RectTransformUtility.WorldToScreenPoint(UiCamera, target.position);
        if (!TryScreenToLocal(flyLayer, screenEnd, out var endLocal))
            return;

        StartCoroutine(FlyRoutine(sprite, startLocal, endLocal, target, duration ?? defaultDuration, iconSize ?? defaultIconSize));
    }

    private bool TryScreenToLocal(RectTransform rect, Vector3 screen, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, UiCamera, out local);
    }

    private IEnumerator FlyRoutine(Sprite sprite, Vector2 start, Vector2 end, RectTransform target, float duration, Vector2 iconSize)
    {
        var icon = Rent();
        icon.Image.sprite = sprite;
        icon.Rt.sizeDelta = iconSize;

        icon.Rt.anchoredPosition = start;
        icon.Rt.localScale = Vector3.one * startScale;

        float arcPx = flyLayer.rect.height * arcHeightPercent;
        var mid = (start + end) * 0.5f;
        var control = mid + new Vector2(0f, arcPx);

        float t = 0f;
        float inv = 1f / Mathf.Max(0.001f, duration);

        while (t < 1f)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t = Mathf.Clamp01(t + dt * inv);

            float eased = EaseInOut(t);

            icon.Rt.anchoredPosition = QuadraticBezier(start, control, end, eased);
            float s = Mathf.Lerp(startScale, endScale, eased);
            icon.Rt.localScale = Vector3.one * s;

            if (fadeOutTailPercent > 0f)
            {
                float fadeStart = 1f - fadeOutTailPercent;
                if (t >= fadeStart)
                {
                    float ft = Mathf.InverseLerp(fadeStart, 1f, t);
                    icon.Cg.alpha = 1f - ft;
                }
            }

            yield return null;
        }

        icon.Rt.anchoredPosition = end;
        icon.Cg.alpha = 0f;

        if (punchTargetOnArrival && target != null)
            StartCoroutine(PunchTarget(target));

        Return(icon);
    }

    private IEnumerator PunchTarget(RectTransform target)
    {
        if (target == null)
            yield break;

        var baseScale = target.localScale;
        float half = Mathf.Max(0.01f, punchTime) * 0.5f;

        float t = 0f;
        while (t < half)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;
            float a = Mathf.Clamp01(t / half);
            target.localScale = Vector3.Lerp(baseScale, baseScale * punchScale, a);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;
            float a = Mathf.Clamp01(t / half);
            target.localScale = Vector3.Lerp(baseScale * punchScale, baseScale, a);
            yield return null;
        }

        target.localScale = baseScale;
    }

    private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return (u * u) * a + (2f * u * t) * b + (t * t) * c;
    }

    private static float EaseInOut(float t) => t * t * (3f - 2f * t);

    private sealed class FlyIcon
    {
        public GameObject Go { get; }
        public RectTransform Rt { get; }
        public Image Image { get; }
        public CanvasGroup Cg { get; }

        public FlyIcon(GameObject go, RectTransform rt, Image image, CanvasGroup cg)
        {
            Go = go;
            Rt = rt;
            Image = image;
            Cg = cg;
        }
    }
}