using UnityEngine;
using System.Collections;

public class ExitDoorController : MonoBehaviour
{
    public PadTrigger pad;
    public Transform pivot;
    public float openAngle = -90f;
    public float openSpeed = 3f;

    private bool opening = false;
    private bool opened = false;

    void Update()
    {
        if (opened || opening) return;

        // חייב להיות על הפד
        if (pad.IsPlayerOnPad())
        {
            // חייבים לאסוף את כל המפתחות
            if (GameManager.Instance.AllKeysCollected())
            {
                // חייבים ללחוץ רווח
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    StartCoroutine(OpenDoor());
                }
            }
        }
    }

    IEnumerator OpenDoor()
    {
        opening = true;

        Quaternion target = Quaternion.Euler(0, openAngle, 0);

        while (Quaternion.Angle(pivot.localRotation, target) > 0.1f)
        {
            pivot.localRotation = Quaternion.Lerp(
                pivot.localRotation,
                target,
                Time.deltaTime * openSpeed
            );

            yield return null;
        }

        pivot.localRotation = target;
        opened = true;
        opening = false;
    }
}
