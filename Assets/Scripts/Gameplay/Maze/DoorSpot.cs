// Assets/Scripts/Maze/DoorSpot.cs
using UnityEngine;

[System.Serializable]
public sealed class DoorSpot
{
    public Vector2Int cell;      // wall cell where the door sits
    public Quaternion rotation;  // door rotation
    public float score;          // optional sorting score
    public Vector2 offset; // offset in CELL UNITS from the cell center (x,y grid plane)
    public int id;         // stable candidate id (unique per cell+offset)


    // the two open cells separated by this wall (for "on path" checks)
    public Vector2Int aOpen;
    public Vector2Int bOpen;
}
