// Assets/Scripts/Maze/ResourcePlacement.cs
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
        int amount
    )
    {
        if (prefab == null || amount <= 0 || grid == null || pathCells == null)
            return;

        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        List<Vector2Int> valid = new();

        foreach (var cell in pathCells)
        {
            if (!Inside(cell.x, cell.y, w, h))
                continue;

            // skip if blocked
            if (blockedCells != null && blockedCells.Contains(cell))
                continue;

            // skip if wall
            if (grid[cell.x, cell.y])
                continue;

            // skip if too tight
            if (!IsCellSafeFromWalls(grid, cell))
                continue;

            valid.Add(cell);
        }

        if (valid.Count == 0)
            return;

        // Spread them evenly using farthest-point sampling
        List<Vector2Int> picked = PickEvenlySpacedCells(valid, amount);

        foreach (var c in picked)
        {
            Vector3 pos = new Vector3(c.x * cellSize, 0, c.y * cellSize);
            Object.Instantiate(prefab, pos, Quaternion.identity, parent);

            blockedCells?.Add(c);
        }
    }

    static List<Vector2Int> PickEvenlySpacedCells(List<Vector2Int> cells, int count)
    {
        List<Vector2Int> picked = new();
        if (count <= 0 || cells == null || cells.Count == 0)
            return picked;

        Vector2Int first = cells[Random.Range(0, cells.Count)];
        picked.Add(first);

        while (picked.Count < count)
        {
            Vector2Int best = default;
            float bestMin = -1f;
            bool found = false;

            foreach (var c in cells)
            {
                if (picked.Contains(c))
                    continue;

                float dmin = float.PositiveInfinity;
                foreach (var p in picked)
                    dmin = Mathf.Min(dmin, Vector2.Distance(c, p));

                if (dmin > bestMin)
                {
                    bestMin = dmin;
                    best = c;
                    found = true;
                }
            }

            if (!found)
                break;

            picked.Add(best);
        }

        return picked;
    }

    static bool IsCellSafeFromWalls(bool[,] grid, Vector2Int c)
    {
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        if (!Inside(c.x, c.y, w, h))
            return false;

        if (grid[c.x, c.y])
            return false;

        int wallCount = 0;

        if (Inside(c.x + 1, c.y, w, h) && grid[c.x + 1, c.y]) wallCount++;
        if (Inside(c.x - 1, c.y, w, h) && grid[c.x - 1, c.y]) wallCount++;
        if (Inside(c.x, c.y + 1, w, h) && grid[c.x, c.y + 1]) wallCount++;
        if (Inside(c.x, c.y - 1, w, h) && grid[c.x, c.y - 1]) wallCount++;

        // reject only if surrounded by 3-4 walls
        return wallCount < 3;
    }

    static bool Inside(int x, int y, int w, int h)
    {
        return x >= 1 && y >= 1 && x <= w - 2 && y <= h - 2;
    }
}
