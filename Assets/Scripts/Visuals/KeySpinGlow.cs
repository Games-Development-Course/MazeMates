using UnityEngine;

public class KeyFloatSpinGlow : MonoBehaviour
{
    [Header("Floating")]
    public float floatSpeed = 2f;
    public float floatAmount = 0.15f;

    [Header("Spinning")]
    public float spinSpeed = 90f;

    [Header("Glow")]
    public Color glowColor = Color.yellow;
    public float glowSpeed = 3;
    public float glowIntensity = 0.001f; // reduced so texture stays visible

    private Vector3 startPos;
    private Material mat;
    private Color baseEmission;

    void Start()
    {
        // Force key to stand upright no matter the model import rotation
        transform.localRotation = Quaternion.Euler(0, 0, 0);

        // Slightly lift from floor
        startPos = transform.localPosition + new Vector3(0, 0.25f, 0);

        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null)
        {
            mat = r.material;

            // Capture existing emission instead of replacing it
            if (mat.HasProperty("_EmissionColor"))
                baseEmission = mat.GetColor("_EmissionColor");
            else
                baseEmission = Color.black;

            mat.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        // Floating
        float offset = Mathf.Sin(Time.time * floatSpeed) * floatAmount;
        transform.localPosition = startPos + new Vector3(0, offset, 0);

        // Spin around world up axis (keeps it upright)
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // Glow pulsing (additive, not destructive)
        if (mat != null)
        {
            float pulse = (Mathf.Sin(Time.time * glowSpeed) + 1f) * 0.5f; // 0 → 1
            Color gl = glowColor * (pulse * glowIntensity);

            // Add glow on top of original emission
            mat.SetColor("_EmissionColor", baseEmission + gl);
        }
    }
}
