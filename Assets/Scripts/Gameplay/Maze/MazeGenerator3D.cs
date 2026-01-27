// =========================
// File: Assets/Scripts/Maze/MazeGenerator3D.cs
// (Tutorial: fixed 21x21 T-shape + deterministic doors/resources + fixed exit)
// =========================
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MazeGenerator3D : MonoBehaviour
{
    [Header("Navigator Exit Anchor (in GameScene)")]
    [SerializeField] private Transform navigatorEntranceAnchor;

    [Header("Maze Settings (fallback if no GameConfigNet)")]
    [SerializeField] private int width = 21;
    [SerializeField] private int height = 21;
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private float doorPrefabYawOffset = 0f;

    [Header("Layers")]
    [SerializeField] private int wallsLayer = 12;
    [SerializeField] private int doorsLayer = 14;

    [Header("Traveller Spawn")]
    [SerializeField] private Transform travellerSpawn;

    public Transform TravellerSpawn => travellerSpawn;
    public Vector2Int StartCellPublic => startCell;

    public bool IsReady { get; private set; }
    public event System.Action Ready;
    public float MazeWorldWidth => width * cellSize;
    public float MazeWorldHeight => height * cellSize;

    public bool IsCellOpen(Vector2Int c) => InBounds(c) && grid != null && !grid[c.x, c.y];

    public Dictionary<Vector2Int, int> GetDistancesFromWorld(Vector3 worldPos)
    {
        Vector2Int start = WorldToCell(worldPos);
        return BFS_Distances(start);
    }

    public Vector2Int WorldToCellPublic(Vector3 worldPos) => WorldToCell(worldPos);
    public float CellSize => cellSize;

    [Header("Ground")]
    [SerializeField] private GameObject groundPrefab;

    [Header("Prefabs")]
    [SerializeField] private GameObject wallPrefab;

    [Header("Doors (Prefabs)")]
    [SerializeField] private GameObject normalDoorPrefab;
    [SerializeField] private GameObject puzzleDoorPrefab;
    [SerializeField] private GameObject winDoorPrefab;
    [SerializeField] private float doorYawOffset = 0f;

    [Header("Puzzles (ScriptableObjects)")]
    [SerializeField] private Puzzle puzzleEasySO;
    [SerializeField] private Puzzle puzzleMediumSO;
    [SerializeField] private Puzzle puzzleHardSO;

    [Header("Resources (Prefabs)")]
    [SerializeField] private GameObject heartPrefab;
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private GameObject keyPrefab;

    private Transform wallsRoot;
    private Transform doorsRoot;
    private Transform resourcesRoot;

    private readonly List<GameObject> spawnedDoors = new();
    private GameObject spawnedWinDoor;

    private bool[,] grid;
    private readonly List<Vector2Int> pathCells = new();
    private readonly List<Vector2Int> carvedWalls = new();

    private Vector2Int forcedExitCell;
    private Vector2Int forcedExitWallCell;
    private Vector3 forcedExitDoorLocalPos;
    private Quaternion forcedExitDoorRot;

    private int normalDoorsAmount = 3;
    private int puzzleDoorsAmount = 2;

    private int heartsAmount = 3;
    private int bombsAmount = 2;
    private int keysAmount = 2;

    private Difficulty difficulty = Difficulty.Easy;
    private int seed = 0;

    private bool tutorialMode;

    // ✅ was const; now override in tutorial mode for a clean T stem
    private Vector2Int startCell = new Vector2Int(1, 1);

    // tutorial layout constants
    private static readonly Vector2Int TutorialJunction = new Vector2Int(10, 10);
    private static readonly Vector2Int TutorialStemStart = new Vector2Int(10, 1);   // start cell
    private static readonly Vector2Int TutorialLeftEnd = new Vector2Int(1, 10);     // opposite side (key)
    private static readonly Vector2Int TutorialRightEnd = new Vector2Int(19, 10);   // exit side (bomb on path)
    private static readonly Vector2Int TutorialBombCell = new Vector2Int(15, 10);
    private static readonly Vector2Int TutorialKeyCell = new Vector2Int(1, 10);

    private bool forcedExitPrecomputed;

    // =========================
    //   GRID <-> WORLD MAPPING
    // =========================
    private Vector3 CellCenterLocal(int x, int y, float localY = 0f)
    {
        return new Vector3((x + 0.5f) * cellSize, localY, (y + 0.5f) * cellSize);
    }

    private Vector3 CellCenterWorld(int x, int y, float localY = 0f)
    {
        return transform.TransformPoint(CellCenterLocal(x, y, localY));
    }

    private Vector2Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        int cx = Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize), 0, width - 1);
        int cy = Mathf.Clamp(Mathf.FloorToInt(local.z / cellSize), 0, height - 1);
        return new Vector2Int(cx, cy);
    }

    private void Start()
    {
        PullConfigIfExists();

        if (!tutorialMode)
        {
            puzzleDoorsAmount = 1;
            Random.InitState(seed);
        }

        CreateHierarchyFolders();

        if (tutorialMode)
        {
            BuildFixedTutorialMaze_TShape_21x21();
        }
        else
        {
            GenerateMaze();
        }

        // ensure start open
        grid[startCell.x, startCell.y] = false;
        if (!pathCells.Contains(startCell)) pathCells.Add(startCell);

        if (!forcedExitPrecomputed)
        {
            ComputeForcedExitCells_FarthestBorderAdjacent();
            OpenForcedExit();
        }

        BuildMaze();
        CreateGround();

        AlignMazeToNavigatorEntrance();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            UpdateTravellerSpawn();

            if (tutorialMode)
            {
                SpawnTutorialJunctionDoors();
                SpawnTutorialResources();
            }
            else
            {
                List<GameObject> puzzleDoorInstances = PlaceDoors();
                PlaceResources();
                StartCoroutine(ComputeBombRemovalsAfterResources());
                AssignPuzzlesToPuzzleDoors(puzzleDoorInstances);
            }
        }

        MarkReady();
    }

    private void MarkReady()
    {
        if (IsReady) return;
        IsReady = true;
        Ready?.Invoke();
    }

    private IEnumerator ComputeBombRemovalsAfterResources()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        ComputeAndApplyBombRemovalsRuntime();
    }

    private void PullConfigIfExists()
    {
        var cfg = GameConfigNet.Instance;
        if (cfg == null) return;

        tutorialMode = cfg.IsTutorial.Value;

        width = cfg.MazeWidth.Value;
        height = cfg.MazeHeight.Value;

        heartsAmount = cfg.Hearts.Value;
        bombsAmount = cfg.Bombs.Value;
        keysAmount = cfg.Keys.Value;

        normalDoorsAmount = cfg.NormalDoors.Value;
        puzzleDoorsAmount = cfg.PuzzleDoors.Value;

        int d = cfg.Difficulty.Value;
        difficulty = (d == 0) ? Difficulty.Easy : (d == 1 ? Difficulty.Medium : Difficulty.Hard);

        seed = cfg.Seed.Value;
        if (seed == 0) seed = 1234567;

        if (tutorialMode)
        {
            width = 21;
            height = 21;

            heartsAmount = 0;
            bombsAmount = 1;
            keysAmount = 1;

            normalDoorsAmount = 3;
            puzzleDoorsAmount = 0;

            startCell = TutorialStemStart;
        }
    }

    // ================================================================
    //   CREATE FOLDER STRUCTURE
    // ================================================================
    private void CreateHierarchyFolders()
    {
        wallsRoot = EnsureFolder("Walls");
        doorsRoot = EnsureFolder("Doors");
        resourcesRoot = EnsureFolder("Resources");
    }

    private Transform EnsureFolder(string name)
    {
        var t = transform.Find(name);
        if (t != null) return t;
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private void CreateGround()
    {
        if (groundPrefab == null) return;

        var existing = transform.Find("Ground");
        if (existing != null) Destroy(existing.gameObject);

        GameObject ground = Instantiate(groundPrefab);
        ground.name = "Ground";
        ground.transform.SetParent(transform, false);

        float groundWidth = width * cellSize;
        float groundHeight = height * cellSize;

        ground.transform.localPosition = new Vector3(groundWidth / 2f, 0f, groundHeight / 2f);
        ground.transform.localScale = new Vector3(groundWidth, groundHeight, 1f);
    }

    // ================================================================
    //   TUTORIAL: FIXED 21x21 T-SHAPE + FIXED EXIT
    // ================================================================
    private void BuildFixedTutorialMaze_TShape_21x21()
    {
        width = 21;
        height = 21;

        forcedExitPrecomputed = true;

        pathCells.Clear();
        carvedWalls.Clear();

        grid = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = true; // wall

        // Stem: (10,1) -> (10,10)
        OpenLine(TutorialStemStart, TutorialJunction);

        // Arms: (1,10) -> (19,10)
        OpenLine(TutorialLeftEnd, TutorialRightEnd);

        // ensure the junction is included (in case line logic changes)
        OpenCell(TutorialJunction);

        // Fixed exit on the EAST border, outside of (19,10)
        forcedExitCell = TutorialRightEnd;           // (19,10) border-adjacent
        forcedExitWallCell = new Vector2Int(20, 10); // border wall cell to remove
        forcedExitDoorRot = Quaternion.LookRotation(Vector3.right);
        forcedExitDoorLocalPos = CellCenterLocal(forcedExitWallCell.x, forcedExitWallCell.y, 0f)
                               + new Vector3(cellSize * 0.5f, 0f, 0f);

        // open exit cell (already open via arms, but keep deterministic)
        grid[forcedExitCell.x, forcedExitCell.y] = false;
        if (!pathCells.Contains(forcedExitCell)) pathCells.Add(forcedExitCell);

        Debug.Log("[TutorialMaze] Built fixed 21x21 T-shape with fixed east exit.");
    }

    private void OpenLine(Vector2Int from, Vector2Int to)
    {
        if (from.x == to.x)
        {
            int x = from.x;
            int y0 = Mathf.Min(from.y, to.y);
            int y1 = Mathf.Max(from.y, to.y);
            for (int y = y0; y <= y1; y++) OpenCell(new Vector2Int(x, y));
            return;
        }

        if (from.y == to.y)
        {
            int y = from.y;
            int x0 = Mathf.Min(from.x, to.x);
            int x1 = Mathf.Max(from.x, to.x);
            for (int x = x0; x <= x1; x++) OpenCell(new Vector2Int(x, y));
            return;
        }

        // not axis-aligned: do L with x then y
        OpenLine(from, new Vector2Int(to.x, from.y));
        OpenLine(new Vector2Int(to.x, from.y), to);
    }

    private void OpenCell(Vector2Int c)
    {
        if (!InBounds(c)) return;
        grid[c.x, c.y] = false;
        if (!pathCells.Contains(c))
            pathCells.Add(c);
    }

    private void SpawnTutorialJunctionDoors()
    {
        if (normalDoorPrefab == null) return;

        // Doors at each branch out of junction:
        // Left: between (10,10) and (9,10)
        SpawnDoorBetweenCells(normalDoorPrefab, TutorialJunction, new Vector2Int(7, 10), "TutorialDoor_Left");

        // Right: between (10,10) and (11,10)
        SpawnDoorBetweenCells(normalDoorPrefab, TutorialJunction, new Vector2Int(14, 10), "TutorialDoor_Right");

        // Stem (down): between (10,10) and (10,9)
        //SpawnDoorBetweenCells(normalDoorPrefab, TutorialJunction, new Vector2Int(10, 9), "TutorialDoor_Stem");
    }

    // private void SpawnTutorialResources()
    // {
    //     // Bomb on the path to exit (right arm)
    //     if (bombPrefab != null)
    //         SpawnNetPrefabAtCell(bombPrefab, TutorialBombCell, "TutorialBomb", yOffset: 1f);

    //     // Key on opposite side (left end)
    //     if (keyPrefab != null)
    //         SpawnNetPrefabAtCell(keyPrefab, TutorialKeyCell, "TutorialKey", yOffset: 1f);
    // }

    private void SpawnDoorBetweenCells(GameObject prefab, Vector2Int a, Vector2Int b, string name)
    {
        if (prefab == null) return;
        if (!InBounds(a) || !InBounds(b)) return;
        if (grid[a.x, a.y] || grid[b.x, b.y]) return; // both must be open

        Vector3 worldA = CellCenterWorld(a.x, a.y, 0f);
        Vector3 worldB = CellCenterWorld(b.x, b.y, 0f);
        Vector3 pos = (worldA + worldB) * 0.5f;

        Vector3 dir = (worldB - worldA);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        Quaternion rot =
            Quaternion.LookRotation(dir.normalized, Vector3.up) *
            Quaternion.Euler(0f, doorPrefabYawOffset, 0f);

        GameObject go = Instantiate(prefab, pos, rot);
        go.name = name;
        go.layer = doorsLayer;

        var netObj = go.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn(true);

        spawnedDoors.Add(go);
    }

    // private void SpawnNetPrefabAtCell(GameObject prefab, Vector2Int cell, string name, float yOffset)
    // {
    //     if (prefab == null) return;
    //     if (!InBounds(cell)) return;
    //     if (grid[cell.x, cell.y]) return;

    //     Vector3 pos = CellCenterWorld(cell.x, cell.y, yOffset);
    //     var go = Instantiate(prefab, pos, Quaternion.identity);
    //     go.name = name;

    //     var netObj = go.GetComponent<NetworkObject>();
    //     if (netObj != null)
    //         netObj.Spawn(true);
    // }

    // ================================================================
    //   MAZE GENERATION (DFS CARVE) - PERFECT MAZE
    // ================================================================
    private void GenerateMaze()
    {
        if (width % 2 == 0) width++;
        if (height % 2 == 0) height++;

        pathCells.Clear();
        carvedWalls.Clear();

        grid = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = true;

        DFS(startCell.x, startCell.y);
    }

    private void DFS(int x, int y)
    {
        grid[x, y] = false;
        pathCells.Add(new Vector2Int(x, y));

        List<Vector2Int> dirs = new()
        {
            new Vector2Int(2, 0),
            new Vector2Int(-2, 0),
            new Vector2Int(0, 2),
            new Vector2Int(0, -2),
        };

        Shuffle(dirs);

        foreach (var d in dirs)
        {
            int nx = x + d.x;
            int ny = y + d.y;

            if (IsInside(nx, ny) && grid[nx, ny])
            {
                int wallX = x + d.x / 2;
                int wallY = y + d.y / 2;

                grid[wallX, wallY] = false;
                var wallPos = new Vector2Int(wallX, wallY);

                pathCells.Add(wallPos);
                carvedWalls.Add(wallPos);

                DFS(nx, ny);
            }
        }
    }

    private bool IsInside(int x, int y) => x > 0 && y > 0 && x < width - 1 && y < height - 1;

    private void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    // ================================================================
    //   EXIT (fallback) farthest reachable border-adjacent from start
    // ================================================================
    private void ComputeForcedExitCells_FarthestBorderAdjacent()
    {
        List<Vector2Int> candidates = new();

        for (int x = 1; x < width - 1; x++)
        {
            if (!grid[x, 1]) candidates.Add(new Vector2Int(x, 1));
            if (!grid[x, height - 2]) candidates.Add(new Vector2Int(x, height - 2));
        }

        for (int y = 1; y < height - 1; y++)
        {
            if (!grid[1, y]) candidates.Add(new Vector2Int(1, y));
            if (!grid[width - 2, y]) candidates.Add(new Vector2Int(width - 2, y));
        }

        Dictionary<Vector2Int, int> dist = BFS_Distances(startCell);

        Vector2Int best = new Vector2Int(width - 2, height - 2);
        int bestD = -1;

        foreach (var c in candidates)
        {
            if (!dist.TryGetValue(c, out int d)) continue;
            if (d > bestD)
            {
                bestD = d;
                best = c;
            }
        }

        forcedExitCell = best;

        Vector3 borderCenterLocal;
        Vector3 outward;

        if (forcedExitCell.y == height - 2)
        {
            forcedExitWallCell = new Vector2Int(forcedExitCell.x, height - 1);
            forcedExitDoorRot = Quaternion.LookRotation(Vector3.forward);
            borderCenterLocal = CellCenterLocal(forcedExitWallCell.x, forcedExitWallCell.y, 0f);
            outward = new Vector3(0f, 0f, cellSize * 0.5f);
        }
        else if (forcedExitCell.y == 1)
        {
            forcedExitWallCell = new Vector2Int(forcedExitCell.x, 0);
            forcedExitDoorRot = Quaternion.LookRotation(Vector3.back);
            borderCenterLocal = CellCenterLocal(forcedExitWallCell.x, forcedExitWallCell.y, 0f);
            outward = new Vector3(0f, 0f, -cellSize * 0.5f);
        }
        else if (forcedExitCell.x == 1)
        {
            forcedExitWallCell = new Vector2Int(0, forcedExitCell.y);
            forcedExitDoorRot = Quaternion.LookRotation(Vector3.left);
            borderCenterLocal = CellCenterLocal(forcedExitWallCell.x, forcedExitWallCell.y, 0f);
            outward = new Vector3(-cellSize * 0.5f, 0f, 0f);
        }
        else
        {
            forcedExitWallCell = new Vector2Int(width - 1, forcedExitCell.y);
            forcedExitDoorRot = Quaternion.LookRotation(Vector3.right);
            borderCenterLocal = CellCenterLocal(forcedExitWallCell.x, forcedExitWallCell.y, 0f);
            outward = new Vector3(cellSize * 0.5f, 0f, 0f);
        }

        forcedExitDoorLocalPos = borderCenterLocal + outward;
    }

    private Dictionary<Vector2Int, int> BFS_Distances(Vector2Int start)
    {
        Dictionary<Vector2Int, int> dist = new();
        if (!InBounds(start) || grid[start.x, start.y]) return dist;

        Queue<Vector2Int> q = new();
        q.Enqueue(start);
        dist[start] = 0;

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
            int cd = dist[cur];

            foreach (var d in dirs)
            {
                var nxt = cur + d;
                if (!InBounds(nxt)) continue;
                if (grid[nxt.x, nxt.y]) continue;
                if (dist.ContainsKey(nxt)) continue;

                dist[nxt] = cd + 1;
                q.Enqueue(nxt);
            }
        }

        return dist;
    }

    private void OpenForcedExit()
    {
        grid[forcedExitCell.x, forcedExitCell.y] = false;
        if (!pathCells.Contains(forcedExitCell))
            pathCells.Add(forcedExitCell);
    }

    // ================================================================
    //   BUILD WALLS
    // ================================================================
// File: Assets/Scripts/Maze/MazeGenerator3D.cs
// Replace/adjust your tutorial door spawn with the code below.

private void SpawnTutorialDoors_TwoOnly()
{
    if (normalDoorPrefab == null) return;

    // Door at x=7 on the horizontal arm (blocks passage). Place BETWEEN (7,10) and (8,10).
    SpawnDoorBetweenCells_Tutorial(normalDoorPrefab,
        a: new Vector2Int(7, 10),
        b: new Vector2Int(8, 10),
        name: "TutorialDoor_Left_7_10");

    // Door at x=14 on the horizontal arm. Place BETWEEN (14,10) and (13,10).
    SpawnDoorBetweenCells_Tutorial(normalDoorPrefab,
        a: new Vector2Int(14, 10),
        b: new Vector2Int(13, 10),
        name: "TutorialDoor_Right_14_10");
}

private void SpawnDoorBetweenCells_Tutorial(GameObject prefab, Vector2Int a, Vector2Int b, string name)
{
    if (prefab == null) return;
    if (!InBounds(a) || !InBounds(b)) return;

    // Both cells must be open in your T path
    if (grid[a.x, a.y] || grid[b.x, b.y])
    {
        Debug.LogWarning($"[TutorialDoor] Not spawning {name}: a/b not open. a={a} open={!grid[a.x,a.y]} b={b} open={!grid[b.x,b.y]}");
        return;
    }

    Vector3 worldA = CellCenterWorld(a.x, a.y, 0f);
    Vector3 worldB = CellCenterWorld(b.x, b.y, 0f);
    Vector3 pos = (worldA + worldB) * 0.5f;

    Vector3 dir = (worldB - worldA);
    dir.y = 0f;
    if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

    Quaternion rot =
        Quaternion.LookRotation(dir.normalized, Vector3.up) *
        Quaternion.Euler(0f, doorPrefabYawOffset, 0f);

    GameObject go = Instantiate(prefab, pos, rot);
    go.name = name;

    // ✅ critical: set layers on ALL children (colliders often live on children)
    SetLayerRecursive(go, doorsLayer);

    // Optional but nice for hierarchy
    if (doorsRoot != null)
        go.transform.SetParent(doorsRoot, true);

    var netObj = go.GetComponent<NetworkObject>();
    if (netObj != null)
        netObj.Spawn(true);

    spawnedDoors.Add(go);

    Debug.Log($"[TutorialDoor] Spawned {name} at {pos}, rootLayer={go.layer}");
}

private void SetLayerRecursive(GameObject go, int layer)
{
    if (go == null) return;
    go.layer = layer;
    foreach (Transform child in go.transform)
        SetLayerRecursive(child.gameObject, layer);
}

    private void BuildMaze()
    {
        foreach (Transform c in wallsRoot) Destroy(c.gameObject);
        foreach (Transform c in resourcesRoot) Destroy(c.gameObject);

        if (spawnedWinDoor != null)
        {
            Destroy(spawnedWinDoor);
            spawnedWinDoor = null;
        }

        for (int i = spawnedDoors.Count - 1; i >= 0; i--)
        {
            if (spawnedDoors[i] != null)
                Destroy(spawnedDoors[i]);
        }
        spawnedDoors.Clear();

        foreach (Transform c in doorsRoot) Destroy(c.gameObject);

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (!grid[x, y]) continue;

            if (x == forcedExitWallCell.x && y == forcedExitWallCell.y)
                continue;

            Vector3 worldPos = CellCenterWorld(x, y, 1.75f);
            GameObject wall = Instantiate(wallPrefab, worldPos, Quaternion.identity, wallsRoot);

            SetLayerRecursive(wall, wallsLayer);
            var s = wall.transform.localScale;
            wall.transform.localScale = new Vector3(cellSize, s.y, cellSize);
        }
    }
// File: Assets/Scripts/Maze/MazeGenerator3D.cs

private void SpawnTutorialResources()
{
    // Bomb on the path to exit (right arm)
    if (bombPrefab != null)
        SpawnNetPrefabAtCell(
            prefab: bombPrefab,
            cell: TutorialBombCell,
            // IMPORTANT: keep canonical name if your systems depend on it
            forcedName: "Bomb",
            yOffset: 1f
        );

    // Key on opposite side (left end)
    if (keyPrefab != null)
        SpawnNetPrefabAtCell(
            prefab: keyPrefab,
            cell: TutorialKeyCell,
            // IMPORTANT: AllKeysCollected often depends on finding/collecting "Key"
            forcedName: "Key",
            yOffset: 1f
        );
}

// ✅ Replace your existing SpawnNetPrefabAtCell with this version.
// Key point: DON'T invent names like "TutorialKey" unless you're 100% sure no logic depends on it.
private void SpawnNetPrefabAtCell(GameObject prefab, Vector2Int cell, string forcedName, float yOffset)
{
    if (prefab == null) return;
    if (!InBounds(cell)) return;
    if (grid[cell.x, cell.y]) return; // must be open

    Vector3 pos = CellCenterWorld(cell.x, cell.y, yOffset);

    var go = Instantiate(prefab, pos, Quaternion.identity);

    // If you pass forcedName, use it; otherwise keep prefab's name.
    if (!string.IsNullOrWhiteSpace(forcedName))
        go.name = forcedName;
    else
        go.name = prefab.name;

    var netObj = go.GetComponent<NetworkObject>();
    if (netObj != null)
        netObj.Spawn(true);
}

    // private void SetLayerRecursive(GameObject go, int layer)
    // {
    //     if (go == null) return;
    //     go.layer = layer;
    //     foreach (Transform child in go.transform)
    //         SetLayerRecursive(child.gameObject, layer);
    // }

    // ================================================================
    //   ALIGNMENT + WIN DOOR
    // ================================================================
    private void AlignMazeToNavigatorEntrance()
    {
        if (navigatorEntranceAnchor == null)
        {
            SpawnVictoryDoorLocal();
            return;
        }

        Vector3 doorWorldBefore = transform.TransformPoint(forcedExitDoorLocalPos);

        Vector3 exitForwardWorld = transform.TransformDirection(forcedExitDoorRot * Vector3.forward);
        Vector3 targetForwardWorld = navigatorEntranceAnchor.forward;

        exitForwardWorld.y = 0f;
        targetForwardWorld.y = 0f;

        Vector3 transpose = new Vector3(cellSize * 0.25f, 0.5f, cellSize * 0.25f);

        if (exitForwardWorld.sqrMagnitude < 0.0001f || targetForwardWorld.sqrMagnitude < 0.0001f)
        {
            Vector3 targetPos = navigatorEntranceAnchor.position;
            Vector3 currentPos = transform.TransformPoint(forcedExitDoorLocalPos);
            Vector3 delta = targetPos - currentPos;

            transform.position += delta + transpose;
            SpawnVictoryDoorLocal();
            return;
        }

        exitForwardWorld.Normalize();
        targetForwardWorld.Normalize();

        Quaternion rotDelta = Quaternion.FromToRotation(exitForwardWorld, targetForwardWorld);
        transform.RotateAround(doorWorldBefore, Vector3.up, rotDelta.eulerAngles.y);

        Vector3 doorWorldAfter = transform.TransformPoint(forcedExitDoorLocalPos);
        Vector3 targetWorld = navigatorEntranceAnchor.position;
        Vector3 deltaPos = targetWorld - doorWorldAfter;

        transform.position += deltaPos + transpose;
        transform.position += Vector3.down * 0.5f;

        SpawnVictoryDoorLocal();
    }

    private void SpawnVictoryDoorLocal()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;
        if (winDoorPrefab == null) return;

        if (spawnedWinDoor != null)
        {
            Destroy(spawnedWinDoor);
            spawnedWinDoor = null;
        }

        Vector3 worldPos = transform.TransformPoint(forcedExitDoorLocalPos);
        Quaternion worldRot = transform.rotation * forcedExitDoorRot;

        GameObject door = Instantiate(winDoorPrefab, worldPos, worldRot);
        door.name = "WinDoor";
        SetLayerRecursive(door, doorsLayer);

        var netObj = door.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn(true);

        spawnedWinDoor = door;
    }

    // ================================================================
    //   DOORS/RESOURCES (original runtime)
    // ================================================================
    private List<GameObject> PlaceDoors()
    {
        Vector2Int forwardDir = GetStartCorridorDir();

        float[] distSteps = { 4f, 3f, 2f, 1f, 0.5f, 0f };
        float doorWidthWorld = ComputeDoorWidthWorld_FromMesh(normalDoorPrefab != null ? normalDoorPrefab : puzzleDoorPrefab);
        float doorWidthCells = doorWidthWorld / cellSize;

        var plan = DoorPlacement.PlanDoors(
            grid: grid,
            carvedWalls: carvedWalls,
            width: width,
            height: height,
            startCell: startCell,
            forcedExitCell: forcedExitCell,
            wantNormal: normalDoorsAmount,
            wantPuzzle: puzzleDoorsAmount,
            keepClearStepsForward: 3,
            startForwardDir: forwardDir,
            minDistStepsCells: distSteps,
            nearPathManhattanRadius: 1,
            doorWidthCells: doorWidthCells
        );

        List<GameObject> puzzleDoorInstances = new();

        for (int i = 0; i < plan.Puzzle.Count; i++)
        {
            var go = SpawnDoorWorld(puzzleDoorPrefab, plan.Puzzle[i]);
            if (go != null) puzzleDoorInstances.Add(go);
        }

        for (int i = 0; i < plan.Normal.Count; i++)
        {
            SpawnDoorWorld(normalDoorPrefab, plan.Normal[i]);
        }

        return puzzleDoorInstances;
    }

    private GameObject SpawnDoorWorld(GameObject prefab, DoorSpot spot)
    {
        if (prefab == null) return null;

        var nm = NetworkManager.Singleton;
        if (nm != null && !nm.IsServer)
            return null;

        Vector3 world = CellCenterWorld(spot.cell.x, spot.cell.y, 0f)
                      + transform.TransformVector(new Vector3(spot.offset.x * cellSize, 0f, spot.offset.y * cellSize));

        Quaternion rot = transform.rotation * spot.rotation * Quaternion.Euler(0f, doorPrefabYawOffset, 0f);

        var go = Instantiate(prefab, world, rot);
        go.layer = doorsLayer;

        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Destroy(go);
            return null;
        }

        netObj.Spawn(true);
        spawnedDoors.Add(go);
        return go;
    }

    private void PlaceResources()
    {
        HashSet<Vector2Int> blocked = new();

        if (spawnedWinDoor != null)
        {
            Vector2Int c = WorldToCell(spawnedWinDoor.transform.position);
            if (InBounds(c)) blocked.Add(c);
        }

        for (int i = 0; i < spawnedDoors.Count; i++)
        {
            var d = spawnedDoors[i];
            if (d == null) continue;

            Vector2Int c = WorldToCell(d.transform.position);
            if (InBounds(c)) blocked.Add(c);
        }

        Vector2Int forwardDir = GetStartCorridorDir();

        ResourcePlacement.PlaceAllResourcesEvenly(
            grid,
            pathCells,
            blocked,
            cellSize,
            resourcesRoot,
            new ResourcePlacement.ResourceRequest[]
            {
                new ResourcePlacement.ResourceRequest(heartPrefab, heartsAmount),
                new ResourcePlacement.ResourceRequest(bombPrefab, bombsAmount),
                new ResourcePlacement.ResourceRequest(keyPrefab, keysAmount),
            },
            startCell: startCell,
            keepClearStepsForward: 3,
            forwardDir: forwardDir,
            yOffset: 1f,
            minSeparationCells: 4,
            maxNeighborWallsAllowed: 2,
            wallsLayer: wallsLayer,
            collisionRadiusMultiplier: 0.22f
        );
    }

    private Vector2Int GetStartCorridorDir()
    {
        Vector2Int s = startCell;

        Vector2Int[] dirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        List<Vector2Int> openDirs = new();

        foreach (var d in dirs)
        {
            Vector2Int n = s + d;
            if (!InBounds(n)) continue;
            if (grid[n.x, n.y] == false)
                openDirs.Add(d);
        }

        if (openDirs.Count == 0)
            return new Vector2Int(0, 1);

        if (openDirs.Count == 1)
            return openDirs[0];

        List<Vector2Int> solution = DoorPlacement.FindPathBFS(grid, width, height, startCell, forcedExitCell);
        if (solution.Count >= 2)
        {
            Vector2Int next = solution[1];
            Vector2Int delta = next - startCell;
            foreach (var d in openDirs)
                if (d == delta) return d;
        }

        return openDirs[0];
    }

    private void AssignPuzzlesToPuzzleDoors(List<GameObject> puzzleDoors)
    {
        if (puzzleDoors == null || puzzleDoors.Count == 0) return;

        int desired = 1;
        int count = Mathf.Min(desired, puzzleDoors.Count);

        for (int i = 0; i < count; i++)
        {
            var controller = puzzleDoors[i].GetComponent<DoorController>();
            if (controller == null) continue;

            Puzzle chosen = null;

            if (difficulty == Difficulty.Easy) chosen = puzzleEasySO;
            else if (difficulty == Difficulty.Medium) chosen = puzzleMediumSO;
            else chosen = puzzleHardSO;

            if (chosen == null) continue;

            controller.SetPuzzleDefinitionServer(chosen);
        }
    }

    // ================================================================
    //   TRAVELLER SPAWN
    // ================================================================
    private void UpdateTravellerSpawn()
    {
        if (travellerSpawn == null)
            return;

        if (grid[startCell.x, startCell.y])
            grid[startCell.x, startCell.y] = false;

        Vector3 spawnPos = CellCenterWorld(startCell.x, startCell.y, 0.5f);
        travellerSpawn.position = spawnPos;

        Vector2Int dir = GetClosestOpenNeighborDir(startCell);

        Vector3 lookDirWorld;

        if (dir == Vector2Int.zero)
        {
            lookDirWorld = transform.forward;
        }
        else
        {
            Vector3 from = CellCenterWorld(startCell.x, startCell.y, 0.5f);
            Vector3 to = CellCenterWorld(startCell.x + dir.x, startCell.y + dir.y, 0.5f);
            lookDirWorld = (to - from);
        }

        lookDirWorld.y = 0f;
        if (lookDirWorld.sqrMagnitude > 0.0001f)
            travellerSpawn.rotation = Quaternion.LookRotation(lookDirWorld.normalized, Vector3.up);
    }

    private Vector2Int GetClosestOpenNeighborDir(Vector2Int cell)
    {
        Vector2Int[] dirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        Vector3 center = CellCenterWorld(cell.x, cell.y, 0.5f);

        float best = float.PositiveInfinity;
        Vector2Int bestDir = Vector2Int.zero;

        foreach (var d in dirs)
        {
            Vector2Int n = cell + d;
            if (!InBounds(n)) continue;
            if (grid[n.x, n.y]) continue;

            Vector3 nCenter = CellCenterWorld(n.x, n.y, 0.5f);
            float dist = (nCenter - center).sqrMagnitude;

            if (dist < best)
            {
                best = dist;
                bestDir = d;
            }
        }

        return bestDir;
    }

    private bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;

    private float ComputeDoorWidthWorld_FromMesh(GameObject doorPrefab)
    {
        if (doorPrefab == null) return 0.4f;

        var renderers = doorPrefab.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return 0.4f;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        float w = Mathf.Min(b.size.x, b.size.z);
        return Mathf.Max(0.01f, w);
    }

    // ================================================================
    //   Bomb removals (kept as-is)
    // ================================================================
    private void ComputeAndApplyBombRemovalsRuntime()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        var cfg = GameConfigNet.Instance;
        if (cfg == null) return;

        HashSet<Vector2Int> bombCells = CollectPlacedBombCells_SceneWide();
        int totalBombs = bombCells.Count;

        List<Vector2Int> exitPath = DoorPlacement.FindPathBFS(grid, width, height, startCell, forcedExitCell);

        int bombsOnExitPath = 0;
        for (int i = 0; i < exitPath.Count; i++)
        {
            if (bombCells.Contains(exitPath[i]))
                bombsOnExitPath++;
        }

        int easy = totalBombs;
        int hard = bombsOnExitPath;
        int medium = Mathf.RoundToInt((easy + hard) * 0.5f);

        int diff = cfg.Difficulty.Value;
        int result = (diff == 0) ? easy : (diff == 1 ? medium : hard);

        result = Mathf.Clamp(result, 0, totalBombs);

        cfg.SetBombRemovalsRuntimeServerRpc(result);
    }

    private HashSet<Vector2Int> CollectPlacedBombCells_SceneWide()
    {
        HashSet<Vector2Int> cells = new();

        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            var root = roots[r];
            if (root == null) continue;

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null) continue;

                string n = t.gameObject.name;
                if (n != "Bomb" && n != "Bomb(Clone)") continue;

                Vector2Int c = WorldToCell(t.position);
                if (InBounds(c) && !grid[c.x, c.y])
                    cells.Add(c);
            }
        }

        return cells;
    }
    /// <summary>
/// Snap a world position to the nearest open (walkable) cell within a Manhattan radius.
/// Returns false if no open cell found.
/// </summary>
public bool TrySnapToNearestOpenCell(Vector3 worldPos, int radius, out Vector2Int snapped)
{
    Vector2Int start = WorldToCell(worldPos);

    snapped = start;
    if (IsCellOpen(start))
        return true;

    int bestDist = int.MaxValue;
    bool found = false;

    for (int dx = -radius; dx <= radius; dx++)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            Vector2Int c = new Vector2Int(start.x + dx, start.y + dy);
            if (!IsCellOpen(c)) continue;

            int md = Mathf.Abs(dx) + Mathf.Abs(dy);
            if (md < bestDist)
            {
                bestDist = md;
                snapped = c;
                found = true;
            }
        }
    }

    return found;
}

/// <summary>
/// BFS distances from a cell over open cells.
/// </summary>
public Dictionary<Vector2Int, int> GetDistancesFromCell(Vector2Int startCell)
{
    return BFS_Distances(startCell);
}

/// <summary>
/// World-space bounds for a given cell (useful for overlap tests / gizmos).
/// </summary>
public Bounds GetCellWorldBounds(Vector2Int c, float yCenter = 0f, float ySize = 10f)
{
    float cs = cellSize;

    Vector3 center = transform.TransformPoint(
        new Vector3((c.x + 0.5f) * cs, yCenter, (c.y + 0.5f) * cs)
    );

    Vector3 size = new Vector3(cs, ySize, cs);
    return new Bounds(center, size);
}

}
