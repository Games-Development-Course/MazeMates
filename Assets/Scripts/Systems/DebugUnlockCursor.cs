using UnityEngine;

public class DebugUnlockCursor : MonoBehaviour
{
    public KeyCode unlockKey = KeyCode.F1;

    void Update()
    {
        if (Input.GetKeyDown(unlockKey))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("Cursor UNLOCKED for debugging.");
        }
    }
}
