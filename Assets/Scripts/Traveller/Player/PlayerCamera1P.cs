using UnityEngine;

public class PlayerCamera1P : MonoBehaviour
{
    public float mouseSensitivity = 200f;
    public Transform playerBody;

    float xRotation = 0f;

    public float lockDuration = 0.5f; // כמה זמן שהמצלמה נעולה
    private bool cameraLocked = true;
    private float timer = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }
    public void LockCameraForSeconds(float duration)
    {
        lockDuration = duration;
        cameraLocked = true;
        timer = 0f;
    }


    void Update()
    {
        // שלב 1 — המצלמה נעולה בהתחלה
        if (cameraLocked)
        {
            timer += Time.deltaTime;

            // קיבוע מלא של הרוטציה
            xRotation = 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            playerBody.rotation = Quaternion.Euler(0f, 0f, 0f);

            if (timer >= lockDuration)
                cameraLocked = false;

            return;
        }

        //  שלב 2 — אחרי שהנעילה נגמרה, המצלמה עובדת רגיל
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
