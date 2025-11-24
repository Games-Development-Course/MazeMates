using UnityEngine;

public class PadTrigger : MonoBehaviour
{
    private DoorController door;
    private bool playerOnPad = false;

    void Start()
    {
        door = GetComponentInParent<DoorController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerOnPad = true;

        // לא מציגים הודעה אם הדלת פתוחה
        if (door.IsOpen())
            return;

        var hud = HUDManager.Instance;
        var gm = GameManager.Instance;

        switch (door.doorType)
        {
            case DoorType.Normal:
                hud.ShowMessageToTraveller("בקש מהנווט לפתוח לך את הדלת");
                break;

            case DoorType.Puzzle:
                hud.ShowMessageToTraveller("בקש מהנווט להתחיל את החידה");
                break;

            case DoorType.Exit:
                if (gm.AllKeysCollected())
                    hud.ShowMessageToTraveller("יש לך את כל המפתחות! בקש מהנווט לפתוח את הדלת האחרונה ונצחו במשחק!");
                else
                    hud.ShowMessageToTraveller("עליך לאסוף את כל המפתחות כדי לפתוח את דלת היציאה");
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
