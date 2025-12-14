using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DiscoTime : MonoBehaviour
{
    public float hueSpeed = 2f;
    public float intensity = 0.5f;
    public bool active = false; // נדליק את זה מהטוטוריאל

    private Volume volume;
    private ColorAdjustments colorAdjust;

    void Awake()
    {
        volume = GetComponent<Volume>();
        if (volume == null)
            volume = gameObject.AddComponent<Volume>();

        volume.isGlobal = true;
        volume.priority = 100;

        if (volume.profile == null)
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

        if (!volume.profile.TryGet(out colorAdjust))
            colorAdjust = volume.profile.Add<ColorAdjustments>(true);

        colorAdjust.hueShift.overrideState = true;
        colorAdjust.colorFilter.overrideState = true;
    }

    void Update()
    {
        if (colorAdjust == null)
            return;

        if (!active)
        {
            // מצב רגוע כשאין דיסקו
            colorAdjust.hueShift.value = 0;
            colorAdjust.colorFilter.value = Color.white;
            return;
        }

        // דיסקו :)
        float hue = Mathf.Sin(Time.time * hueSpeed) * 180f;
        colorAdjust.hueShift.value = hue;
        colorAdjust.colorFilter.value = GetSmoothRainbowColor() * intensity;
    }

    private Color GetSmoothRainbowColor()
    {
        return new Color(
            Mathf.Sin(Time.time * 2f) * 0.5f + 0.5f,
            Mathf.Sin(Time.time * 2.3f) * 0.5f + 0.5f,
            Mathf.Sin(Time.time * 2.7f) * 0.5f + 0.5f
        );
    }

    public void EnableDisco()
    {
        active = true;
    }

    public void DisableDisco()
    {
        active = false;
    }
}
