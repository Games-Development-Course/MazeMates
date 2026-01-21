// Assets/Scripts/Maze/ResourcePlacement.cs
//
// Fixes for the bad clustering you saw:
// ✅ First placement is NOT random anymore: it chooses the cell farthest (walking distance) from start.
// ✅ Type selection is NOT shuffled bag anymore: it uses "Most-Constrained-First" each step
//    (pick the type whose best legal placement is worst right now).
// ✅ Placement scoring is GEODESIC (walking distance in the maze) via Multi-Source BFS.
// ✅ Local-search polish fixed (keeps original want, consistent comparisons) and only accepts true improvements.
// ✅ No-Path stays strict in strict stages.
//
// Priority (lexicographic):
// 1) placed (higher)
// 2) No-Path violations (lower)
// 3) geodesicSpread (higher)
// 4) minSeparation violations (lower)
// 5) adjacency violations (lower)
// Tie-break: prefer stricter stage, then higher sep

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public static class ResourcePlacement
{
    public struct ResourceRequest
    {
        public GameObject prefab;
        public int amount;
        public ResourceRequest(GameObject prefab, int amount) { this.prefab = prefab; this.amount = amount; }
    }

    private enum RelaxStage
    {
        Strict_AllRules,             // adjacency(8) + noPath + minSeparation
        Adj4_KeepNoPath,             // adjacency(4) + noPath + minSeparation
        NoPathRelaxed_KeepAdjacency, // adjacency(8) + minSeparation
        Adj4_NoPathRelaxed           // adjacency(4) + minSeparation
    }

    private struct PlannedItem { public Vector2Int cell; public int typeId; }

    private struct PlanResult
    {
        public readonly List<PlannedItem> items;
        public readonly int placed;
        public readonly int want;

        public readonly int vAdj;
        public readonly int vPath;
        public readonly int vSep;

        public readonly int geodesicSpread; // higher better
        public readonly int usedSep;
        public readonly RelaxStage usedStage;

        public PlanResult(
            List<PlannedItem> items,
            int placed,
            int want,
            int vAdj,
            int vPath,
            int vSep,
            int geodesicSpread,
            int usedSep,
            RelaxStage usedStage)
        {
            this.items = items;
            this.placed = placed;
            this.want = want;
            this.vAdj = vAdj;
            this.vPath = vPath;
            this.vSep = vSep;
            this.geodesicSpread = geodesicSpread;
            this.usedSep = usedSep;
            this.usedStage = usedStage;
        }
    }

    public static void PlaceAllResourcesEvenly(
        bool[,] grid,
        List<Vector2Int> pathCells,
        HashSet<Vector2Int> blockedCells,
        float cellSize,
        Transform parent,
        ResourceRequest[] requests,
        Vector2Int? startCell = null,
        int keepClearStepsForward = 3,
        Vector2Int? forwardDir = null,
        float yOffset = 0.4f,
        int minSeparationCells = 2,
        int maxNeighborWallsAllowed = 2,
        int wallsLayer = 0,
        float collisionRadiusMultiplier = 0.22f
    )
    {
        if (grid == null || pathCells == null || parent == null || requests == null)
            return;

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            return;

        blockedCells ??= new HashSet<Vector2Int>();

        // active types
        List<ResourceRequest> active = new();
        int wantTotal = 0;
        for (int i = 0; i < requests.Length; i++)
        {
            if (requests[i].prefab == null) continue;
            if (requests[i].amount <= 0) continue;
            active.Add(requests[i]);
            wantTotal += requests[i].amount;
        }
        if (wantTotal <= 0) return;

        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        // reserve start + N forward
        if (startCell.HasValue)
        {
            blockedCells.Add(startCell.Value);

            if (keepClearStepsForward > 0)
            {
                Vector2Int dir = forwardDir ?? new Vector2Int(0, 1);
                dir = new Vector2Int(Mathf.Clamp(dir.x, -1, 1), Mathf.Clamp(dir.y, -1, 1));
                if (dir == Vector2Int.zero) dir = new Vector2Int(0, 1);

                for (int i = 1; i <= keepClearStepsForward; i++)
                {
                    Vector2Int c = startCell.Value + dir * i;
                    if (Inside(c.x, c.y, w, h)) blockedCells.Add(c);
                }
            }
        }

        // build valid candidates (hard filters)
        List<Vector2Int> valid = new();
        int wallMask = 1 << wallsLayer;
        float collisionRadius = Mathf.Max(0.01f, cellSize * collisionRadiusMultiplier);

        foreach (var cell in pathCells)
        {
            if (!Inside(cell.x, cell.y, w, h)) continue;
            if (blockedCells.Contains(cell)) continue;
            if (grid[cell.x, cell.y]) continue;
            if (!IsCellSafeFromWalls(grid, cell, maxNeighborWallsAllowed)) continue;

            Vector3 worldPos = CellCenterWorld(cell, cellSize, parent, yOffset);
            if (Physics.CheckSphere(worldPos, collisionRadius, wallMask, QueryTriggerInteraction.Ignore))
                continue;

            valid.Add(cell);
        }

        if (valid.Count == 0)
        {
            Debug.LogWarning("[ResourcePlacement] No valid cells.");
            return;
        }

        int attempts = Mathf.Clamp(110 + wantTotal * 18, 110, 700);
        int sepStart = Mathf.Max(0, minSeparationCells);

        RelaxStage[] stages = new[]
        {
            RelaxStage.Strict_AllRules,
            RelaxStage.Adj4_KeepNoPath,
            RelaxStage.NoPathRelaxed_KeepAdjacency,
            RelaxStage.Adj4_NoPathRelaxed
        };

        Debug.Log($"[ResourcePlacement] PLAN start: want={wantTotal} types={active.Count} valid={valid.Count} attempts={attempts}");

        PlanResult best = default;
        bool bestInit = false;

        int fullPlans = 0;
        int bestFullMinPath = int.MaxValue;

        for (int sep = sepStart; sep >= 0; sep--)
        {
            for (int s = 0; s < stages.Length; s++)
            {
                RelaxStage stage = stages[s];
                int perStageAttempts = Mathf.Max(18, attempts / 8);

                for (int a = 0; a < perStageAttempts; a++)
                {
                    var plan = TryBuildPlan_Geodesic_ConstrainedFirst(
                        valid: valid,
                        grid: grid,
                        w: w,
                        h: h,
                        active: active,
                        baseBlocked: blockedCells,
                        wantTotal: wantTotal,
                        minSeparationCells: sep,
                        stage: stage,
                        startCell: startCell);

                    if (plan.placed == wantTotal)
                    {
                        fullPlans++;
                        if (plan.vPath < bestFullMinPath) bestFullMinPath = plan.vPath;
                    }

                    if (!bestInit || IsPlanBetter(plan, best))
                    {
                        best = plan;
                        bestInit = true;
                    }
                }
            }
        }

        if (!bestInit || best.items == null || best.items.Count == 0)
        {
            Debug.LogWarning("[ResourcePlacement] Planning failed (no items).");
            return;
        }

        Debug.Log($"[ResourcePlacement] PLAN summary: fullPlans={fullPlans}, fullMinPath={(bestFullMinPath == int.MaxValue ? -1 : bestFullMinPath)}");
        Debug.Log($"[ResourcePlacement] BEST pre-polish: placed={best.placed}/{best.want} pathV={best.vPath} spread={best.geodesicSpread} sepV={best.vSep} adjV={best.vAdj} sep={best.usedSep} stage={best.usedStage}");

        // polish only full plans
        if (best.placed == best.want)
        {
            best = PolishPlan_LocalSearch(best, valid, grid, w, h, blockedCells);
            Debug.Log($"[ResourcePlacement] BEST post-polish: placed={best.placed}/{best.want} pathV={best.vPath} spread={best.geodesicSpread} sepV={best.vSep} adjV={best.vAdj} sep={best.usedSep} stage={best.usedStage}");
        }

        // instantiate + spawn
        for (int i = 0; i < best.items.Count; i++)
        {
            var it = best.items[i];
            if (it.typeId < 0 || it.typeId >= active.Count) continue;

            Vector3 pos = CellCenterWorld(it.cell, cellSize, parent, yOffset);
            GameObject go = Object.Instantiate(active[it.typeId].prefab, pos, Quaternion.identity, parent);
            var pickup = go.GetComponent<PickupObject>();
            if (pickup != null &&
                (pickup.type == PickupObject.PickupType.Heart ||
                 pickup.type == PickupObject.PickupType.Bomb))
            {
                var p = go.transform.position;
                p.y += 0.5f;
                go.transform.position = p;
            }

            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();

            blockedCells.Add(it.cell);
        }

        if (best.placed < wantTotal)
            Debug.LogWarning($"[ResourcePlacement] Could not place all resources. Placed={best.placed}/{wantTotal} (pathV={best.vPath}, spread={best.geodesicSpread}).");
    }

    // ============================================================
    // Comparator (lexicographic)
    // ============================================================
    private static bool IsPlanBetter(in PlanResult a, in PlanResult b)
    {
        if (a.placed != b.placed) return a.placed > b.placed;
        if (a.vPath != b.vPath) return a.vPath < b.vPath;
        if (a.geodesicSpread != b.geodesicSpread) return a.geodesicSpread > b.geodesicSpread;
        if (a.vSep != b.vSep) return a.vSep < b.vSep;
        if (a.vAdj != b.vAdj) return a.vAdj < b.vAdj;

        if (a.usedStage != b.usedStage) return a.usedStage < b.usedStage;
        if (a.usedSep != b.usedSep) return a.usedSep > b.usedSep;

        return false;
    }

    // ============================================================
    // Plan builder: Constrained-first type selection + geodesic placement
    // ============================================================
    private static PlanResult TryBuildPlan_Geodesic_ConstrainedFirst(
        List<Vector2Int> valid,
        bool[,] grid,
        int w,
        int h,
        List<ResourceRequest> active,
        HashSet<Vector2Int> baseBlocked,
        int wantTotal,
        int minSeparationCells,
        RelaxStage stage,
        Vector2Int? startCell)
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>(baseBlocked);
        Dictionary<Vector2Int, int> placedType = new Dictionary<Vector2Int, int>();
        List<PlannedItem> items = new List<PlannedItem>(wantTotal);

        // remaining counts per type
        int typeCount = active.Count;
        int[] remaining = new int[typeCount];
        for (int t = 0; t < typeCount; t++) remaining[t] = active[t].amount;

        bool checkNoPath = (stage == RelaxStage.Strict_AllRules || stage == RelaxStage.Adj4_KeepNoPath);
        bool adjacency8 = (stage == RelaxStage.Strict_AllRules || stage == RelaxStage.NoPathRelaxed_KeepAdjacency);

        // Precompute distances-from-start (geodesic) to choose a good first point
        Dictionary<Vector2Int, int> distFromStart = null;
        if (startCell.HasValue && Inside(startCell.Value.x, startCell.Value.y, w, h) && !grid[startCell.Value.x, startCell.Value.y])
            distFromStart = BuildSingleSourceDistanceMap(grid, w, h, startCell.Value);

        int placed = 0;

        while (placed < wantTotal)
        {
            // Build dist-to-nearest-placed (geodesic). Empty => we'll use distFromStart for "farthest first".
            Dictionary<Vector2Int, int> distToNearest = (placedType.Count > 0)
                ? BuildMultiSourceDistanceMap(grid, w, h, placedType.Keys)
                : null;

            // Choose which type to place now: Most-Constrained-First
            if (!TryChooseNextType_MostConstrained(
                    valid, blocked, placedType, distToNearest, distFromStart,
                    remaining, minSeparationCells, adjacency8, checkNoPath, w, h, grid,
                    out int chosenType))
            {
                break;
            }

            // Choose best cell for that type (maximize geodesic distance to nearest resource)
            if (!TryPickBestCellForType(
                    valid, blocked, placedType, distToNearest, distFromStart,
                    chosenType, minSeparationCells, adjacency8, checkNoPath, w, h, grid,
                    out Vector2Int chosenCell))
            {
                break;
            }

            blocked.Add(chosenCell);
            placedType[chosenCell] = chosenType;
            items.Add(new PlannedItem { cell = chosenCell, typeId = chosenType });
            remaining[chosenType]--;
            placed++;
        }

        EvaluatePlan(items, placedType, w, h, grid, out int vAdj, out int vPath, out int vSep, minSeparationCells, adjacency8);
        int spread = ComputeGeodesicSpread(items, grid, w, h);

        return new PlanResult(items, placed, wantTotal, vAdj, vPath, vSep, spread, minSeparationCells, stage);
    }

    // Pick the next type to place:
    // For each type with remaining>0, estimate its BEST achievable geodesic score right now.
    // Choose the type with the LOWEST best score (most constrained).
    private static bool TryChooseNextType_MostConstrained(
        List<Vector2Int> valid,
        HashSet<Vector2Int> blocked,
        Dictionary<Vector2Int, int> placedType,
        Dictionary<Vector2Int, int> distToNearest,
        Dictionary<Vector2Int, int> distFromStart,
        int[] remaining,
        int minSeparationCells,
        bool adjacency8,
        bool checkNoPath,
        int w,
        int h,
        bool[,] grid,
        out int chosenType)
    {
        chosenType = -1;

        int bestType = -1;
        int bestTypeBestScore = int.MaxValue;

        // small randomization to avoid deterministic patterns
        List<int> typeOrder = new List<int>(remaining.Length);
        for (int t = 0; t < remaining.Length; t++) if (remaining[t] > 0) typeOrder.Add(t);
        Shuffle(typeOrder);

        for (int k = 0; k < typeOrder.Count; k++)
        {
            int t = typeOrder[k];

            if (!EstimateTypeBestScore(
                    valid, blocked, placedType, distToNearest, distFromStart,
                    t, minSeparationCells, adjacency8, checkNoPath, w, h, grid,
                    out int typeBestScore))
            {
                // if type has ZERO legal positions => it's maximally constrained
                typeBestScore = -999999;
            }

            // Most constrained = lowest best score
            if (typeBestScore < bestTypeBestScore)
            {
                bestTypeBestScore = typeBestScore;
                bestType = t;
            }
        }

        if (bestType < 0) return false;
        chosenType = bestType;
        return true;
    }

    // Returns the maximum achievable score for this type among a sampled set of candidates.
    // Score is geodesic distance to nearest placed resource (or distance from start if none placed yet).
    private static bool EstimateTypeBestScore(
        List<Vector2Int> valid,
        HashSet<Vector2Int> blocked,
        Dictionary<Vector2Int, int> placedType,
        Dictionary<Vector2Int, int> distToNearest,
        Dictionary<Vector2Int, int> distFromStart,
        int typeId,
        int minSeparationCells,
        bool adjacency8,
        bool checkNoPath,
        int w,
        int h,
        bool[,] grid,
        out int bestScore)
    {
        bestScore = int.MinValue;
        bool found = false;

        int samples = Mathf.Min(260, valid.Count);
        for (int i = 0; i < samples; i++)
        {
            var c = valid[Random.Range(0, valid.Count)];
            if (blocked.Contains(c)) continue;

            if (minSeparationCells > 0 && !IsFarEnoughFromAll(c, placedType, minSeparationCells))
                continue;

            if (HasSameTypeNeighbor(c, placedType, typeId, adjacency8))
                continue;

            if (checkNoPath && ExistsPathToSameTypeWithoutOtherResources(c, placedType, typeId, w, h, grid))
                continue;

            int score = ScoreCell(c, placedType, distToNearest, distFromStart);
            if (score > bestScore) bestScore = score;
            found = true;
        }

        return found;
    }

    private static bool TryPickBestCellForType(
        List<Vector2Int> valid,
        HashSet<Vector2Int> blocked,
        Dictionary<Vector2Int, int> placedType,
        Dictionary<Vector2Int, int> distToNearest,
        Dictionary<Vector2Int, int> distFromStart,
        int typeId,
        int minSeparationCells,
        bool adjacency8,
        bool checkNoPath,
        int w,
        int h,
        bool[,] grid,
        out Vector2Int chosen)
    {
        chosen = default;
        bool found = false;

        int bestScore = int.MinValue;

        // sample subset for speed; still gives great spread
        int samples = Mathf.Min(600, valid.Count);
        for (int i = 0; i < samples; i++)
        {
            var c = valid[Random.Range(0, valid.Count)];
            if (blocked.Contains(c)) continue;

            if (minSeparationCells > 0 && !IsFarEnoughFromAll(c, placedType, minSeparationCells))
                continue;

            if (HasSameTypeNeighbor(c, placedType, typeId, adjacency8))
                continue;

            if (checkNoPath && ExistsPathToSameTypeWithoutOtherResources(c, placedType, typeId, w, h, grid))
                continue;

            int score = ScoreCell(c, placedType, distToNearest, distFromStart);
            if (!found || score > bestScore)
            {
                bestScore = score;
                chosen = c;
                found = true;
            }
        }

        // fallback: full scan if sampling missed good options
        if (!found)
        {
            for (int i = 0; i < valid.Count; i++)
            {
                var c = valid[i];
                if (blocked.Contains(c)) continue;

                if (minSeparationCells > 0 && !IsFarEnoughFromAll(c, placedType, minSeparationCells))
                    continue;

                if (HasSameTypeNeighbor(c, placedType, typeId, adjacency8))
                    continue;

                if (checkNoPath && ExistsPathToSameTypeWithoutOtherResources(c, placedType, typeId, w, h, grid))
                    continue;

                int score = ScoreCell(c, placedType, distToNearest, distFromStart);
                if (!found || score > bestScore)
                {
                    bestScore = score;
                    chosen = c;
                    found = true;
                }
            }
        }

        return found;
    }

    private static int ScoreCell(
        Vector2Int cell,
        Dictionary<Vector2Int, int> placedType,
        Dictionary<Vector2Int, int> distToNearest,
        Dictionary<Vector2Int, int> distFromStart)
    {
        // If we already have placed resources: maximize distance to nearest placed (geodesic)
        if (placedType != null && placedType.Count > 0)
            return (distToNearest != null && distToNearest.TryGetValue(cell, out int d)) ? d : 0;

        // First placement: maximize distance-from-start in maze (geodesic), not random
        if (distFromStart != null && distFromStart.TryGetValue(cell, out int ds))
            return ds;

        // if no start map, just give 0 (tie broken by randomness in sampling)
        return 0;
    }

    // ============================================================
    // Local Search polish (fixed want + strict acceptance)
    // ============================================================
    private static PlanResult PolishPlan_LocalSearch(
        PlanResult basePlan,
        List<Vector2Int> valid,
        bool[,] grid,
        int w,
        int h,
        HashSet<Vector2Int> baseBlocked)
    {
        if (basePlan.items == null || basePlan.items.Count == 0) return basePlan;
        if (basePlan.placed != basePlan.want) return basePlan;

        int want = basePlan.want;
        int minSep = basePlan.usedSep;

        bool adjacency8 = (basePlan.usedStage == RelaxStage.Strict_AllRules || basePlan.usedStage == RelaxStage.NoPathRelaxed_KeepAdjacency);
        bool checkNoPath = (basePlan.usedStage == RelaxStage.Strict_AllRules || basePlan.usedStage == RelaxStage.Adj4_KeepNoPath);

        // rebuild state
        List<PlannedItem> items = new List<PlannedItem>(basePlan.items);
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>(baseBlocked);
        Dictionary<Vector2Int, int> placedType = new Dictionary<Vector2Int, int>();

        for (int i = 0; i < items.Count; i++)
        {
            blocked.Add(items[i].cell);
            placedType[items[i].cell] = items[i].typeId;
        }

        PlanResult current = ReEvaluate(items, placedType, grid, w, h, minSep, adjacency8, basePlan.usedStage, want);

        int iterations = Mathf.Clamp(140 + items.Count * 30, 200, 1000);

        for (int it = 0; it < iterations; it++)
        {
            int idx = Random.Range(0, items.Count);
            PlannedItem old = items[idx];

            // remove
            placedType.Remove(old.cell);
            blocked.Remove(old.cell);

            Dictionary<Vector2Int, int> distToNearest = BuildMultiSourceDistanceMap(grid, w, h, placedType.Keys);

            // try new spot for same type
            if (TryPickBestCellForType(
                    valid, blocked, placedType,
                    distToNearest, distFromStart: null,
                    old.typeId, minSep, adjacency8, checkNoPath, w, h, grid,
                    out Vector2Int newCell))
            {
                // apply
                items[idx] = new PlannedItem { cell = newCell, typeId = old.typeId };
                blocked.Add(newCell);
                placedType[newCell] = old.typeId;

                PlanResult candidate = ReEvaluate(items, placedType, grid, w, h, minSep, adjacency8, basePlan.usedStage, want);

                if (IsPlanBetter(candidate, current))
                {
                    current = candidate; // keep
                }
                else
                {
                    // revert
                    placedType.Remove(newCell);
                    blocked.Remove(newCell);

                    items[idx] = old;
                    blocked.Add(old.cell);
                    placedType[old.cell] = old.typeId;
                }
            }
            else
            {
                // restore old
                items[idx] = old;
                blocked.Add(old.cell);
                placedType[old.cell] = old.typeId;
            }
        }

        return current;
    }

    private static PlanResult ReEvaluate(
        List<PlannedItem> items,
        Dictionary<Vector2Int, int> placedType,
        bool[,] grid,
        int w,
        int h,
        int minSeparationCells,
        bool adjacency8,
        RelaxStage stage,
        int want)
    {
        EvaluatePlan(items, placedType, w, h, grid, out int vAdj, out int vPath, out int vSep, minSeparationCells, adjacency8);
        int spread = ComputeGeodesicSpread(items, grid, w, h);

        return new PlanResult(
            items: new List<PlannedItem>(items),
            placed: items.Count,
            want: want,
            vAdj: vAdj,
            vPath: vPath,
            vSep: vSep,
            geodesicSpread: spread,
            usedSep: minSeparationCells,
            usedStage: stage
        );
    }

    // ============================================================
    // Metrics
    // ============================================================
    private static void EvaluatePlan(
        List<PlannedItem> items,
        Dictionary<Vector2Int, int> placedType,
        int w,
        int h,
        bool[,] grid,
        out int vAdj,
        out int vPath,
        out int vSep,
        int minSeparationCells,
        bool adjacency8)
    {
        vAdj = 0;
        vPath = 0;
        vSep = 0;

        if (items == null || items.Count == 0) return;

        // adjacency: count each unordered pair once
        for (int i = 0; i < items.Count; i++)
        {
            for (int j = i + 1; j < items.Count; j++)
            {
                if (items[i].typeId != items[j].typeId) continue;

                Vector2Int a = items[i].cell;
                Vector2Int b = items[j].cell;

                int dx = Mathf.Abs(a.x - b.x);
                int dy = Mathf.Abs(a.y - b.y);

                bool neigh4 = (dx + dy == 1);
                bool neigh8 = (dx <= 1 && dy <= 1 && (dx + dy) > 0);

                if (neigh4 || (adjacency8 && neigh8)) vAdj++;
            }
        }

        // min separation violations (Euclidean)
        if (minSeparationCells > 0)
        {
            float minSep = minSeparationCells;
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    float d = Vector2.Distance(items[i].cell, items[j].cell);
                    if (d < minSep) vSep++;
                }
            }
        }

        // path violations: count sources that can reach another same-type without passing other resources
        for (int i = 0; i < items.Count; i++)
        {
            var src = items[i];
            if (ExistsPathToSameTypeWithoutOtherResources(src.cell, placedType, src.typeId, w, h, grid))
                vPath++;
        }
    }

    // Spread = sum of each resource's nearest-neighbor walking distance (maze steps)
    private static int ComputeGeodesicSpread(List<PlannedItem> items, bool[,] grid, int w, int h)
    {
        if (items == null || items.Count <= 1) return 0;

        HashSet<Vector2Int> all = new HashSet<Vector2Int>();
        for (int i = 0; i < items.Count; i++) all.Add(items[i].cell);

        int sum = 0;
        for (int i = 0; i < items.Count; i++)
        {
            Vector2Int start = items[i].cell;
            int d = BfsDistanceToNearestOtherResource(grid, w, h, start, all);
            sum += (d < 0 ? 0 : d);
        }
        return sum;
    }

    private static int BfsDistanceToNearestOtherResource(bool[,] grid, int w, int h, Vector2Int start, HashSet<Vector2Int> allResources)
    {
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        Dictionary<Vector2Int, int> dist = new Dictionary<Vector2Int, int>();

        q.Enqueue(start);
        dist[start] = 0;

        Vector2Int[] dirs =
        {
            new Vector2Int(1,0),
            new Vector2Int(-1,0),
            new Vector2Int(0,1),
            new Vector2Int(0,-1),
        };

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int cd = dist[cur];

            foreach (var d in dirs)
            {
                var nxt = cur + d;
                if (!Inside(nxt.x, nxt.y, w, h)) continue;
                if (grid[nxt.x, nxt.y]) continue;
                if (dist.ContainsKey(nxt)) continue;

                if (allResources.Contains(nxt) && nxt != start)
                    return cd + 1;

                dist[nxt] = cd + 1;
                q.Enqueue(nxt);
            }
        }
        return -1;
    }

    // Multi-source BFS: distance to nearest source (maze steps)
    private static Dictionary<Vector2Int, int> BuildMultiSourceDistanceMap(bool[,] grid, int w, int h, IEnumerable<Vector2Int> sources)
    {
        Dictionary<Vector2Int, int> dist = new Dictionary<Vector2Int, int>();
        Queue<Vector2Int> q = new Queue<Vector2Int>();

        bool any = false;
        foreach (var s in sources)
        {
            if (!Inside(s.x, s.y, w, h)) continue;
            if (grid[s.x, s.y]) continue;

            any = true;
            if (!dist.ContainsKey(s))
            {
                dist[s] = 0;
                q.Enqueue(s);
            }
        }
        if (!any) return dist;

        Vector2Int[] dirs =
        {
            new Vector2Int(1,0),
            new Vector2Int(-1,0),
            new Vector2Int(0,1),
            new Vector2Int(0,-1),
        };

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int cd = dist[cur];

            foreach (var d in dirs)
            {
                var nxt = cur + d;
                if (!Inside(nxt.x, nxt.y, w, h)) continue;
                if (grid[nxt.x, nxt.y]) continue;
                if (dist.ContainsKey(nxt)) continue;

                dist[nxt] = cd + 1;
                q.Enqueue(nxt);
            }
        }

        return dist;
    }

    // Single-source BFS: distance from a given start (maze steps)
    private static Dictionary<Vector2Int, int> BuildSingleSourceDistanceMap(bool[,] grid, int w, int h, Vector2Int start)
    {
        Dictionary<Vector2Int, int> dist = new Dictionary<Vector2Int, int>();
        if (!Inside(start.x, start.y, w, h)) return dist;
        if (grid[start.x, start.y]) return dist;

        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(start);
        dist[start] = 0;

        Vector2Int[] dirs =
        {
            new Vector2Int(1,0),
            new Vector2Int(-1,0),
            new Vector2Int(0,1),
            new Vector2Int(0,-1),
        };

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int cd = dist[cur];

            foreach (var d in dirs)
            {
                var nxt = cur + d;
                if (!Inside(nxt.x, nxt.y, w, h)) continue;
                if (grid[nxt.x, nxt.y]) continue;
                if (dist.ContainsKey(nxt)) continue;

                dist[nxt] = cd + 1;
                q.Enqueue(nxt);
            }
        }
        return dist;
    }

    // ============================================================
    // Rules
    // ============================================================
    private static bool HasSameTypeNeighbor(Vector2Int cell, Dictionary<Vector2Int, int> placedType, int typeId, bool includeDiagonal)
    {
        if (placedType == null || placedType.Count == 0) return false;

        if (placedType.TryGetValue(cell + new Vector2Int(1, 0), out int t1) && t1 == typeId) return true;
        if (placedType.TryGetValue(cell + new Vector2Int(-1, 0), out int t2) && t2 == typeId) return true;
        if (placedType.TryGetValue(cell + new Vector2Int(0, 1), out int t3) && t3 == typeId) return true;
        if (placedType.TryGetValue(cell + new Vector2Int(0, -1), out int t4) && t4 == typeId) return true;

        if (!includeDiagonal) return false;

        if (placedType.TryGetValue(cell + new Vector2Int(1, 1), out int d1) && d1 == typeId) return true;
        if (placedType.TryGetValue(cell + new Vector2Int(1, -1), out int d2) && d2 == typeId) return true;
        if (placedType.TryGetValue(cell + new Vector2Int(-1, 1), out int d3) && d3 == typeId) return true;
        if (placedType.TryGetValue(cell + new Vector2Int(-1, -1), out int d4) && d4 == typeId) return true;

        return false;
    }

    private static bool IsFarEnoughFromAll(Vector2Int cell, Dictionary<Vector2Int, int> placedType, int minSepCells)
    {
        if (placedType == null || placedType.Count == 0) return true;

        float minSep = Mathf.Max(0, minSepCells);
        foreach (var kv in placedType)
        {
            float d = Vector2.Distance(cell, kv.Key);
            if (d < minSep) return false;
        }
        return true;
    }

    // No-Path rule: BFS where other resources are walls; if same-type reachable => violation.
    private static bool ExistsPathToSameTypeWithoutOtherResources(
        Vector2Int startCell,
        Dictionary<Vector2Int, int> placedType,
        int typeId,
        int w,
        int h,
        bool[,] grid)
    {
        if (placedType == null || placedType.Count == 0) return false;

        bool hasSame = false;
        foreach (var kv in placedType) { if (kv.Value == typeId) { hasSame = true; break; } }
        if (!hasSame) return false;

        Queue<Vector2Int> q = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        q.Enqueue(startCell);
        visited.Add(startCell);

        Vector2Int[] dirs =
        {
            new Vector2Int(1,0),
            new Vector2Int(-1,0),
            new Vector2Int(0,1),
            new Vector2Int(0,-1),
        };

        while (q.Count > 0)
        {
            var cur = q.Dequeue();

            foreach (var d in dirs)
            {
                var nxt = cur + d;
                if (!Inside(nxt.x, nxt.y, w, h)) continue;
                if (visited.Contains(nxt)) continue;
                if (grid[nxt.x, nxt.y]) continue;

                if (placedType.TryGetValue(nxt, out int t))
                {
                    if (t == typeId) return true;
                    continue; // other resource blocks
                }

                visited.Add(nxt);
                q.Enqueue(nxt);
            }
        }
        return false;
    }

    // ============================================================
    // Wall safety / world
    // ============================================================
    private static bool IsCellSafeFromWalls(bool[,] grid, Vector2Int cell, int maxNeighborWallsAllowed)
    {
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        int walls = 0;
        Vector2Int[] dirs =
        {
            new Vector2Int(1,0),
            new Vector2Int(-1,0),
            new Vector2Int(0,1),
            new Vector2Int(0,-1),
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2Int n = cell + dirs[i];
            if (!Inside(n.x, n.y, w, h)) { walls++; continue; }
            if (grid[n.x, n.y]) walls++;
        }

        return walls <= maxNeighborWallsAllowed;
    }

    private static Vector3 CellCenterWorld(Vector2Int cell, float cellSize, Transform parent, float yOffset)
    {
        Vector3 local = new Vector3((cell.x + 0.5f) * cellSize, yOffset, (cell.y + 0.5f) * cellSize);
        return parent.TransformPoint(local);
    }

    private static bool Inside(int x, int y, int w, int h) => x >= 0 && y >= 0 && x < w && y < h;

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
