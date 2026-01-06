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
    // NEW (Pivot Side / Hinge Side legality):
    // - Your DoorController creates a pivot on the model's LOCAL "left edge".
    // - Therefore we MUST choose a yaw that makes that local-left edge sit against a WALL cell
    //   (otherwise the hinge ends up in the corridor and opening 90/-90 will block the path).
    //
    // We do this by allowing TWO rotations for every spot:
    //   rotA = baseRotation
    //   rotB = baseRotation * 180 yaw flip
    // Then we select the first rotation whose "hinge side" (rot * Vector3.left)
    // points to a WALL cell (perpendicular wall side).
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

            // This cell must be OPEN to place a door "in the passage"
            if (grid[x, y]) // true == wall
                continue;

            bool leftOpen = !grid[x - 1, y];
            bool rightOpen = !grid[x + 1, y];
            bool downOpen = !grid[x, y - 1];
            bool upOpen = !grid[x, y + 1];

            bool isHorizontal = leftOpen && rightOpen && !upOpen && !downOpen; // passage L<->R, walls U/D
            bool isVertical = upOpen && downOpen && !leftOpen && !rightOpen;   // passage D<->U, walls L/R

            if (!isHorizontal && !isVertical)
                continue;

            // base rotation that aligns the door plane with the corridor opening
            Quaternion baseRot = isHorizontal
                ? Quaternion.Euler(0f, 90f, 0f)
                : Quaternion.Euler(0f, 0f, 0f);

            // Determine the two OPEN cells on the passage sides
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

            // Determine the two WALL cells on the perpendicular sides
            // (these MUST be walls by isHorizontal/isVertical definition)
            Vector2Int w1, w2;
            if (isHorizontal)
            {
                w1 = new Vector2Int(x, y + 1); // up wall
                w2 = new Vector2Int(x, y - 1); // down wall
            }
            else
            {
                w1 = new Vector2Int(x - 1, y); // left wall
                w2 = new Vector2Int(x + 1, y); // right wall
            }

            // Two candidate rotations: base + flipped (swap hinge side)
            Quaternion rotA = baseRot;
            Quaternion rotB = baseRot * Quaternion.Euler(0f, 180f, 0f);

            // Pick a rotation whose hinge (local-left) points into one of the perpendicular WALL cells.
            // If neither does, this spot is NOT safe for your pivot logic -> skip.
            if (!TryPickSafeRotation(grid, width, height, new Vector2Int(x, y), w1, w2, rotA, rotB, out Quaternion chosenRot))
                continue;

            spots.Add(new DoorSpot
            {
                cell = new Vector2Int(x, y),
                rotation = chosenRot,
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
        // candidates (now hinge-safe)
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
    // Pivot/Hinge legality helpers (NEW)
    // ==========================================================
    private static bool TryPickSafeRotation(
        bool[,] grid,
        int width,
        int height,
        Vector2Int doorCell,
        Vector2Int perpWall1,
        Vector2Int perpWall2,
        Quaternion rotA,
        Quaternion rotB,
        out Quaternion chosen)
    {
        chosen = default;

        // Prefer rotA if safe, else rotB
        if (IsRotationHingeOnPerpWall(grid, width, height, doorCell, perpWall1, perpWall2, rotA))
        {
            chosen = rotA;
            return true;
        }

        if (IsRotationHingeOnPerpWall(grid, width, height, doorCell, perpWall1, perpWall2, rotB))
        {
            chosen = rotB;
            return true;
        }

        return false;
    }

    // The hinge direction is where the prefab's LOCAL "left edge" points in world.
    // We approximate it using the rotation only:
    //   hingeWorldDir = rot * Vector3.left
    // Then snap it to the nearest cardinal direction on the grid (x/z),
    // and require that the adjacent cell in that direction is one of the perpendicular WALL cells.
    private static bool IsRotationHingeOnPerpWall(
        bool[,] grid,
        int width,
        int height,
        Vector2Int doorCell,
        Vector2Int perpWall1,
        Vector2Int perpWall2,
        Quaternion rot)
    {
        Vector3 hingeDirWorld = rot * Vector3.left; // local-left => hinge side in world
        Vector2Int hingeDirCell = SnapWorldDirToCellDir(hingeDirWorld);

        if (hingeDirCell == Vector2Int.zero)
            return false;

        Vector2Int hingeCell = doorCell + hingeDirCell;

        // hinge must be adjacent and inside bounds
        if (!InBounds(hingeCell, width, height))
            return false;

        // hinge must point to one of the perpendicular wall cells
        // (and that cell must actually be a WALL in the grid)
        bool matchesPerp =
            (hingeCell == perpWall1) ||
            (hingeCell == perpWall2);

        if (!matchesPerp)
            return false;

        // Must be wall (true == wall)
        if (!grid[hingeCell.x, hingeCell.y])
            return false;

        return true;
    }

    private static Vector2Int SnapWorldDirToCellDir(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.000001f) return Vector2Int.zero;
        dir.Normalize();

        // Choose dominant axis between world X and world Z
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.z))
            return (dir.x >= 0f) ? new Vector2Int(1, 0) : new Vector2Int(-1, 0);
        else
            return (dir.z >= 0f) ? new Vector2Int(0, 1) : new Vector2Int(0, -1);
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
    // Greedy Max-Min
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
    // Utility: filter out already-used cells
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
    // BFS PATHFINDING
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
