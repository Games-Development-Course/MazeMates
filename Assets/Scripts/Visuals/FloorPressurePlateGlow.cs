using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))]
public class FloorPressurePlateGlow : MonoBehaviour
{
    [Header("Press settings")]
    public float pressDepth = 0.08f; // כמה לשקוע למטה
    public float pressSpeed = 8f; // מהירות האנימציה

    [Header("Glow settings")]
    public Color idleColor = Color.gray; // צבע רגיל
    public Color pressedColor = Color.white; // צבע כשהפלטה לחוצה

    public Color idleEmissionColor = Color.black; // Emission במצב רגיל
    public Color pressedEmissionColor = Color.yellow; // Emission לחוץ
    public float emissionIntensity = 2f; // חוזק ה־Glow

    [Header("Events")]
    public UnityEvent onPressed; // מה לעשות כשדורכים (פעם אחת)
    public string playerTag = "Player";

    private Vector3 startPos;
    private Renderer rend;
    private Material matInstance;

    private int objectsOnPlate = 0;
    private bool isPressed = false; // ✅ האם הפלטה כרגע לחוצה
    private Coroutine currentAnim;

    void Awake()
    {
        startPos = transform.localPosition;

        var col = GetComponent<Collider>();
        col.isTrigger = true;

        rend = GetComponent<Renderer>();
        matInstance = rend.material;
        matInstance.EnableKeyword("_EMISSION");

        // לקחת את הצבע ההתחלתי כחופשי (idle)
        if (matInstance.HasProperty("_BaseColor"))
            idleColor = matInstance.GetColor("_BaseColor");
        else if (matInstance.HasProperty("_Color"))
            idleColor = matInstance.GetColor("_Color");
        else
            idleColor = matInstance.color;

        // start with no emission
        SetMaterialColors(idleColor, idleEmissionColor * 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        objectsOnPlate++;

        // ✅ מעבר מ"לא לחוץ" ל"לחוץ" – מפעיל פעם אחת בלבד
        if (!isPressed)
        {
            isPressed = true;
            StartPressAnimation(true);
            onPressed?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        objectsOnPlate--;
        if (objectsOnPlate < 0)
            objectsOnPlate = 0;

        // ✅ רק כשהפלטה ננטשת לגמרי – חוזרים למעלה
        if (objectsOnPlate == 0 && isPressed)
        {
            isPressed = false;
            StartPressAnimation(false);
        }
    }

    private void StartPressAnimation(bool pressingDown)
    {
        if (currentAnim != null)
            StopCoroutine(currentAnim);

        currentAnim = StartCoroutine(PressAnimation(pressingDown));
    }

    private IEnumerator PressAnimation(bool pressingDown)
    {
        Vector3 downPos = startPos - transform.up * pressDepth;

        Vector3 fromPos = pressingDown ? startPos : downPos;
        Vector3 toPos = pressingDown ? downPos : startPos;

        Color fromColor = pressingDown ? idleColor : pressedColor;
        Color toColor = pressingDown ? pressedColor : idleColor;

        Color fromEmission = pressingDown
            ? idleEmissionColor * 0f
            : pressedEmissionColor * emissionIntensity;
        Color toEmission = pressingDown
            ? pressedEmissionColor * emissionIntensity
            : idleEmissionColor * 0f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * pressSpeed;
            float lerp = Mathf.SmoothStep(0f, 1f, t);

            transform.localPosition = Vector3.Lerp(fromPos, toPos, lerp);

            Color currColor = Color.Lerp(fromColor, toColor, lerp);
            Color currEmission = Color.Lerp(fromEmission, toEmission, lerp);
            SetMaterialColors(currColor, currEmission);

            yield return null;
        }

        transform.localPosition = toPos;
        SetMaterialColors(toColor, toEmission);

        currentAnim = null;
    }

    public void RefreshStartPosition()
    {
        startPos = transform.localPosition;
    }

    private void SetMaterialColors(Color baseColor, Color emissionColor)
    {
        if (matInstance == null)
            return;

        if (matInstance.HasProperty("_BaseColor"))
            matInstance.SetColor("_BaseColor", baseColor);
        if (matInstance.HasProperty("_Color"))
            matInstance.SetColor("_Color", baseColor);

        if (matInstance.HasProperty("_EmissionColor"))
            matInstance.SetColor("_EmissionColor", emissionColor);
    }
}
