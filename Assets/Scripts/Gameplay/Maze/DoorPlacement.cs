// Assets/Scripts/Maze/DoorPlacement.cs
using System.Collections.Generic;
using UnityEngine;

public static class DoorPlacement
{
    // Toggle logs (console only, no gizmos)
    public static bool EnableLogs = true;
    private static void DLog(string msg)
    {
        if (!EnableLogs) return;
        Debug.Log(msg);
    }

    // ----------------------------------------------------------
    // DoorSpot candidates (SUB-CELL):
    // We DO NOT rely on carvedWalls anymore (it was too restrictive).
    // Instead, we scan the whole grid and pick STRAIGHT corridor cells:
    //   - Horizontal corridor cell: open + left/right open + up/down walls
    //   - Vertical corridor cell:   open + up/down open + left/right walls
    //
    // Sub-cell candidates:
    // - For each eligible corridor cell, generate multiple candidate positions along the corridor axis.
    // - Step is doorWidthCells (in "cell units").
    //
    // Corridor end legality (your rule: remove positions with distance < 1 cell from corridor end):
    // - runLen >= 3 -> allow only interior CELLS (not first/last of the run)
    // - runLen == 2 -> allow exactly ONE candidate: seam between the two cells (distance==1 from both ends)
    // - runLen == 1 -> none
    //
    // NOTE: hinge-safe removed (per your request).
    // ----------------------------------------------------------
    public static List<DoorSpot> FromCarvedWalls(
        bool[,] grid,
        List<Vector2Int> carvedWalls, // kept for API compatibility, not used for candidate enumeration
        int width,
        int height,
        float doorWidthCells = 0.2f)
    {
        List<DoorSpot> spots = new();

        // normalize (do NOT clamp to <=1; if door wider than cell, offsets will fallback to center)
        doorWidthCells = Mathf.Max(0.01f, doorWidthCells);
        float half = doorWidthCells * 0.5f;

        // stats
        int totalCellsScanned = 0;
        int totalOpenCells = 0;
        int totalStraightCells = 0;

        int runLenLE1 = 0;
        int runLenEQ2 = 0;
        int endCellsSkipped = 0;

        int seamCandidatesAdded = 0;
        int interiorCellsPassed = 0;
        int subCandidatesAdded = 0;

        // avoid duplicating the special seam candidate for len==2 runs
        HashSet<int> seamRunKeys = new();

        // Scan ALL interior cells of the grid (this is the important fix)
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                totalCellsScanned++;

                if (grid[x, y]) // wall
                    continue;

                totalOpenCells++;

                bool leftOpen = !grid[x - 1, y];
                bool rightOpen = !grid[x + 1, y];
                bool downOpen = !grid[x, y - 1];
                bool upOpen = !grid[x, y + 1];

                bool isHorizontal = leftOpen && rightOpen && !upOpen && !downOpen; // passage L<->R, walls U/D
                bool isVertical = upOpen && downOpen && !leftOpen && !rightOpen;   // passage D<->U, walls L/R

                if (!isHorizontal && !isVertical)
                    continue;

                totalStraightCells++;

                // Determine corridor run extents
                int runMinX = x, runMaxX = x;
                int runMinY = y, runMaxY = y;

                int runLen;
                if (isHorizontal)
                {
                    GetHorizontalRun(grid, width, height, x, y, out runMinX, out runMaxX);
                    runLen = runMaxX - runMinX + 1;
                }
                else
                {
                    GetVerticalRun(grid, width, height, x, y, out runMinY, out runMaxY);
                    runLen = runMaxY - runMinY + 1;
                }

                // Corridor legality
                if (runLen <= 1)
                {
                    runLenLE1++;
                    continue;
                }

                // base rotation
                Quaternion rot = isHorizontal
                    ? Quaternion.Euler(0f, 90f, 0f)
                    : Quaternion.Euler(0f, 0f, 0f);

                // ---- runLen == 2: ONE seam candidate ----
                if (runLen == 2)
                {
                    runLenEQ2++;

                    int runKey = isHorizontal
                        ? HashRunKey(axis: 0, fixedCoord: y, runMin: runMinX, runMax: runMaxX)
                        : HashRunKey(axis: 1, fixedCoord: x, runMin: runMinY, runMax: runMaxY);

                    if (seamRunKeys.Contains(runKey))
                        continue;

                    seamRunKeys.Add(runKey);

                    // seam between the TWO corridor cells (these are the passable sides)
                    Vector2Int c0 = isHorizontal ? new Vector2Int(runMinX, y) : new Vector2Int(x, runMinY);
                    Vector2Int c1 = isHorizontal ? new Vector2Int(runMaxX, y) : new Vector2Int(x, runMaxY);

                    // Put the candidate in the first cell with offset +0.5 toward the second cell
                    Vector2Int seamCell = c0;
                    Vector2 seamOffset = isHorizontal ? new Vector2(+0.5f, 0f) : new Vector2(0f, +0.5f);

                    int id = HashCandidateId(seamCell, seamOffset, isHorizontal, subIndex: 0);

                    spots.Add(new DoorSpot
                    {
                        cell = seamCell,
                        rotation = rot,
                        aOpen = c0,
                        bOpen = c1,
                        offset = seamOffset,
                        id = id
                    });

                    seamCandidatesAdded++;
                    continue;
                }

                // ---- runLen >= 3: only interior CELLS (distance < 1 cell from end is forbidden) ----
                bool isEndCell = isHorizontal
                    ? (x == runMinX || x == runMaxX)
                    : (y == runMinY || y == runMaxY);

                if (isEndCell)
                {
                    endCellsSkipped++;
                    continue;
                }

                interiorCellsPassed++;

                // For normal interior corridor cell: passable sides are the neighbors along the corridor axis
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

                // Generate multiple offsets inside this cell along corridor axis.
                // positions range inside cell: [-0.5 + half, +0.5 - half] step=doorWidthCells
                List<float> axisOffsets = GenerateAxisOffsets(half, doorWidthCells);

                for (int oi = 0; oi < axisOffsets.Count; oi++)
                {
                    float t = axisOffsets[oi];

                    Vector2 off = isHorizontal ? new Vector2(t, 0f) : new Vector2(0f, t);
                    int id = HashCandidateId(new Vector2Int(x, y), off, isHorizontal, oi);

                    spots.Add(new DoorSpot
                    {
                        cell = new Vector2Int(x, y),
                        rotation = rot,
                        aOpen = aOpen,
                        bOpen = bOpen,
                        offset = off,
                        id = id
                    });

                    subCandidatesAdded++;
                }
            }
        }

        DLog(
            "[DoorPlacement] Candidate scan stats:\n" +
            $"  scannedCells={totalCellsScanned}\n" +
            $"  openCells(all)={totalOpenCells}\n" +
            $"  straightCorridorCells={totalStraightCells}\n" +
            $"  runLen<=1 (rejected)={runLenLE1}\n" +
            $"  runLen==2 (seam-handled visits)={runLenEQ2}\n" +
            $"  endCellsSkipped(runLen>=3)={endCellsSkipped}\n" +
            $"  interiorCellsPassed(runLen>=3)={interiorCellsPassed}\n" +
            $"  seamCandidatesAdded={seamCandidatesAdded}\n" +
            $"  subCandidatesAdded={subCandidatesAdded}\n" +
            $"  FINAL spots(sub-cell total)={spots.Count}  (doorWidthCells={doorWidthCells:0.###})"
        );

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
    /// - Enforces "uniform-ish" distribution via greedy max-min (maximizes nearest-distance).
    /// - Relaxes minDist gradually until all doors are planned (or candidates exhausted).
    /// - Keeps start clear (StartCell + N forward).
    /// - Keeps forced exit clear (also used as a distance "reserve").
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
        int nearPathManhattanRadius = 1,
        float doorWidthCells = 0.2f)
    {
        // candidates (sub-cell) — carvedWalls kept only for signature compatibility
        List<DoorSpot> spots = FromCarvedWalls(grid, carvedWalls, width, height, doorWidthCells);
        DLog($"[DoorPlacement] PlanDoors: initial spots={spots.Count} (wantPuzzle={wantPuzzle}, wantNormal={wantNormal})");

        // Always block these cells:
        HashSet<Vector2Int> blockedCells = new();
        blockedCells.Add(forcedExitCell);
        blockedCells.Add(startCell);

        // keep clear "N forward"
        int beforeForward = blockedCells.Count;
        AddForwardClearCells(blockedCells, grid, width, height, startCell, startForwardDir, keepClearStepsForward);
        DLog($"[DoorPlacement] blockedCells: base={beforeForward} +forwardClear -> {blockedCells.Count}");

        // remove blocked from base candidates (by CELL; offsets don't matter here)
        int beforeBlockedFilter = spots.Count;
        spots = FilterOutCellSet(spots, blockedCells);
        DLog($"[DoorPlacement] spots after blocked filter: {beforeBlockedFilter} -> {spots.Count}");

        // Build path for puzzle prioritization (strict/near/any)
        List<Vector2Int> solutionPath = FindPathBFS(grid, width, height, startCell, forcedExitCell);
        HashSet<Vector2Int> pathSet = new(solutionPath);
        DLog($"[DoorPlacement] solutionPath length={solutionPath.Count} (pathSet={pathSet.Count}) nearRadius={nearPathManhattanRadius}");

        List<DoorSpot> puzzleStrict = FilterOnPath(spots, pathSet); // both passable sides on path
        List<DoorSpot> puzzleNear = BuildNearPathCandidates(spots, pathSet, nearPathManhattanRadius);
        List<DoorSpot> puzzleAny = spots;

        DLog($"[DoorPlacement] puzzle candidates: strict={puzzleStrict.Count} near={puzzleNear.Count} any={puzzleAny.Count}");

        // used spots for distance scoring (continuous position in cell-units)
        List<DoorSpot> used = new();

        // Reserve forced exit for distance scoring (so doors won't cluster near it)
        used.Add(new DoorSpot
        {
            cell = forcedExitCell,
            rotation = Quaternion.identity,
            aOpen = forcedExitCell,
            bOpen = forcedExitCell,
            offset = Vector2.zero,
            id = 0
        });

        // plan outputs
        List<DoorSpot> plannedPuzzle = new();
        List<DoorSpot> plannedNormal = new();

        // track exact chosen candidates (NOT by cell anymore)
        HashSet<int> usedIds = new();

        int placedPuzzle = 0;
        int placedNormal = 0;

        // scope widening for puzzle doors
        int puzzleScope = 0; // 0=strict, 1=near, 2=any

        // ensure minDist steps exist
        if (minDistStepsCells == null || minDistStepsCells.Length == 0)
            minDistStepsCells = new float[] { 4f, 3f, 2f, 1f, 0.5f, 0f };

        for (int step = 0; step < minDistStepsCells.Length; step++)
        {
            float minDist = minDistStepsCells[step];
            DLog($"[DoorPlacement] ---- Relax step {step + 1}/{minDistStepsCells.Length} | minDist={minDist} | puzzleScope={puzzleScope} ----");

            // -------- PUZZLE --------
            while (placedPuzzle < wantPuzzle)
            {
                List<DoorSpot> baseCandidates =
                    (puzzleScope == 0) ? puzzleStrict :
                    (puzzleScope == 1) ? puzzleNear :
                    puzzleAny;

                List<DoorSpot> candidates = FilterOutUsedIds(baseCandidates, usedIds);
                candidates = FilterOutCellSet(candidates, blockedCells);

                DLog($"[DoorPlacement] Puzzle loop: base={baseCandidates.Count} afterUsed+blocked={candidates.Count} placed={placedPuzzle}/{wantPuzzle}");

                if (!TrySelectSpotMaxMin(candidates, used, minDist, out DoorSpot chosen))
                {
                    DLog("[DoorPlacement] Puzzle loop: no selectable spot at this minDist.");
                    break;
                }

                plannedPuzzle.Add(chosen);
                used.Add(chosen);
                usedIds.Add(chosen.id);
                placedPuzzle++;

                DLog($"[DoorPlacement] +PUZZLE at cell={chosen.cell} off={chosen.offset} | now placedPuzzle={placedPuzzle}/{wantPuzzle} usedTotal={used.Count}");
            }

            if (placedPuzzle < wantPuzzle && puzzleScope < 2)
            {
                puzzleScope++;
                DLog($"[DoorPlacement] Puzzle still missing -> widen scope to {puzzleScope} and retry same minDist.");
                step--;
                continue;
            }

            // -------- NORMAL --------
            while (placedNormal < wantNormal)
            {
                List<DoorSpot> candidates = FilterOutUsedIds(spots, usedIds);
                candidates = FilterOutCellSet(candidates, blockedCells);

                DLog($"[DoorPlacement] Normal loop: base={spots.Count} afterUsed+blocked={candidates.Count} placed={placedNormal}/{wantNormal}");

                if (!TrySelectSpotMaxMin(candidates, used, minDist, out DoorSpot chosen))
                {
                    DLog("[DoorPlacement] Normal loop: no selectable spot at this minDist.");
                    break;
                }

                plannedNormal.Add(chosen);
                used.Add(chosen);
                usedIds.Add(chosen.id);
                placedNormal++;

                DLog($"[DoorPlacement] +NORMAL at cell={chosen.cell} off={chosen.offset} | now placedNormal={placedNormal}/{wantNormal} usedTotal={used.Count}");
            }

            if (placedPuzzle >= wantPuzzle && placedNormal >= wantNormal)
                break;
        }

        DLog($"[DoorPlacement] PlanDoors result:\n" +
             $"  placedPuzzle={placedPuzzle}/{wantPuzzle}\n" +
             $"  placedNormal={placedNormal}/{wantNormal}\n" +
             $"  finalUsed(inc exit-reserve)={used.Count}");

        return new DoorPlan(plannedPuzzle, plannedNormal, wantPuzzle, wantNormal, placedPuzzle, placedNormal);
    }

    // ==========================================================
    // Corridor-run helpers
    // ==========================================================
    private static void GetHorizontalRun(bool[,] grid, int width, int height, int x, int y, out int minX, out int maxX)
    {
        minX = x;
        maxX = x;

        while (minX - 1 > 0 && IsStraightHorizontalCell(grid, width, height, minX - 1, y))
            minX--;

        while (maxX + 1 < width - 1 && IsStraightHorizontalCell(grid, width, height, maxX + 1, y))
            maxX++;
    }

    private static void GetVerticalRun(bool[,] grid, int width, int height, int x, int y, out int minY, out int maxY)
    {
        minY = y;
        maxY = y;

        while (minY - 1 > 0 && IsStraightVerticalCell(grid, width, height, x, minY - 1))
            minY--;

        while (maxY + 1 < height - 1 && IsStraightVerticalCell(grid, width, height, x, maxY + 1))
            maxY++;
    }

    private static bool IsStraightHorizontalCell(bool[,] grid, int width, int height, int x, int y)
    {
        if (x <= 0 || y <= 0 || x >= width - 1 || y >= height - 1)
            return false;

        if (grid[x, y]) return false;

        bool leftOpen = !grid[x - 1, y];
        bool rightOpen = !grid[x + 1, y];
        bool upOpen = !grid[x, y + 1];
        bool downOpen = !grid[x, y - 1];

        return leftOpen && rightOpen && !upOpen && !downOpen;
    }

    private static bool IsStraightVerticalCell(bool[,] grid, int width, int height, int x, int y)
    {
        if (x <= 0 || y <= 0 || x >= width - 1 || y >= height - 1)
            return false;

        if (grid[x, y]) return false;

        bool leftOpen = !grid[x - 1, y];
        bool rightOpen = !grid[x + 1, y];
        bool upOpen = !grid[x, y + 1];
        bool downOpen = !grid[x, y - 1];

        return upOpen && downOpen && !leftOpen && !rightOpen;
    }

    // ==========================================================
    // Sub-cell offset generation
    // ==========================================================
    private static List<float> GenerateAxisOffsets(float halfDoor, float step)
    {
        // Valid center positions in cell local axis are within [-0.5+half, +0.5-half]
        float a = -0.5f + halfDoor;
        float b = 0.5f - halfDoor;

        List<float> res = new();

        if (b < a)
        {
            // door too wide, fallback to center
            res.Add(0f);
            return res;
        }

        // Deterministic stepping from a to b by step (inclusive end)
        step = Mathf.Max(0.0001f, step);

        float t = a;
        int guard = 0;
        while (t <= b + 1e-5f && guard++ < 10000)
        {
            res.Add(t);
            t += step;
        }

        // If last point is far from b, include b to hit the far edge
        if (res.Count == 0 || Mathf.Abs(res[res.Count - 1] - b) > 1e-3f)
            res.Add(b);

        // Optional: ensure center included when it fits (helps symmetry)
        if (0f >= a - 1e-5f && 0f <= b + 1e-5f)
        {
            bool hasCenter = false;
            for (int i = 0; i < res.Count; i++)
                if (Mathf.Abs(res[i]) < 1e-3f) { hasCenter = true; break; }
            if (!hasCenter) res.Add(0f);
        }

        res.Sort();
        return res;
    }

    // ==========================================================
    // Filtering helpers
    // ==========================================================
    private static List<DoorSpot> FilterOutUsedIds(List<DoorSpot> all, HashSet<int> usedIds)
    {
        if (all == null) return new List<DoorSpot>();
        if (usedIds == null || usedIds.Count == 0) return new List<DoorSpot>(all);

        List<DoorSpot> res = new();
        for (int i = 0; i < all.Count; i++)
        {
            var s = all[i];
            if (usedIds.Contains(s.id)) continue;
            res.Add(s);
        }
        return res;
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

    private static bool InBounds(Vector2Int c, int w, int h) =>
        c.x >= 0 && c.y >= 0 && c.x < w && c.y < h;

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
    // Placement validity + selection (uses continuous position)
    // ==========================================================
    private static Vector2 SpotPos2(DoorSpot s) =>
        new Vector2(s.cell.x + s.offset.x, s.cell.y + s.offset.y);

    public static bool IsValidSpot(DoorSpot spot, List<DoorSpot> used, float minDist)
    {
        if (used == null || used.Count == 0) return true;
        if (minDist <= 0f) return true;

        Vector2 p = SpotPos2(spot);

        foreach (var u in used)
        {
            Vector2 q = SpotPos2(u);
            if (Vector2.Distance(p, q) < minDist)
                return false;
        }

        return true;
    }

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

            Vector2 sp = SpotPos2(s);
            float nearest = float.PositiveInfinity;

            if (used != null && used.Count > 0)
            {
                for (int k = 0; k < used.Count; k++)
                {
                    Vector2 up = SpotPos2(used[k]);
                    float d = Vector2.Distance(sp, up);
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
        DLog($"[DoorPlacement] TrySelectSpotMaxMin: candidates={candidates.Count}, minDist={minDist}, chosen=YES bestScore={bestScore:0.###}");
        return true;
    }

    // ==========================================================
    // Start forward clear
    // ==========================================================
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

    // ==========================================================
    // Hash helpers (stable IDs)
    // ==========================================================
    private static int HashRunKey(int axis, int fixedCoord, int runMin, int runMax)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + axis;
            h = h * 31 + fixedCoord;
            h = h * 31 + runMin;
            h = h * 31 + runMax;
            return h;
        }
    }

    private static int HashCandidateId(Vector2Int cell, Vector2 off, bool isHorizontal, int subIndex)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + cell.x;
            h = h * 31 + cell.y;
            h = h * 31 + (isHorizontal ? 1 : 2);
            h = h * 31 + subIndex;

            // quantized offsets to avoid float instability
            int ox = Mathf.RoundToInt(off.x * 10000f);
            int oy = Mathf.RoundToInt(off.y * 10000f);
            h = h * 31 + ox;
            h = h * 31 + oy;
            return h;
        }
    }

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
