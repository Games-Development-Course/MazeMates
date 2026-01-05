// Assets/Scripts/Maze/DoorPlacement.cs
using System.Collections.Generic;
using UnityEngine;

public static class DoorPlacement
{
    // ----------------------------------------------------------
    // DoorSpot candidates:
    // Door is allowed only if:
    //  1) This cell is OPEN (grid[x,y] == false)
    //  2) There are OPEN cells on the two sides of the passage
    //  3) There are WALLS on the perpendicular sides (so it sits "between 2 walls")
    //
    // Rotation:
    // - If passage is Left<->Right => rotate 90 yaw
    // - If passage is Down<->Up    => identity
    // ----------------------------------------------------------
    public static List<DoorSpot> FromCarvedWalls(
       bool[,] grid,
       List<Vector2Int> carvedWalls,
       int width,
       int height)
    {
        List<DoorSpot> spots = new();

        foreach (var cell in carvedWalls)
        {
            int x = cell.x;
            int y = cell.y;

            if (x <= 0 || y <= 0 || x >= width - 1 || y >= height - 1)
                continue;

            bool leftOpen = !grid[x - 1, y];
            bool rightOpen = !grid[x + 1, y];
            bool downOpen = !grid[x, y - 1];
            bool upOpen = !grid[x, y + 1];

            bool isHorizontal = leftOpen && rightOpen && !upOpen && !downOpen;
            bool isVertical = upOpen && downOpen && !leftOpen && !rightOpen;

            if (!isHorizontal && !isVertical)
                continue;

            Quaternion rot = isHorizontal
                ? Quaternion.Euler(0f, 90f, 0f)
                : Quaternion.Euler(0f, 0f, 0f);

            Vector2Int aOpen, bOpen;

            if (isHorizontal)
            {
                aOpen = new Vector2Int(x - 1, y);
                bOpen = new Vector2Int(x + 1, y);
            }
            else
            {
                aOpen = new Vector2Int(x, y - 1);
                bOpen = new Vector2Int(x, y + 1);
            }

            spots.Add(new DoorSpot
            {
                cell = new Vector2Int(x, y),
                rotation = rot,
                aOpen = aOpen,
                bOpen = bOpen
            });
        }

        return spots;
    }

    // ==========================================================
    // MAIN API: Plan door placement (ALL selection logic lives here)
    // ==========================================================
    public readonly struct DoorPlan
    {
        public readonly List<DoorSpot> Puzzle;
        public readonly List<DoorSpot> Normal;
        public readonly int WantPuzzle;
        public readonly int WantNormal;
        public readonly int PlacedPuzzle;
        public readonly int PlacedNormal;

        public DoorPlan(
            List<DoorSpot> puzzle,
            List<DoorSpot> normal,
            int wantPuzzle,
            int wantNormal,
            int placedPuzzle,
            int placedNormal)
        {
            Puzzle = puzzle;
            Normal = normal;
            WantPuzzle = wantPuzzle;
            WantNormal = wantNormal;
            PlacedPuzzle = placedPuzzle;
            PlacedNormal = placedNormal;
        }
    }

    /// <summary>
    /// Plans puzzle + normal door spots.
    /// - Enforces "uniform-ish" distribution via greedy max-min.
    /// - Relaxes constraints gradually until all doors are planned.
    /// - Keeps start clear: prevents doors from being placed on StartCell and N steps forward from start corridor.
    /// - Keeps forced exit clear.
    /// </summary>
    public static DoorPlan PlanDoors(
        bool[,] grid,
        List<Vector2Int> carvedWalls,
        int width,
        int height,
        Vector2Int startCell,
        Vector2Int forcedExitCell,
        int wantNormal,
        int wantPuzzle,
        int keepClearStepsForward,
        Vector2Int startForwardDir,
        float[] minDistStepsCells,
        int nearPathManhattanRadius = 1)
    {
        // candidates
        List<DoorSpot> spots = FromCarvedWalls(grid, carvedWalls, width, height);

        // Always block these:
        HashSet<Vector2Int> blockedCells = new();
        blockedCells.Add(forcedExitCell);
        blockedCells.Add(startCell);

        // keep clear "N forward"
        AddForwardClearCells(blockedCells, grid, width, height, startCell, startForwardDir, keepClearStepsForward);

        // remove blocked from base candidates
        spots = FilterOutCellSet(spots, blockedCells);

        // Build path for puzzle prioritization (strict/near/any)
        List<Vector2Int> solutionPath = FindPathBFS(grid, width, height, startCell, forcedExitCell);
        HashSet<Vector2Int> pathSet = new(solutionPath);

        List<DoorSpot> puzzleStrict = FilterOnPath(spots, pathSet); // both sides on path
        List<DoorSpot> puzzleNear = BuildNearPathCandidates(spots, pathSet, nearPathManhattanRadius);
        List<DoorSpot> puzzleAny = spots;

        // used spots for distance scoring (CELL distance)
        List<DoorSpot> used = new();

        // (reserve forced exit, so doors won't cluster near it)
        used.Add(new DoorSpot { cell = forcedExitCell, rotation = Quaternion.identity, aOpen = forcedExitCell, bOpen = forcedExitCell });

        // plan outputs
        List<DoorSpot> plannedPuzzle = new();
        List<DoorSpot> plannedNormal = new();

        HashSet<Vector2Int> usedPuzzleCells = new();
        HashSet<Vector2Int> usedNormalCells = new();

        int placedPuzzle = 0;
        int placedNormal = 0;

        // scope widening for puzzle doors
        int puzzleScope = 0; // 0=strict, 1=near, 2=any

        // ensure minDist steps exist
        if (minDistStepsCells == null || minDistStepsCells.Length == 0)
            minDistStepsCells = new float[] { 4f, 3f, 2f, 1f, 0.5f, 0f };

        // Relax loop
        for (int step = 0; step < minDistStepsCells.Length; step++)
        {
            float minDist = minDistStepsCells[step];

            // -------- PUZZLE --------
            while (placedPuzzle < wantPuzzle)
            {
                List<DoorSpot> baseCandidates =
                    (puzzleScope == 0) ? puzzleStrict :
                    (puzzleScope == 1) ? puzzleNear :
                    puzzleAny;

                // remove already-used cells (planned puzzle/normal)
                List<DoorSpot> candidates = FilterOutCells(baseCandidates, usedPuzzleCells, usedNormalCells);

                // also remove the blocked cells set again (safe)
                candidates = FilterOutCellSet(candidates, blockedCells);

                if (!TrySelectSpotMaxMin(candidates, used, minDist, out DoorSpot chosen))
                    break;

                plannedPuzzle.Add(chosen);
                used.Add(chosen);
                usedPuzzleCells.Add(chosen.cell);
                placedPuzzle++;
            }

            // If puzzle missing: widen scope first (minimal relaxation), retry same distance
            if (placedPuzzle < wantPuzzle && puzzleScope < 2)
            {
                puzzleScope++;
                step--;
                continue;
            }

            // -------- NORMAL --------
            while (placedNormal < wantNormal)
            {
                List<DoorSpot> candidates = FilterOutCells(spots, usedPuzzleCells, usedNormalCells);
                candidates = FilterOutCellSet(candidates, blockedCells);

                if (!TrySelectSpotMaxMin(candidates, used, minDist, out DoorSpot chosen))
                    break;

                plannedNormal.Add(chosen);
                used.Add(chosen);
                usedNormalCells.Add(chosen.cell);
                placedNormal++;
            }

            if (placedPuzzle >= wantPuzzle && placedNormal >= wantNormal)
                break;
        }

        return new DoorPlan(plannedPuzzle, plannedNormal, wantPuzzle, wantNormal, placedPuzzle, placedNormal);
    }

    // ==========================================================
    // Path filtering
    // ==========================================================
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
    // Placement validity (CELL distance)
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

    // ==========================================================
    // Uniform-ish placement helper (CELL space)
    // Greedy Max-Min: pick the candidate that maximizes its
    // minimum distance to all used spots, while respecting minDist.
    // ==========================================================
    public static bool TrySelectSpotMaxMin(
        List<DoorSpot> candidates,
        List<DoorSpot> used,
        float minDist,
        out DoorSpot selected)
    {
        selected = default;

        if (candidates == null || candidates.Count == 0)
            return false;

        int bestIndex = -1;
        float bestScore = -1f;

        for (int i = 0; i < candidates.Count; i++)
        {
            var s = candidates[i];
            if (!IsValidSpot(s, used, minDist))
                continue;

            // score = distance to nearest used spot (maximize it)
            float nearest = float.PositiveInfinity;

            if (used != null && used.Count > 0)
            {
                for (int k = 0; k < used.Count; k++)
                {
                    float d = Vector2.Distance(s.cell, used[k].cell);
                    if (d < nearest) nearest = d;
                }
            }
            else
            {
                nearest = 999999f;
            }

            if (nearest > bestScore)
            {
                bestScore = nearest;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
            return false;

        selected = candidates[bestIndex];
        return true;
    }

    // ==========================================================
    // Candidate expansion: Near a path (Manhattan radius)
    // ==========================================================
    public static List<DoorSpot> BuildNearPathCandidates(
        List<DoorSpot> all,
        HashSet<Vector2Int> path,
        int manhattanRadius = 1)
    {
        if (all == null) return new List<DoorSpot>();
        if (path == null || path.Count == 0) return new List<DoorSpot>();

        HashSet<Vector2Int> near = new();

        foreach (var p in path)
        {
            near.Add(p);

            for (int r = 1; r <= manhattanRadius; r++)
            {
                near.Add(p + new Vector2Int(r, 0));
                near.Add(p + new Vector2Int(-r, 0));
                near.Add(p + new Vector2Int(0, r));
                near.Add(p + new Vector2Int(0, -r));
            }
        }

        List<DoorSpot> res = new();
        foreach (var s in all)
            if (near.Contains(s.cell))
                res.Add(s);

        return res;
    }

    // ==========================================================
    // Utility: filter out already-used cells (compat)
    // ==========================================================
    public static List<DoorSpot> FilterOutCells(
        List<DoorSpot> all,
        HashSet<Vector2Int> a,
        HashSet<Vector2Int> b)
    {
        if (all == null) return new List<DoorSpot>();

        List<DoorSpot> res = new();
        for (int i = 0; i < all.Count; i++)
        {
            var s = all[i];
            if (a != null && a.Contains(s.cell)) continue;
            if (b != null && b.Contains(s.cell)) continue;
            res.Add(s);
        }
        return res;
    }

    // ==========================================================
    // KEEP OLD API (compat)
    // ==========================================================
    public static List<DoorSpot> PickEvenlySpaced(List<DoorSpot> spots, int count, float minDist)
    {
        List<DoorSpot> picked = new();
        if (count <= 0 || spots == null || spots.Count == 0) return picked;

        List<DoorSpot> pool = new(spots);
        Shuffle(pool);

        float curMin = Mathf.Max(0f, minDist);

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
    // INTERNAL HELPERS
    // ==========================================================
    private static void Shuffle(List<DoorSpot> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static List<DoorSpot> FilterOutCellSet(List<DoorSpot> all, HashSet<Vector2Int> blocked)
    {
        if (all == null) return new List<DoorSpot>();
        if (blocked == null || blocked.Count == 0) return new List<DoorSpot>(all);

        List<DoorSpot> res = new();
        for (int i = 0; i < all.Count; i++)
        {
            var s = all[i];
            if (blocked.Contains(s.cell)) continue;
            res.Add(s);
        }
        return res;
    }

    private static void AddForwardClearCells(
        HashSet<Vector2Int> blocked,
        bool[,] grid,
        int width,
        int height,
        Vector2Int start,
        Vector2Int forwardDir,
        int steps)
    {
        if (blocked == null) return;
        if (steps <= 0) return;
        if (forwardDir == Vector2Int.zero) return;

        // We add the NEXT N open cells along the forward direction.
        // If a cell is out-of-bounds or a wall, we stop early.
        Vector2Int cur = start;
        for (int i = 1; i <= steps; i++)
        {
            Vector2Int nxt = cur + forwardDir;

            if (!InBounds(nxt, width, height)) break;
            if (grid[nxt.x, nxt.y]) break; // wall
            blocked.Add(nxt);
            cur = nxt;
        }
    }

    private static bool InBounds(Vector2Int c, int w, int h) =>
        c.x >= 0 && c.y >= 0 && c.x < w && c.y < h;

    // ==========================================================
    // BFS PATHFINDING (moved from MazeGenerator3D)
    // ==========================================================
    public static List<Vector2Int> FindPathBFS(bool[,] grid, int width, int height, Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> empty = new();
        if (!InBounds(start, width, height) || !InBounds(goal, width, height)) return empty;
        if (grid[start.x, start.y] || grid[goal.x, goal.y]) return empty;

        Queue<Vector2Int> q = new();
        Dictionary<Vector2Int, Vector2Int> parent = new();
        HashSet<Vector2Int> visited = new();

        q.Enqueue(start);
        visited.Add(start);

        Vector2Int[] dirs = new[]
        {
            new Vector2Int(1,0),
            new Vector2Int(-1,0),
            new Vector2Int(0,1),
            new Vector2Int(0,-1),
        };

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == goal) break;

            foreach (var d in dirs)
            {
                var nxt = cur + d;
                if (!InBounds(nxt, width, height)) continue;
                if (visited.Contains(nxt)) continue;
                if (grid[nxt.x, nxt.y]) continue;

                visited.Add(nxt);
                parent[nxt] = cur;
                q.Enqueue(nxt);
            }
        }

        if (!visited.Contains(goal))
            return empty;

        List<Vector2Int> path = new();
        Vector2Int t = goal;
        path.Add(t);

        while (t != start)
        {
            t = parent[t];
            path.Add(t);
        }

        path.Reverse();
        return path;
    }
}
