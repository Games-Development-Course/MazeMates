using UnityEngine;

public static class GridCellAssignment
{
    // ------------- Bounds של המודל (לא collider) -------------
    public static bool TryGetModelWorldBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        if (go == null) return false;

        // כולל Skinned + Mesh, כולל ילדים לא פעילים
        var renderers = go.GetComponentsInChildren<Renderer>(true);

        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            // חשוב: לא דורשים enabled כדי שלא יכשל על שרת/skin
            var b = r.bounds;

            // bounds "ריקים" לפעמים
            if (b.size.sqrMagnitude < 0.000001f) continue;

            if (!found) { bounds = b; found = true; }
            else bounds.Encapsulate(b);
        }

        return found;
    }

    // ------------- חפיפה 2D ב-XZ בין Bounds לתא -------------
    private static float OverlapAreaXZ(Bounds a, Bounds b)
    {
        float axMin = a.min.x, axMax = a.max.x;
        float azMin = a.min.z, azMax = a.max.z;

        float bxMin = b.min.x, bxMax = b.max.x;
        float bzMin = b.min.z, bzMax = b.max.z;

        float ox = Mathf.Max(0f, Mathf.Min(axMax, bxMax) - Mathf.Max(axMin, bxMin));
        float oz = Mathf.Max(0f, Mathf.Min(azMax, bzMax) - Mathf.Max(azMin, bzMin));

        return ox * oz;
    }

    // ------------- השיוך המדויק: תא פתוח עם הכי הרבה חפיפה -------------
    public static bool TryGetBestOpenCellByModelOverlap(MazeGenerator3D maze, GameObject go, out Vector2Int bestCell)
    {
        bestCell = default;
        if (maze == null || go == null) return false;

        if (!TryGetModelWorldBounds(go, out var modelB))
            return false;

        // בודקים תאים שהמודל מכסה (ב-XZ)
        // נהפוך את פינות ה-bounds לתאים
        Vector2Int cMin = maze.WorldToCellPublic(modelB.min);
        Vector2Int cMax = maze.WorldToCellPublic(modelB.max);

        float bestScore = 0f;
        bool found = false;

        for (int x = cMin.x; x <= cMax.x; x++)
        {
            for (int y = cMin.y; y <= cMax.y; y++)
            {
                var c = new Vector2Int(x, y);
                if (!maze.IsCellOpen(c)) continue;

                // תא בעולם (עבה ב-Y כדי לא לפספס בגלל גובה)
                Bounds cellB = maze.GetCellWorldBounds(c, yCenter: modelB.center.y, ySize: modelB.size.y + 5f);

                float area = OverlapAreaXZ(modelB, cellB);
                if (area <= 0f) continue;

                if (!found || area > bestScore)
                {
                    bestScore = area;
                    bestCell = c;
                    found = true;
                }
            }
        }

        return found;
    }

    public static bool TryGetModelCenterXZ(GameObject go, out Vector3 center)
    {
        center = default;
        if (go == null) return false;

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds b = default;

        foreach (var r in renderers)
        {
            if (r == null) continue;

            // Renderer.bounds עובד גם על SkinnedMeshRenderer
            var rb = r.bounds;
            if (rb.size.sqrMagnitude < 0.000001f) continue;

            if (!found) { b = rb; found = true; }
            else b.Encapsulate(rb);
        }

        if (!found) return false;

        center = b.center; // מרכז המודל בעולם
        return true;
    }

    // ✅ דטרמיניסטי: בוחר תא פתוח הכי קרוב למרכז המודל (ב-XZ), בלי Colliders
    public static bool TryGetBestOpenCellByModelCenter(
        MazeGenerator3D maze,
        GameObject go,
        int searchRadius,
        out Vector2Int bestCell)
    {
        bestCell = default;
        if (maze == null || go == null) return false;

        if (!TryGetModelCenterXZ(go, out var cWorld))
            return false;

        Vector2Int c = maze.WorldToCellPublic(cWorld);

        if (maze.IsCellOpen(c))
        {
            bestCell = c;
            return true;
        }

        float best = float.PositiveInfinity;
        bool found = false;
        float cs = maze.CellSize;

        for (int dx = -searchRadius; dx <= searchRadius; dx++)
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                var cc = new Vector2Int(c.x + dx, c.y + dy);
                if (!maze.IsCellOpen(cc)) continue;

                // מרכז תא בעולם (רק XZ מעניין אותנו)
                Vector3 cellCenter = maze.transform.TransformPoint(
                    new Vector3((cc.x + 0.5f) * cs, cWorld.y, (cc.y + 0.5f) * cs)
                );

                float d = (new Vector2(cellCenter.x, cellCenter.z) - new Vector2(cWorld.x, cWorld.z)).sqrMagnitude;

                if (d < best)
                {
                    best = d;
                    bestCell = cc;
                    found = true;
                }
            }

        return found;
    }

}
