// Assets/Scripts/Maze/DoorPlacement.cs
using System.Collections.Generic;
using UnityEngine;

public static class DoorPlacement
{
    // ----------------------------------------------------------
    // Build candidate "door spots" from carved wall cells:
    // A door can be placed only on a wall cell that separates two OPEN cells.
    // ----------------------------------------------------------
    public static List<DoorSpot> FromCarvedWalls(
        bool[,] grid,
        List<Vector2Int> carvedWalls,
        int width,
        int height
    )
    {
        List<DoorSpot> spots = new();

        foreach (var c in carvedWalls)
        {
            int x = c.x;
            int y = c.y;

            // EAST-WEST separator: open on left & right
            if (
                Inside(x - 1, y, width, height)
                && Inside(x + 1, y, width, height)
                && !grid[x - 1, y]
                && !grid[x + 1, y]
                && grid[x, y] // must still be a wall cell in the grid
            )
            {
                spots.Add(
                    new DoorSpot
                    {
                        cell = c,
                        rotation = Quaternion.Euler(0, 90, 0),
                        score = Random.value,
                        aOpen = new Vector2Int(x - 1, y),
                        bOpen = new Vector2Int(x + 1, y),
                    }
                );
            }

            // NORTH-SOUTH separator: open on down & up
            if (
                Inside(x, y - 1, width, height)
                && Inside(x, y + 1, width, height)
                && !grid[x, y - 1]
                && !grid[x, y + 1]
                && grid[x, y]
            )
            {
                spots.Add(
                    new DoorSpot
                    {
                        cell = c,
                        rotation = Quaternion.identity,
                        score = Random.value,
                        aOpen = new Vector2Int(x, y - 1),
                        bOpen = new Vector2Int(x, y + 1),
                    }
                );
            }
        }

        return spots;
    }

    public static List<DoorSpot> FilterOnPath(List<DoorSpot> spots, HashSet<Vector2Int> pathSet)
    {
        List<DoorSpot> res = new();
        foreach (var s in spots)
        {
            if (pathSet.Contains(s.aOpen) && pathSet.Contains(s.bOpen))
                res.Add(s);
        }
        return res;
    }

    public static bool IsValidSpot(DoorSpot spot, List<DoorSpot> used, float minDist)
    {
        foreach (var u in used)
            if (Vector2.Distance(spot.cell, u.cell) < minDist)
                return false;

        return true;
    }

    public static List<DoorSpot> PickEvenlySpaced(List<DoorSpot> spots, int count, float minDist)
    {
        List<DoorSpot> picked = new();
        if (count <= 0 || spots == null || spots.Count == 0)
            return picked;

        List<DoorSpot> pool = new(spots);

        // start from a random valid spot
        DoorSpot first = pool[Random.Range(0, pool.Count)];
        picked.Add(first);

        while (picked.Count < count)
        {
            DoorSpot best = null;
            float bestMin = -1f;

            foreach (var s in pool)
            {
                if (!IsValidSpot(s, picked, minDist))
                    continue;

                float dmin = float.PositiveInfinity;
                foreach (var p in picked)
                    dmin = Mathf.Min(dmin, Vector2.Distance(s.cell, p.cell));

                if (dmin > bestMin)
                {
                    bestMin = dmin;
                    best = s;
                }
            }

            if (best == null)
                break;

            picked.Add(best);
        }

        return picked;
    }

    static bool Inside(int x, int y, int width, int height)
    {
        return x > 0 && y > 0 && x < width - 1 && y < height - 1;
    }
}
