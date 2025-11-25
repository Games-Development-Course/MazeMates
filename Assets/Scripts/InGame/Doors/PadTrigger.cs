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
        if (!other.CompareTag("Player"))
            return;
        playerOnPad = true;

        if (door.IsOpen())
            return;

        var hud = HUDManager.Instance;
        var gm = GameManager.Instance;

        switch (door.doorType)
        {
            case DoorType.Normal:
                hud.ShowMessageToTraveller("לחץ רווח לפתוח את הדלת ");
                break;

            case DoorType.Puzzle:
                hud.ShowMessageToTraveller("לחץ רווח להתחיל את החידה");
                break;

            case DoorType.Exit:
                if (gm.AllKeysCollected())
                    hud.ShowMessageToTraveller(
                        "יש לך את כל המפתחות! הקש רווח לפתוח את הדלת ולנצח!!"
                    );
                else
                    hud.ShowMessageToTraveller("עליך לאסוף את כל המפתחות!");
                break;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        playerOnPad = false;

        if (!door.IsOpen())
        {
            var puzzle = door.GetPuzzle();
            if (puzzle != null)
            {
                puzzle.ForceClosePuzzle();
            }
        }
    }

    public bool IsPlayerOnPad() => playerOnPad;

    // 🔥 פונקציה חדשה: האם מותר לשחקן ללחוץ רווח ולהפעיל דלת?
    public bool CanActivateDoorWithSpace()
    {
        // אם אין מנהל – נניח שמותר
        if (DoorPadToggle.Instance == null)
            return true;

        return DoorPadToggle.Instance.allowSpaceActivation;
    }
}
