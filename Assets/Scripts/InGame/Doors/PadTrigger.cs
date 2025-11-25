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
                    hud.ShowMessageToTraveller("יש לך את כל המפתחות! בקש מהנווט לפתוח!");
                else
                    hud.ShowMessageToTraveller("עליך לאסוף את כל המפתחות!");
                break;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
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
}
