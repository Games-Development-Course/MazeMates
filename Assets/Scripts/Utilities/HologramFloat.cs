using UnityEngine;

public class HologramFloat : MonoBehaviour
{
    public float amplitude = 0.01f;
    public float speed = 1f;

    Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * speed) * amplitude;
        transform.localPosition = startPos + Vector3.up * y;
    }
}
