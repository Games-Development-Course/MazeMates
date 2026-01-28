using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class RainbowPulseTMP : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private TMP_Text tmp;

    [Header("Rainbow Colors")]
    [Min(0.01f)][SerializeField] private float colorInterval = 0.15f;
    [Range(0f, 1f)][SerializeField] private float saturation = 1f;
    [Range(0f, 1f)][SerializeField] private float value = 1f;
    [SerializeField] private bool perCharacter = true;

    [Header("Font Size Oscillation")]
    [SerializeField] private bool enablePulse = true;
    [Min(0.1f)][SerializeField] private float minFontSize = 28f;
    [Min(0.1f)][SerializeField] private float maxFontSize = 36f;
    [Min(0f)][SerializeField] private float pulseSpeed = 2f; // cycles per second-ish

    [Header("Editor Preview")]
    [SerializeField] private bool previewInEditor = true;

    private double _lastTime;
    private float _colorTimer;
    private float _phase;
    private bool _initialized;

    private void Reset()
    {
        tmp = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (tmp == null) tmp = GetComponent<TMP_Text>();
        _lastTime = GetNow();
        _colorTimer = 0f;
        _phase = 0f;
        _initialized = true;

        ApplyNow(forceColor: true);
    }

    private void OnDisable()
    {
        _initialized = false;
    }

    private void Update()
    {
        if (!_initialized)
            OnEnable();

        // In editor, only animate if previewInEditor is true
        if (!Application.isPlaying && !previewInEditor)
            return;

        double now = GetNow();
        float dt = Mathf.Clamp((float)(now - _lastTime), 0f, 0.1f);
        _lastTime = now;

        // Pulse font size
        if (enablePulse && tmp != null)
        {
            _phase += dt * pulseSpeed * (Mathf.PI * 2f);
            float t = (Mathf.Sin(_phase) + 1f) * 0.5f; // 0..1
            float size = Mathf.Lerp(minFontSize, maxFontSize, t);
            if (!Mathf.Approximately(tmp.fontSize, size))
                tmp.fontSize = size;
        }

        // Rainbow colors
        _colorTimer += dt;
        if (_colorTimer >= colorInterval)
        {
            _colorTimer = 0f;
            ApplyColors();
        }
    }

    private void ApplyNow(bool forceColor)
    {
        if (tmp == null) return;

        // Clamp min/max to safe values
        if (maxFontSize < minFontSize) maxFontSize = minFontSize;

        if (forceColor)
            ApplyColors();
    }

    private void ApplyColors()
    {
        if (tmp == null) return;

        // בדיקה מהירה: אם אתה רוצה לראות שזה עובד, תנסה לשים perCharacter=false
        if (!perCharacter)
        {
            tmp.color = RandomRainbow();
            tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            MarkDirty();
            return;
        }

        tmp.ForceMeshUpdate();

        var textInfo = tmp.textInfo;
        int charCount = textInfo.characterCount;

        for (int i = 0; i < charCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            Color32 c = RandomRainbow();

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            var colors = textInfo.meshInfo[materialIndex].colors32;
            colors[vertexIndex + 0] = c;
            colors[vertexIndex + 1] = c;
            colors[vertexIndex + 2] = c;
            colors[vertexIndex + 3] = c;
        }

        // ✅ זה העדכון הנכון לצבעים ב-TMP (כולל UI)
        tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        MarkDirty();
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(tmp);
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();

            // מרענן גם Scene וגם Game view בעורך
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
#endif
    }

    private Color32 RandomRainbow()
    {
        float h = Random.value; // 0..1
        Color c = Color.HSVToRGB(h, saturation, value);
        return (Color32)c;
    }

    private static double GetNow()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return UnityEditor.EditorApplication.timeSinceStartup;
#endif
        return Time.unscaledTimeAsDouble;
    }

  
}
