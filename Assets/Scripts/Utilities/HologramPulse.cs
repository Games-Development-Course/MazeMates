using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class HologramPulse : MonoBehaviour
{
    public float min = 1.2f;
    public float max = 2.2f;
    public float speed = 1f;

    private Material mat;

    void Awake()
    {
        // material יוצר instance ייחודי – טוב להולוגרמה
        mat = GetComponent<Renderer>().material;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        float e = Mathf.Lerp(min, max, t);
        mat.SetColor("_EmissionColor", Color.cyan * e);
    }
}
