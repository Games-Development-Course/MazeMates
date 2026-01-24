using UnityEngine;
using UnityEngine.UI;

public class PuzzleCloseButton : MonoBehaviour
{
    public DoorController door; // set at runtime

    // Called by Button.onClick
    public void Close()
    {
        // closes puzzle UI + clears TV preview + unlocks movement (via PuzzleDoor)
        door?.GetPuzzle()?.ForceClosePuzzle();
    }
}
