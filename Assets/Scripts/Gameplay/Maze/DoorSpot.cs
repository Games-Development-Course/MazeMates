// Assets/Scripts/Maze/DoorSpot.cs
using UnityEngine;

[System.Serializable]
public sealed class DoorSpot
{
    public Vector2Int cell;      // wall cell where the door sits
    public Quaternion rotation;  // door rotation
    public float score;          // optional sorting score

    // the two open cells separated by this wall (for "on path" checks)
    public Vector2Int aOpen;
    public Vector2Int bOpen;
}
