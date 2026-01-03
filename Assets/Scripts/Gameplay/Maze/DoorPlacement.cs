// Assets/Scripts/Maze/DoorPlacement.cs
using System.Collections.Generic;
using UnityEngine;

public static class DoorPlacement
{
    // ----------------------------------------------------------
    // DoorSpot candidates:
    // Door is allowed only if:
    //  1) This cell is OPEN (your generator carves it to false)
    //  2) There are OPEN cells on the two sides of the passage
    //  3) There are WALLS on the perpendicular sides (so it sits "between 2 walls")
    //
    // Rotation:
    // - If passage is Left<->Right => door blocks across X => use identity (Z-facing door blocks X corridor)
    // - If passage is Down<->Up    => door blocks across Y => use 90° yaw
    // ----------------------------------------------------------
    public static List<DoorSpot> FromCarvedWalls(
        bool[,] grid,
        List<Vector2Int> carvedWalls,
        int width,
        int height
    )
    {
        List<DoorSpot> spots = new();
        if (grid == null || carvedWalls == null) return spots;

        foreach (var c in carvedWalls)
        {
            int x = c.x;
            int y = c.y;

            if (!Inside(x, y, width, height))
                continue;

            // carved cell must be OPEN in your generator
            if (grid[x, y])
                continue;

            bool leftOpen = Inside(x - 1, y, width, height) && !grid[x - 1, y];
            bool rightOpen = Inside(x + 1, y, width, height) && !grid[x + 1, y];
            bool downOpen = Inside(x, y - 1, width, height) && !grid[x, y - 1];
            bool upOpen = Inside(x, y + 1, width, height) && !grid[x, y + 1];

            // Perpendicular must be WALLS ("between 2 walls")
            bool upWall = Inside(x, y + 1, width, height) && grid[x, y + 1];
            bool downWall = Inside(x, y - 1, width, height) && grid[x, y - 1];
            bool leftWall = Inside(x - 1, y, width, height) && grid[x - 1, y];
            bool rightWall = Inside(x + 1, y, width, height) && grid[x + 1, y];

            // Passage is LEFT<->RIGHT, walls are UP & DOWN
            if (leftOpen && rightOpen && upWall && downWall)
            {
                spots.Add(new DoorSpot
                {
                    cell = c,
                    rotation = Quaternion.identity,
                    score = Random.value,
                    aOpen = new Vector2Int(x - 1, y),
                    bOpen = new Vector2Int(x + 1, y),
                });
            }

            // Passage is DOWN<->UP, walls are LEFT & RIGHT
            if (downOpen && upOpen && leftWall && rightWall)
            {
                spots.Add(new DoorSpot
                {
                    cell = c,
                    rotation = Quaternion.Euler(0, 90, 0),
                    score = Random.value,
                    aOpen = new Vector2Int(x, y - 1),
                    bOpen = new Vector2Int(x, y + 1),
                });
            }
        }

        return spots;
    }

    public static List<DoorSpot> FilterOnPath(List<DoorSpot> spots, HashSet<Vector2Int> pathSet)
    {
        List<DoorSpot> res = new();
        if (spots == null || pathSet == null) return res;

        foreach (var s in spots)
            if (pathSet.Contains(s.aOpen) && pathSet.Contains(s.bOpen))
                res.Add(s);

        return res;
    }

    // blockedCells contains occupied/reserved OPEN cells from ResourcePlacement.
    public static List<DoorSpot> FilterBlockedByResources(List<DoorSpot> spots, HashSet<Vector2Int> blockedCells)
    {
        if (spots == null) return new List<DoorSpot>();
        if (blockedCells == null || blockedCells.Count == 0) return new List<DoorSpot>(spots);

        List<DoorSpot> res = new();
        foreach (var s in spots)
        {
            if (blockedCells.Contains(s.aOpen)) continue;
            if (blockedCells.Contains(s.bOpen)) continue;
            res.Add(s);
        }
        return res;
    }

    // ==========================================================
    // KEEP OLD API (MazeGenerator3D expects these methods)
    // We keep them, but improve the selection internally.
    // ==========================================================

    public static bool IsValidSpot(DoorSpot spot, List<DoorSpot> used, float minDist)
    {
        if (used == null || used.Count == 0) return true;
        if (minDist <= 0f) return true;

        foreach (var u in used)
            if (Vector2.Distance(spot.cell, u.cell) < minDist)
                return false;

        return true;
    }

    // MazeGenerator3D calls PickEvenlySpaced; keep it but make it behave like ResourcePlacement:
    // shuffle + greedy + relax minDist to reach count.
    public static List<DoorSpot> PickEvenlySpaced(List<DoorSpot> spots, int count, float minDist)
    {
        List<DoorSpot> picked = new();
        if (count <= 0 || spots == null || spots.Count == 0) return picked;

        List<DoorSpot> pool = new(spots);
        Shuffle(pool);

        float curMin = Mathf.Max(0f, minDist);

        // Try multiple passes, relaxing distance to actually reach count.
        for (int pass = 0; pass < 6 && picked.Count < count; pass++)
        {
            foreach (var s in pool)
            {
                if (picked.Count >= count) break;
                if (IsValidSpot(s, picked, curMin))
                    picked.Add(s);
            }
            curMin *= 0.6f;
        }

        // If still not enough, just fill remaining (better than placing too few)
        if (picked.Count < count)
        {
            foreach (var s in pool)
            {
                if (picked.Count >= count) break;
                if (!picked.Contains(s))
                    picked.Add(s);
            }
        }

        if (picked.Count > count)
            picked.RemoveRange(count, picked.Count - count);

        return picked;
    }

    // ==========================================================

    static void Shuffle(List<DoorSpot> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    static bool Inside(int x, int y, int width, int height)
    {
        return x > 0 && y > 0 && x < width - 1 && y < height - 1;
    }
}
