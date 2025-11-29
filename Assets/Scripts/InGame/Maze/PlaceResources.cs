using System.Collections.Generic;
using UnityEngine;

public static class ResourcePlacement
{
    public static void PlaceResources(
        bool[,] grid,
        List<Vector2Int> pathCells,
        HashSet<Vector2Int> blockedCells,
        float cellSize,
        Transform parent,
        GameObject prefab,
        int amount)
    {
        if (prefab == null || amount <= 0)
            return;

        List<Vector2Int> valid = new();

        foreach (var cell in pathCells)
        {
            // skip if blocked
            if (blockedCells.Contains(cell))
                continue;

            // skip if this cell is actually a wall (defensive check)
            if (grid[cell.x, cell.y])
                continue;

            // skip if neighbors are walls touching the pivot of the object
            if (!IsCellSafeFromWalls(grid, cell))
                continue;

            valid.Add(cell);
        }

        if (valid.Count == 0)
            return;

        Shuffle(valid);

        int step = Mathf.Max(1, valid.Count / (amount + 1));
        int idx = step;

        for (int i = 0; i < amount; i++)
        {
            if (idx >= valid.Count)
                break;

            Vector2Int c = valid[idx];
            idx += step;

            Vector3 pos = new Vector3(c.x * cellSize, 0, c.y * cellSize);
            Object.Instantiate(prefab, pos, Quaternion.identity, parent);

            blockedCells.Add(c);
        }
    }

private static bool IsCellSafeFromWalls(bool[,] grid, Vector2Int c)
{
    // reject if this IS a wall
    if (grid[c.x, c.y])
        return false;

    // Allow neighbors, but avoid extremely tight corners
    int wallCount = 0;

    if (grid[c.x + 1, c.y]) wallCount++;
    if (grid[c.x - 1, c.y]) wallCount++;
    if (grid[c.x, c.y + 1]) wallCount++;
    if (grid[c.x, c.y - 1]) wallCount++;

    // Reject ONLY if cell is surrounded by 3–4 walls (dead pits)
    return wallCount < 3;
}


    private static void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }
}
