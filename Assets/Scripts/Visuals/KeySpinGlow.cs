using UnityEngine;

public class KeyFloatSpin : MonoBehaviour
{
    [Header("Floating")]
    public float floatSpeed = 2f;
    public float floatAmount = 0.15f;

    [Header("Spinning")]
    public float spinSpeed = 90f;

    private Vector3 startPos;

    void Start()
    {
        // שמירה על מיקום התחלה והרמה קטנה מהרצפה
        startPos = transform.localPosition + new Vector3(0, 0.25f, 0);

        // להעמיד את המודל ישר אם צריך
        transform.localRotation = Quaternion.Euler(0, 0, 0);
    }

    void Update()
    {
        // ריחוף
        float offset = Mathf.Sin(Time.time * floatSpeed) * floatAmount;
        transform.localPosition = startPos + new Vector3(0, offset, 0);

        // סיבוב סביב ציר Y
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }
}
