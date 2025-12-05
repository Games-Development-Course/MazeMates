using UnityEngine;

[CreateAssetMenu(fileName = "DoorTypeSO", menuName = "Scriptable Objects/DoorTypeSO")]
public class DoorTypeSO : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;

    public bool hasPuzzle;
    public PuzzleSO puzzle;

    public enum DoorState { Locked, Unlocked, Open }
    public DoorState defaultState;
}
