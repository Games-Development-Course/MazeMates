using UnityEngine;

public class BombFidget : MonoBehaviour
{
    public float wobbleSpeed = 3f;
    public float wobbleAmount = 10f;

    public float bounceSpeed = 2f;
    public float bounceAmount = 0.1f;

    public float heightOffset = 0.3f; // <<< NEW

    Vector3 initialRot;
    Vector3 initialPos;

    void Start()
    {
        initialRot = transform.localEulerAngles;
        initialPos = transform.localPosition + new Vector3(0, heightOffset, 0);
    }

    void Update()
    {
        float wobble = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAmount;
        float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceAmount;

        transform.localEulerAngles = initialRot + new Vector3(0, wobble, 0);
        transform.localPosition = initialPos + new Vector3(0, bounce, 0);
    }
}
