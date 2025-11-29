using UnityEngine;

public class PadTrigger : MonoBehaviour
{
    private DoorController controller;
    private bool playerOnPad = false;

    private void Awake()
    {
        controller = GetComponentInParent<DoorController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerOnPad = true;

        // ודא שהדלת קיימת
        if (controller == null)
            controller = GetComponentInParent<DoorController>();

        // אם הדלת כבר פתוחה – אל תציג שום הודעה
        if (controller != null && controller.IsOpen())
            return;

        var hud = HUDManager.Instance;
        var gm = GameManager.Instance;

        switch (controller.doorType)
        {
            case DoorType.Normal:
                hud.ShowMessageForTraveller("לחץ רווח לפתוח את הדלת");
                break;

            case DoorType.Puzzle:
                hud.ShowMessageForTraveller("לחץ רווח להתחיל את החידה");
                break;

            case DoorType.Exit:
                if (gm.AllKeysCollected())
                    hud.ShowMessageForTraveller("יש לך את כל המפתחות! הקש רווח לניצחון!");
                else
                    hud.ShowMessageForTraveller("עליך לאסוף את כל המפתחות");
                break;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerOnPad = false;

        // אם הדלת סגורה → סגור חידה אם יש
        if (!controller.IsOpen())
        {
            var puzzle = controller.GetPuzzle();
            if (puzzle != null)
                puzzle.ForceClosePuzzle();
        }
    }

    public bool IsPlayerOnPad() => playerOnPad;

    public bool CanActivateDoorWithSpace()
    {
        if (DoorPadToggle.Instance == null)
            return true;

        return DoorPadToggle.Instance.allowSpaceActivation;
    }
}
