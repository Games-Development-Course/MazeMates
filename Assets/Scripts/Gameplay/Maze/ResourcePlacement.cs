// Assets/Scripts/Maze/ResourcePlacement.cs
using System.Collections.Generic;
using UnityEngine;

public static class ResourcePlacement
{
    public struct ResourceRequest
    {
        public GameObject prefab;
        public int amount;

        public ResourceRequest(GameObject prefab, int amount)
        {
            this.prefab = prefab;
            this.amount = amount;
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

        blockedCells ??= new HashSet<Vector2Int>();

        // active requests
        List<ResourceRequest> active = new();
        int total = 0;
        for (int i = 0; i < requests.Length; i++)
        {
            if (requests[i].prefab == null) continue;
            if (requests[i].amount <= 0) continue;
            active.Add(requests[i]);
            total += requests[i].amount;
        }
        if (total <= 0) return;

        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        // block start cell + 3 steps forward
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
                    if (Inside(c.x, c.y, w, h))
                        blockedCells.Add(c);
                }
            }
        }

        // build valid cells (NOT filtered by separation)
        List<Vector2Int> valid = new();

        int wallMask = 1 << wallsLayer;
        float collisionRadius = Mathf.Max(0.01f, cellSize * collisionRadiusMultiplier);

        foreach (var cell in pathCells)
        {
            if (!Inside(cell.x, cell.y, w, h))
                continue;

            if (blockedCells.Contains(cell))
                continue;

            if (grid[cell.x, cell.y]) // wall
                continue;

            if (!IsCellSafeFromWalls(grid, cell, maxNeighborWallsAllowed))
                continue;

            Vector3 worldPos = CellCenterWorld(cell, cellSize, parent, yOffset);
            if (Physics.CheckSphere(worldPos, collisionRadius, wallMask, QueryTriggerInteraction.Ignore))
                continue;

            valid.Add(cell);
        }

        if (valid.Count == 0)
        {
            Debug.LogWarning("[ResourcePlacement] No valid cells at all.");
            return;
        }

        // ---- NEW: compute BFS distance map along the maze (geodesic coverage)
        Vector2Int bfsStart = startCell ?? valid[Random.Range(0, valid.Count)];
        Dictionary<Vector2Int, int> dist = BuildBfsDistanceMap(grid, bfsStart);

        // keep only reachable
        List<Vector2Int> reachable = new();
        for (int i = 0; i < valid.Count; i++)
        {
            if (dist.TryGetValue(valid[i], out int d) && d >= 0)
                reachable.Add(valid[i]);
        }
        if (reachable.Count > 0) valid = reachable;

        // pick with real separation softening
        int sep = Mathf.Max(0, minSeparationCells);
        List<Vector2Int> picked = new();

        while (sep >= 0)
        {
            picked = PickByDistanceStrata_BestCandidate(valid, dist, total, sep, blockedCells);
            if (picked.Count >= total) break;
            sep--;
        }

        // fill if still short
        if (picked.Count < total)
            FillMoreCells(valid, picked, blockedCells, total, sep);

        if (picked.Count == 0)
        {
            Debug.LogWarning("[ResourcePlacement] Nothing picked.");
            return;
        }

        if (picked.Count < total)
        {
            Debug.LogWarning(
                $"[ResourcePlacement] Not enough space. Need={total} Picked={picked.Count}. " +
                $"Try lowering minSeparationCells or increasing maxNeighborWallsAllowed."
            );
        }

        // round-robin instantiate (so Key won't starve)
        List<int> remaining = new();
        for (int i = 0; i < active.Count; i++) remaining.Add(active[i].amount);

        int idx = 0;
        while (idx < picked.Count)
        {
            bool placed = false;

            for (int r = 0; r < active.Count && idx < picked.Count; r++)
            {
                if (remaining[r] <= 0) continue;

                Vector2Int c = picked[idx++];
                Vector3 pos = CellCenterWorld(c, cellSize, parent, yOffset);
                Object.Instantiate(active[r].prefab, pos, Quaternion.identity, parent);

                blockedCells.Add(c);
                remaining[r]--;
                placed = true;
            }

            if (!placed) break;
        }
    }

    // -------------------------
    // Picking: distance strata + Mitchell best-candidate (blue-noise-ish)
    // -------------------------
    private static List<Vector2Int> PickByDistanceStrata_BestCandidate(
        List<Vector2Int> cells,
        Dictionary<Vector2Int, int> dist,
        int count,
        int minSeparationCells,
        HashSet<Vector2Int> blocked
    )
    {
        List<Vector2Int> picked = new();
        if (count <= 0 || cells == null || cells.Count == 0) return picked;

        // find max dist
        int maxD = 0;
        for (int i = 0; i < cells.Count; i++)
            if (dist.TryGetValue(cells[i], out int d)) maxD = Mathf.Max(maxD, d);

        // number of strata: enough to cover maze, not too many
        int strata = Mathf.Clamp(count, 4, 12);
        int step = Mathf.Max(1, (maxD + 1) / strata);

        // build buckets by distance
        List<Vector2Int>[] buckets = new List<Vector2Int>[strata];
        for (int i = 0; i < strata; i++) buckets[i] = new List<Vector2Int>();

        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (blocked != null && blocked.Contains(c)) continue;
            if (!dist.TryGetValue(c, out int d)) continue;

            int bi = Mathf.Clamp(d / step, 0, strata - 1);
            buckets[bi].Add(c);
        }

        // shuffle each bucket + shuffle bucket order
        for (int i = 0; i < buckets.Length; i++) Shuffle(buckets[i]);

        List<int> order = new();
        for (int i = 0; i < strata; i++) order.Add(i);
        Shuffle(order);

        // round-robin across distance buckets
        int guard = 0;
        while (picked.Count < count && guard++ < 200000)
        {
            bool progressed = false;

            for (int oi = 0; oi < order.Count && picked.Count < count; oi++)
            {
                int bi = order[oi];
                var b = buckets[bi];
                if (b.Count == 0) continue;

                // Mitchell best-candidate: sample K candidates from this bucket,
                // choose the one maximizing min-distance to picked (blue-noise style). :contentReference[oaicite:1]{index=1}
                const int K = 20;
                Vector2Int best = default;
                float bestScore = -1f;
                bool found = false;

                int attempts = Mathf.Min(K, b.Count);
                for (int t = 0; t < attempts; t++)
                {
                    var cand = b[Random.Range(0, b.Count)];

                    if (minSeparationCells > 0 && !FarFromList(cand, picked, minSeparationCells))
                        continue;

                    float score = MinDistanceToPicked(cand, picked);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = cand;
                        found = true;
                    }
                }

                if (found)
                {
                    picked.Add(best);
                    b.Remove(best);
                    progressed = true;
                }
            }

            if (!progressed) break;
        }

        return picked;
    }

    private static float MinDistanceToPicked(Vector2Int c, List<Vector2Int> picked)
    {
        if (picked.Count == 0) return 999999f;

        float best = float.PositiveInfinity;
        for (int i = 0; i < picked.Count; i++)
            best = Mathf.Min(best, Vector2.Distance(c, picked[i]));
        return best;
    }

    private static bool FarFromList(Vector2Int c, List<Vector2Int> list, int minDist)
    {
        int md2 = minDist * minDist;
        for (int i = 0; i < list.Count; i++)
        {
            int dx = c.x - list[i].x;
            int dy = c.y - list[i].y;
            if (dx * dx + dy * dy <= md2) return false;
        }
        return true;
    }

    private static void FillMoreCells(
        List<Vector2Int> valid,
        List<Vector2Int> picked,
        HashSet<Vector2Int> blocked,
        int targetCount,
        int sep
    )
    {
        Shuffle(valid);

        for (int i = 0; i < valid.Count && picked.Count < targetCount; i++)
        {
            var c = valid[i];
            if (picked.Contains(c)) continue;
            if (blocked.Contains(c)) continue;

            if (sep > 0 && !FarFromList(c, picked, sep)) continue;

            picked.Add(c);
            blocked.Add(c);
        }
    }

    // BFS distance along corridors (grid[x,y] == false means walkable)
    private static Dictionary<Vector2Int, int> BuildBfsDistanceMap(bool[,] grid, Vector2Int start)
    {
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        Dictionary<Vector2Int, int> dist = new();
        Queue<Vector2Int> q = new();

        if (!Inside(start.x, start.y, w, h) || grid[start.x, start.y])
            return dist;

        dist[start] = 0;
        q.Enqueue(start);

        Vector2Int[] dirs = {
            new Vector2Int(1,0), new Vector2Int(-1,0),
            new Vector2Int(0,1), new Vector2Int(0,-1)
        };

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int cd = dist[cur];

            for (int i = 0; i < dirs.Length; i++)
            {
                var n = cur + dirs[i];
                if (!Inside(n.x, n.y, w, h)) continue;
                if (grid[n.x, n.y]) continue; // wall
                if (dist.ContainsKey(n)) continue;

                dist[n] = cd + 1;
                q.Enqueue(n);
            }
        }

        return dist;
    }

    private static Vector3 CellCenterWorld(Vector2Int c, float cellSize, Transform parent, float yOffset)
    {
        Vector3 localPos = new Vector3(
            c.x * cellSize + cellSize * 0.5f,
            0f,
            c.y * cellSize + cellSize * 0.5f
        );

        Vector3 world = parent.TransformPoint(localPos);
        world.y += yOffset;
        return world;
    }

    // generic shuffle (works for List<int> and List<Vector2Int>)
    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    private static bool IsCellSafeFromWalls(bool[,] grid, Vector2Int c, int maxNeighborWallsAllowed)
    {
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        if (!Inside(c.x, c.y, w, h)) return false;
        if (grid[c.x, c.y]) return false;

        int wallCount = 0;
        if (Inside(c.x + 1, c.y, w, h) && grid[c.x + 1, c.y]) wallCount++;
        if (Inside(c.x - 1, c.y, w, h) && grid[c.x - 1, c.y]) wallCount++;
        if (Inside(c.x, c.y + 1, w, h) && grid[c.x, c.y + 1]) wallCount++;
        if (Inside(c.x, c.y - 1, w, h) && grid[c.x, c.y - 1]) wallCount++;

        return wallCount <= maxNeighborWallsAllowed;
    }

    private static bool Inside(int x, int y, int w, int h)
    {
        return x >= 1 && y >= 1 && x <= w - 2 && y <= h - 2;
    }
}
