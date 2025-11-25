using UnityEngine;

public class PlayerCamera1P : MonoBehaviour
{
    public float mouseSensitivity = 200f;
    public Transform playerBody;

    float xRotation = 0f;

    public float lockDuration = 0.5f;
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
        // מניעת NRE אם GameManager עדיין לא קיים
        if (GameManager.Instance == null)
            return;

        // אם זה מסך Navigator בלי playerBody → לא לזוז
        if (playerBody == null)
            return;

        // בזמן חידה המצלמה לא זזה
        if (GameManager.Instance.inPuzzle)
            return;

        // שלב נעילה הראשוני
        if (cameraLocked)
        {
            timer += Time.deltaTime;

            xRotation = 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            playerBody.rotation = Quaternion.Euler(0f, 0f, 0f);

            if (timer >= lockDuration)
                cameraLocked = false;

            return;
        }

        // מצב מצלמה רגיל
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
