#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class GridAssignmentGizmos : MonoBehaviour
{
    public static GridAssignmentGizmos Instance;

    private MazeGenerator3D maze;

    private Vector2Int? travellerCell;
    private readonly Dictionary<GameObject, Vector2Int> bombCells = new();
    private GameObject chosenBomb;

    // snapshot so you can still see after bomb despawns
    private float snapshotUntil = -1f;
    private Vector2Int? snapTraveller;
    private readonly List<Vector2Int> snapBombs = new();
    private Vector2Int? snapChosen;

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    public void SetMaze(MazeGenerator3D m) => maze = m;
    public void SetTravellerCell(Vector2Int cell) => travellerCell = cell;

    public void SetBombCell(GameObject bomb, Vector2Int cell)
    {
        if (bomb == null) return;
        bombCells[bomb] = cell;
    }

    public void SetChosenBomb(GameObject bomb) => chosenBomb = bomb;

    public void Clear()
    {
        travellerCell = null;
        bombCells.Clear();
        chosenBomb = null;

        snapTraveller = null;
        snapBombs.Clear();
        snapChosen = null;
        snapshotUntil = -1f;
    }

    public void BeginSnapshot(float seconds)
    {
        snapshotUntil = Time.realtimeSinceStartup + Mathf.Max(0.1f, seconds);

        snapTraveller = travellerCell;
        snapBombs.Clear();
        foreach (var kv in bombCells)
            if (kv.Key != null)
                snapBombs.Add(kv.Value);

        if (chosenBomb != null && bombCells.TryGetValue(chosenBomb, out var c))
            snapChosen = c;
        else
            snapChosen = null;
    }

    private void OnDrawGizmos()
    {
        if (maze == null) return;

        float cs = maze.CellSize;

        bool useSnapshot = snapshotUntil > 0f && Time.realtimeSinceStartup <= snapshotUntil;

        if (useSnapshot)
        {
            if (snapTraveller.HasValue) DrawCell(snapTraveller.Value, cs, Color.cyan);
            foreach (var c in snapBombs) DrawCell(c, cs, Color.yellow);
            if (snapChosen.HasValue) DrawCell(snapChosen.Value, cs, Color.red);
            return;
        }

        if (travellerCell.HasValue) DrawCell(travellerCell.Value, cs, Color.cyan);

        foreach (var kv in bombCells)
        {
            if (kv.Key == null) continue;
            DrawCell(kv.Value, cs, Color.yellow);

            Gizmos.color = Color.gray;
            Gizmos.DrawLine(kv.Key.transform.position, CellCenterWorld(kv.Value, cs));
        }

        if (chosenBomb != null && bombCells.TryGetValue(chosenBomb, out var chosenCell))
        {
            DrawCell(chosenCell, cs, Color.red);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(chosenBomb.transform.position, 0.25f);
        }
    }

    private void DrawCell(Vector2Int c, float cs, Color col)
    {
        Gizmos.color = col;
        Vector3 center = CellCenterWorld(c, cs);
        Vector3 size = new Vector3(cs, 0.05f, cs);
        Gizmos.DrawWireCube(center, size);
    }

    private Vector3 CellCenterWorld(Vector2Int c, float cs)
    {
        return maze.transform.TransformPoint(
            new Vector3((c.x + 0.5f) * cs, 0.05f, (c.y + 0.5f) * cs)
        );
    }
}
#endif
