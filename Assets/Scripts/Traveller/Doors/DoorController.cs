using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorController : MonoBehaviour
{
    public Transform pivot;             // הציר
    public float openAngle = 90f;       // זווית פתיחה
    public float openSpeed = 3f;        // מהירות הפתיחה
    public PadTrigger pad;              // פד

    private bool isOpening = false;
    private bool opened = false;

    void Update()
    {

        if (pad.IsPlayerOnPad() && Input.GetKeyDown(KeyCode.Space))
        {
            if (!opened)
            {
                isOpening = true;
            }
        }

        if (isOpening)
        {
            Quaternion targetRotation = Quaternion.Euler(0, openAngle, 0);

            pivot.localRotation = Quaternion.Lerp(
                pivot.localRotation,
                targetRotation,
                Time.deltaTime * openSpeed
            );

            if (Quaternion.Angle(pivot.localRotation, targetRotation) < 1f)
            {
                opened = true;
                isOpening = false;
            }
        }
    }
}
