using UnityEngine;

[CreateAssetMenu(fileName = "PuzzleSO", menuName = "Scriptable Objects/PuzzleSO")]
public class PuzzleSO : ScriptableObject
{
    public string puzzleId;
    public string puzzleName;
    [TextArea] public string description;

    public Sprite preview;

    // Example: a list of steps or required symbols
    public List<string> steps;
}
