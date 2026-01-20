using UnityEngine;

public class PlayerAreaState : MonoBehaviour
{
    public enum AreaState { Maze, NavigatorRoom }

    [Tooltip("איפה השחקן נמצא כרגע (לוגית)")]
    public AreaState currentArea = AreaState.Maze;
}
