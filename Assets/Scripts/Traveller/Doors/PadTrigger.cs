using UnityEngine;

public class PadTrigger : MonoBehaviour
{
    private DoorController door;
    private bool playerOnPad = false;

    void Start()
    {
        // הדלת יושבת תמיד בהורה
        door = GetComponentInParent<DoorController>();

        if (door == null)
            Debug.LogError("PadTrigger נמצא בלי DoorController בהורה!", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerOnPad = true;

        if (door.IsOpen()) return;

        // הצגת הודעה לפי סוג הדלת
        switch (door.doorType)
        {
            case DoorType.Normal:
                HUD.Instance.ShowMessage("לחץ על רווח לפתיחת הדלת");
                break;

            case DoorType.Puzzle:
                HUD.Instance.ShowMessage("לחץ על רווח להתחלת החידה");
                break;

            case DoorType.Exit:
                if (GameManager.Instance.AllKeysCollected())
                    HUD.Instance.ShowMessage("יש לך את כל המפתחות! לחץ רווח לפתיחת היציאה");
                else
                    HUD.Instance.ShowMessage("עליך לאסוף את כל המפתחות כדי לפתוח את דלת היציאה");
                break;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerOnPad = false;
    }

    void Update()
    {
        if (!playerOnPad) return;
        if (door.IsOpen()) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            door.Interact();
        }
    }
}
