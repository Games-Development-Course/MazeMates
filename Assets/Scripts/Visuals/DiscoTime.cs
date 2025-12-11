using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DiscoCamera : MonoBehaviour
{
    public float speed = 2f;
    public float intensity = 0.6f; // כמה חזק הצבע משפיע

    private Volume volume;
    private ColorAdjustments colorAdjust;

    void Start()
    {
        volume = GetComponent<Volume>();

        if (volume == null)
        {
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
        }

        // לייצר Color Adjustments
        colorAdjust = ScriptableObject.CreateInstance<ColorAdjustments>();
        colorAdjust.hueShift.overrideState = true;
        colorAdjust.colorFilter.overrideState = true;

        // להוסיף ל-volume
        var profile = volume.sharedProfile ?? volume.profile;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;
        }

        profile.Add(colorAdjust);
    }

    void Update()
    {
        // צבע דיסקו מתחלף חלק
        float t = Mathf.Sin(Time.time * speed);

        // הזזה של Hue - מסובב את גלגל הצבעים
        colorAdjust.hueShift.value = t * 180f;

        // הפילטר שולט בחוזק הצבע על המסך
        colorAdjust.colorFilter.value = Color.Lerp(Color.white, RandomColor(), intensity);
    }

    private Color RandomColor()
    {
        return new Color(
            Mathf.Sin(Time.time * 1.3f) * 0.5f + 0.5f,
            Mathf.Sin(Time.time * 1.7f) * 0.5f + 0.5f,
            Mathf.Sin(Time.time * 2.1f) * 0.5f + 0.5f
        );
    }
}
