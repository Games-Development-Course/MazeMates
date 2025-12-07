using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))]
public class FloorPressurePlateGlow : MonoBehaviour
{
    [Header("Press settings")]
    public float pressDepth = 0.08f;     // כמה לשקוע למטה
    public float pressSpeed = 8f;        // מהירות האנימציה

    [Header("Glow settings")]
    public Color idleColor = Color.gray;           // צבע רגיל
    public Color pressedColor = Color.white;       // צבע כשהפלטה לחוצה

    public Color idleEmissionColor = Color.black;  // Emission במצב רגיל
    public Color pressedEmissionColor = Color.yellow; // Emission לחוץ
    public float emissionIntensity = 2f;           // חוזק ה־Glow

    [Header("Events")]
    public UnityEvent onPressed;                   // מה לעשות כשדורכים (פעם ראשונה)
    public string playerTag = "Player";

    private Vector3 startPos;
    private Renderer rend;
    private Material matInstance;
    private int objectsOnPlate = 0;
    private Coroutine currentAnim;

    void Awake()
    {
        startPos = transform.localPosition;

        var col = GetComponent<Collider>();
        col.isTrigger = true;

        rend = GetComponent<Renderer>();
        // יוצרים עותק חומר כדי לא לשנות לכולם
        matInstance = rend.material;

        // לוודא ש־Emission פעיל
        matInstance.EnableKeyword("_EMISSION");

        // סטייל התחלתי
        SetMaterialColors(idleColor, idleEmissionColor * 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        objectsOnPlate++;

        // אם זו הדריכה הראשונה – מפעילים אנימציה ואיוונט
        if (objectsOnPlate == 1)
        {
            StartPressAnimation(true);
            onPressed?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        objectsOnPlate--;
        if (objectsOnPlate < 0) objectsOnPlate = 0;

        // אם כבר אף אחד לא עומד – חוזרים למעלה
        if (objectsOnPlate == 0)
        {
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
        Vector3 toPos   = pressingDown ? downPos : startPos;

        Color fromColor      = pressingDown ? idleColor : pressedColor;
        Color toColor        = pressingDown ? pressedColor : idleColor;

        Color fromEmission   = pressingDown ? idleEmissionColor * 0f
                                            : pressedEmissionColor * emissionIntensity;
        Color toEmission     = pressingDown ? pressedEmissionColor * emissionIntensity
                                            : idleEmissionColor * 0f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * pressSpeed;
            float lerp = Mathf.SmoothStep(0f, 1f, t);

            // תנועה למטה/למעלה
            transform.localPosition = Vector3.Lerp(fromPos, toPos, lerp);

            // שינוי צבע ו־Emission בהדרגה
            Color currColor    = Color.Lerp(fromColor, toColor, lerp);
            Color currEmission = Color.Lerp(fromEmission, toEmission, lerp);
            SetMaterialColors(currColor, currEmission);

            yield return null;
        }

        // לוודא שנחתנו בדיוק ביעד
        transform.localPosition = toPos;
        SetMaterialColors(toColor, toEmission);

        currentAnim = null;
    }

    private void SetMaterialColors(Color baseColor, Color emissionColor)
    {
        if (matInstance == null) return;

        // סטנדרט / URP Lit
        if (matInstance.HasProperty("_BaseColor"))
            matInstance.SetColor("_BaseColor", baseColor);
        if (matInstance.HasProperty("_Color"))
            matInstance.SetColor("_Color", baseColor);

        if (matInstance.HasProperty("_EmissionColor"))
            matInstance.SetColor("_EmissionColor", emissionColor);
    }
}