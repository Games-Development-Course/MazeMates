using System.Collections.Generic;
using UnityEngine;

public static class DoorPlacement
{
    // ----------------------------------------------------------
    // יצירת רשימת מיקומים תקפים לדלתות
    // ----------------------------------------------------------
    public static List<DoorSpot> CollectDoorSpots(bool[,] grid, List<Vector2Int> pathCells, int width, int height)
    {
        List<DoorSpot> spots = new List<DoorSpot>();

        foreach (var c in pathCells)
        {
            int x = c.x;
            int y = c.y;

            // קיר בין שני תאים פתוחים ─ כיוון מזרח-מערב
            TryAddDoorSpot(spots, grid, width, height, x, y, x + 1, y, x - 1, y, Quaternion.Euler(0, 90, 0));

            // קיר בין שני תאים פתוחים ─ כיוון צפון-דרום
            TryAddDoorSpot(spots, grid, width, height, x, y, x, y + 1, x, y - 1, Quaternion.Euler(0, 0, 0));
        }

        return spots;
    }
    public static List<DoorSpot> FromCarvedWalls(bool[,] grid, List<Vector2Int> carvedWalls, int width, int height)
    {
        List<DoorSpot> spots = new();

        foreach (var c in carvedWalls)
        {
            int x = c.x;
            int y = c.y;

            // כיוון מזרח-מערב
            if (Inside(x - 1, y, width, height) &&
                Inside(x + 1, y, width, height) &&
                !grid[x - 1, y] && !grid[x + 1, y])
            {
                spots.Add(new DoorSpot
                {
                    cell = c,
                    rotation = Quaternion.Euler(0, 90, 0),
                    score = Random.value
                });
            }

            // כיוון צפון-דרום
            if (Inside(x, y - 1, width, height) &&
                Inside(x, y + 1, width, height) &&
                !grid[x, y - 1] && !grid[x, y + 1])
            {
                spots.Add(new DoorSpot
                {
                    cell = c,
                    rotation = Quaternion.identity,
                    score = Random.value
                });
            }
        }

        return spots;
    }

    static bool Inside(int x, int y, int width, int height)
    {
        return x > 0 && y > 0 && x < width - 1 && y < height - 1;
    }



    private static void TryAddDoorSpot(List<DoorSpot> spots,
                                       bool[,] grid,
                                       int width,
                                       int height,
                                       int wallX, int wallY,
                                       int aX, int aY,
                                       int bX, int bY,
                                       Quaternion rot)
    {
        if (!Inside(wallX, wallY, width, height)) return;
        if (!Inside(aX, aY, width, height)) return;
        if (!Inside(bX, bY, width, height)) return;

        // תנאי דלת: קיר בין שני תאים פתוחים
        if (grid[wallX, wallY] &&
            !grid[aX, aY] &&
            !grid[bX, bY])
        {
            DoorSpot spot = new DoorSpot
            {
                cell = new Vector2Int(wallX, wallY),
                rotation = rot,
                score = Vector2.Distance(new Vector2(aX, aY), new Vector2(width / 2, height / 2))
            };

            spots.Add(spot);
        }
    }

 

    // ----------------------------------------------------------
    // בחירת מיקומים
    // ----------------------------------------------------------

    public static DoorSpot FindClosestSpot(List<DoorSpot> spots, Vector2Int target, List<DoorSpot> used, float minDist)
    {
        DoorSpot best = null;
        float bestD = Mathf.Infinity;

        foreach (var s in spots)
        {
            if (!IsValidSpot(s, used, minDist)) continue;

            float d = Vector2.Distance(s.cell, target);
            if (d < bestD)
            {
                bestD = d;
                best = s;
            }
        }
        return best;
    }

    public static DoorSpot PickRandomSpot(List<DoorSpot> spots, List<DoorSpot> used, float minDist)
    {
        var list = spots.FindAll(s => IsValidSpot(s, used, minDist));
        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    public static DoorSpot PickDeepSpot(List<DoorSpot> spots, List<DoorSpot> used, float minDist)
    {
        foreach (var s in spots)
            if (IsValidSpot(s, used, minDist))
                return s;
        return null;
    }

    public static bool IsValidSpot(DoorSpot spot, List<DoorSpot> used, float minDist)
    {
        foreach (var u in used)
            if (Vector2.Distance(spot.cell, u.cell) < minDist)
                return false;

        return true;
    }
}
