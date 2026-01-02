// Assets/Scripts/Maze/MazeGenerator3D.cs
using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator3D : MonoBehaviour
{
    [Header("Navigator Exit Anchor (in GameScene)")]
    [Tooltip("גרור לפה Transform שנמצא בדיוק על הכניסה לחדר הנווט (הנקודה שבה דלת הניצחון צריכה לשבת).")]
    [SerializeField] private Transform navigatorEntranceAnchor;

    [Header("Maze Settings (fallback if no GameConfigNet)")]
    [SerializeField] private int width = 21;
    [SerializeField] private int height = 21;
    [SerializeField] private float cellSize = 2f;

    [Header("Layers")]
    [SerializeField] private int wallsLayer = 0; // 0 = Default

    [Header("Traveller Spawn")]
    [SerializeField] private Transform travellerSpawn;



    // -------------------------------
    // Public API for deterministic spawning (read-only)
    // -------------------------------
    public Transform TravellerSpawn => travellerSpawn;



    public bool IsReady { get; private set; }
    public event System.Action Ready;

    private void MarkReady()
    {
        if (IsReady) return;
        IsReady = true;
        Ready?.Invoke();
    }

    [Header("Ground")]
    [SerializeField] private GameObject groundPrefab;

    [Header("Prefabs")]
    [SerializeField] private GameObject wallPrefab;

    [Header("Doors (Prefabs)")]
    [SerializeField] private GameObject normalDoorPrefab;
    [SerializeField] private GameObject puzzleDoorPrefab;
    [SerializeField] private GameObject winDoorPrefab;

    [Header("Puzzles (Prefabs)")]
    [SerializeField] private GameObject puzzleEasyPrefab;
    [SerializeField] private GameObject puzzleMediumPrefab;
    [SerializeField] private GameObject puzzleHardPrefab; // HARD should be used twice

    [Header("Resources (Prefabs)")]
    [SerializeField] private GameObject heartPrefab;
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private GameObject keyPrefab;

    [Header("Wall Material (optional)")]
    [SerializeField] private Material wallMaterial;
    [SerializeField] private bool applyWallMaterialToChildren = true;
    [SerializeField] private bool useSharedMaterial = true;
    [SerializeField] private bool replaceAllMaterialSlots = true;
    [SerializeField] private int materialSlotIndex = 0;

    private Transform wallsRoot;
    private Transform doorsRoot;
    private Transform resourcesRoot;

    private bool[,] grid;
    private readonly List<Vector2Int> pathCells = new();
    private readonly List<Vector2Int> carvedWalls = new();

    private Vector2Int forcedExitCell;     // open cell inside (adjacent to border)
    private Vector2Int forcedExitWallCell; // border wall cell where the victory door sits
    private Vector3 forcedExitDoorLocalPos;
    private Quaternion forcedExitDoorRot;

    private int normalDoorsAmount = 3;
    private int puzzleDoorsAmount = 2;

    private int heartsAmount = 3;
    private int bombsAmount = 2;
    private int keysAmount = 2;

    private int difficulty = 0; // 0 easy, 1 medium, 2 hard
    private int seed = 0;

    private static readonly Vector2Int StartCell = new Vector2Int(1, 1);

    // =========================
    //   GRID <-> WORLD MAPPING
    // =========================
    // אנחנו עובדים תמיד על "מרכז תא"
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
        // הפוך של (x+0.5)*cellSize
        int cx = Mathf.FloorToInt(local.x / cellSize);
        int cy = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(cx, cy);
    }

    private void Start()
    {
        PullConfigIfExists();
        Random.InitState(seed);

        CreateHierarchyFolders();

        GenerateMaze();

        // ✅ קיבוע: התא התחלתית תמיד פתוח
        grid[StartCell.x, StartCell.y] = false;
        if (!pathCells.Contains(StartCell)) pathCells.Add(StartCell);

        // ✅ שינוי חשוב: בוחרים יציאה "רחוקה" (border-adjacent)
        ComputeForcedExitCells_FarthestBorderAdjacent();

        // Ensure exit inward cell is open
        OpenForcedExit();

        BuildMaze();
        CreateGround();

        AlignMazeToNavigatorEntrance();
        UpdateTravellerSpawn();




        List<GameObject> puzzleDoorInstances = PlaceDoors();
        PlaceResources();
        AssignPuzzlesToPuzzleDoors(puzzleDoorInstances);
        MarkReady();
    }

    private void PullConfigIfExists()
    {
        var cfg = GameConfigNet.Instance;
        if (cfg == null)
            return;

        width = cfg.MazeWidth.Value;
        height = cfg.MazeHeight.Value;

        heartsAmount = cfg.Hearts.Value;
        bombsAmount = cfg.Bombs.Value;
        keysAmount = cfg.Keys.Value;

        normalDoorsAmount = cfg.NormalDoors.Value;
        puzzleDoorsAmount = cfg.PuzzleDoors.Value;

        difficulty = cfg.Difficulty.Value;
        seed = cfg.Seed.Value;
        if (seed == 0) seed = 1234567;
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

        // מרכז עולם של הגריד (כי אנחנו עובדים על +0.5 מרכזי תאים)
        ground.transform.localPosition = new Vector3(groundWidth / 2f, 0f, groundHeight / 2f);

        // אם ה-prefab שלך הוא Quad שמוטה/ציר שונה, תצטרך לכוון Scale בהתאם.
        ground.transform.localScale = new Vector3(groundWidth, groundHeight, 1f);
    }

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

        DFS(StartCell.x, StartCell.y);
    }

    private void DFS(int x, int y)
    {
        grid[x, y] = false;
        pathCells.Add(new Vector2Int(x, y));

        List<Vector2Int> dirs = new List<Vector2Int>()
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

    private bool IsInside(int x, int y)
    {
        return x > 0 && y > 0 && x < width - 1 && y < height - 1;
    }

    private void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    // ================================================================
    //   EXIT: choose farthest reachable border-adjacent cell from Start
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

        Dictionary<Vector2Int, int> dist = BFS_Distances(StartCell);

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

        // קובעים איפה על הגבול הדלת יושבת ואיזה כיוון היא פונה,
        // ואז מייצרים forcedExitDoorLocalPos במרכז תא + חצי תא החוצה בכיוון.
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
    private void BuildMaze()
    {
        foreach (Transform c in wallsRoot) Destroy(c.gameObject);
        foreach (Transform c in doorsRoot) Destroy(c.gameObject);
        foreach (Transform c in resourcesRoot) Destroy(c.gameObject);

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (!grid[x, y]) continue;

                // אל תיצור קיר במקום של דלת הניצחון
                if (x == forcedExitWallCell.x && y == forcedExitWallCell.y)
                    continue;

                // ✅ קירות במרכז תא
                Vector3 worldPos = CellCenterWorld(x, y, 1f);
                GameObject wall = Instantiate(wallPrefab, worldPos, Quaternion.identity, wallsRoot);

                SetLayerRecursive(wall, wallsLayer);
                ApplyWallMaterial(wall);
            }
    }

    private void ApplyWallMaterial(GameObject wall)
    {
        if (wall == null || wallMaterial == null) return;

        Renderer[] renderers = applyWallMaterialToChildren
            ? wall.GetComponentsInChildren<Renderer>(true)
            : wall.GetComponents<Renderer>();

        if (renderers == null || renderers.Length == 0) return;

        foreach (var r in renderers)
        {
            if (r == null) continue;

            if (replaceAllMaterialSlots)
            {
                if (useSharedMaterial)
                {
                    var mats = r.sharedMaterials;
                    if (mats == null || mats.Length == 0) { r.sharedMaterial = wallMaterial; continue; }
                    for (int i = 0; i < mats.Length; i++) mats[i] = wallMaterial;
                    r.sharedMaterials = mats;
                }
                else
                {
                    var mats = r.materials;
                    if (mats == null || mats.Length == 0) { r.material = wallMaterial; continue; }
                    for (int i = 0; i < mats.Length; i++) mats[i] = wallMaterial;
                    r.materials = mats;
                }
            }
            else
            {
                if (useSharedMaterial)
                {
                    var mats = r.sharedMaterials;
                    if (mats == null || mats.Length == 0) { r.sharedMaterial = wallMaterial; continue; }
                    if (materialSlotIndex < 0 || materialSlotIndex >= mats.Length) continue;
                    mats[materialSlotIndex] = wallMaterial;
                    r.sharedMaterials = mats;
                }
                else
                {
                    var mats = r.materials;
                    if (mats == null || mats.Length == 0) { r.material = wallMaterial; continue; }
                    if (materialSlotIndex < 0 || materialSlotIndex >= mats.Length) continue;
                    mats[materialSlotIndex] = wallMaterial;
                    r.materials = mats;
                }
            }
        }
    }

    private void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // ================================================================
    //   ALIGNMENT TO NAVIGATOR ENTRANCE + WIN DOOR
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

        if (exitForwardWorld.sqrMagnitude < 0.0001f || targetForwardWorld.sqrMagnitude < 0.0001f)
        {
            Vector3 targetPos = navigatorEntranceAnchor.position;
            Vector3 currentPos = transform.TransformPoint(forcedExitDoorLocalPos);
            Vector3 delta = targetPos - currentPos;
            transform.position += delta + new Vector3(1f, 0.5f, 0.5f);
            SpawnVictoryDoorLocal();
            return;
        }

        exitForwardWorld.Normalize();
        targetForwardWorld.Normalize();

        Quaternion rotDelta = Quaternion.FromToRotation(exitForwardWorld, targetForwardWorld);

        // ✅ rotate around door pivot (world)
        transform.RotateAround(doorWorldBefore, Vector3.up, rotDelta.eulerAngles.y);

        Vector3 doorWorldAfter = transform.TransformPoint(forcedExitDoorLocalPos);
        Vector3 targetWorld = navigatorEntranceAnchor.position;
        Vector3 deltaPos = targetWorld - doorWorldAfter;

        transform.position += deltaPos + new Vector3(1f, 0.5f, 0.5f);

        SpawnVictoryDoorLocal();
    }

    private void SpawnVictoryDoorLocal()
    {
        if (winDoorPrefab == null) return;

        var existing = doorsRoot.Find("WinDoor");
        if (existing != null) Destroy(existing.gameObject);

        GameObject door = Instantiate(winDoorPrefab, doorsRoot);
        door.name = "WinDoor";

        // ✅ local => יורש רוטציה/מיקום של המבוך
        door.transform.localPosition = forcedExitDoorLocalPos;
        door.transform.localRotation = forcedExitDoorRot;
    }

    // ================================================================
    //   DOORS
    // ================================================================
    private List<GameObject> PlaceDoors()
    {
        float minDist = 4f;
        List<GameObject> puzzleDoorInstances = new();

        List<DoorSpot> spots = DoorPlacement.FromCarvedWalls(grid, carvedWalls, width, height);

        List<DoorSpot> used = new();
        used.Add(new DoorSpot { cell = forcedExitWallCell, rotation = forcedExitDoorRot });

        List<Vector2Int> solutionPath = FindPathBFS(StartCell, forcedExitCell);
        HashSet<Vector2Int> pathSet = new(solutionPath);

        List<DoorSpot> puzzleCandidates = DoorPlacement.FilterOnPath(spots, pathSet);
        List<DoorSpot> puzzlePicked = DoorPlacement.PickEvenlySpaced(puzzleCandidates, puzzleDoorsAmount, minDist);

        foreach (var s in puzzlePicked)
        {
            if (!DoorPlacement.IsValidSpot(s, used, minDist)) continue;
            var go = SpawnDoorWorld(puzzleDoorPrefab, s);
            if (go != null) puzzleDoorInstances.Add(go);
            used.Add(s);
        }

        HashSet<Vector2Int> puzzleCells = new();
        foreach (var p in puzzlePicked) puzzleCells.Add(p.cell);

        List<DoorSpot> normalCandidates = new();
        foreach (var s in spots)
        {
            if (s.cell == forcedExitWallCell) continue;
            if (puzzleCells.Contains(s.cell)) continue;
            normalCandidates.Add(s);
        }

        List<DoorSpot> normalPicked = DoorPlacement.PickEvenlySpaced(normalCandidates, normalDoorsAmount, minDist);

        foreach (var s in normalPicked)
        {
            if (!DoorPlacement.IsValidSpot(s, used, minDist)) continue;
            SpawnDoorWorld(normalDoorPrefab, s);
            used.Add(s);
        }

        return puzzleDoorInstances;
    }

    private GameObject SpawnDoorWorld(GameObject prefab, DoorSpot spot)
    {
        if (prefab == null) return null;

        Vector3 world = CellCenterWorld(spot.cell.x, spot.cell.y, 0f);
        return Instantiate(prefab, world, spot.rotation, doorsRoot);
    }

    // ================================================================
    //   RESOURCES
    // ================================================================
    private void PlaceResources()
    {
        HashSet<Vector2Int> blocked = new();

        // ✅ חסימת תאים של דלתות (עם World->Cell נכון ל+0.5)
        foreach (Transform child in doorsRoot)
        {
            Vector2Int c = WorldToCell(child.position);
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
        startCell: StartCell,
        keepClearStepsForward: 3,
        forwardDir: forwardDir,
        yOffset: 0.58f,
      minSeparationCells: 4,
maxNeighborWallsAllowed: 2,
wallsLayer: wallsLayer,
collisionRadiusMultiplier: 0.22f

    );
    }

    private Vector2Int GetStartCorridorDir()
    {
        Vector2Int s = StartCell;

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

        List<Vector2Int> solution = FindPathBFS(StartCell, forcedExitCell);
        if (solution.Count >= 2)
        {
            Vector2Int next = solution[1];
            Vector2Int delta = next - StartCell;
            foreach (var d in openDirs)
                if (d == delta) return d;
        }

        return openDirs[0];
    }

    // ================================================================
    //   PUZZLE ASSIGNMENT
    // ================================================================
    private void AssignPuzzlesToPuzzleDoors(List<GameObject> puzzleDoors)
    {
        if (puzzleDoors == null || puzzleDoors.Count == 0) return;

        List<GameObject> toAssign = new();

        if (difficulty == 0 && puzzleEasyPrefab != null)
        {
            for (int i = 0; i < puzzleDoors.Count; i++) toAssign.Add(puzzleEasyPrefab);
        }
        else if (difficulty == 1 && puzzleMediumPrefab != null)
        {
            for (int i = 0; i < puzzleDoors.Count; i++) toAssign.Add(puzzleMediumPrefab);
        }
        else
        {
            if (puzzleHardPrefab != null)
            {
                int hardCount = Mathf.Min(2, puzzleDoors.Count);
                for (int i = 0; i < hardCount; i++) toAssign.Add(puzzleHardPrefab);
                for (int i = hardCount; i < puzzleDoors.Count; i++) toAssign.Add(puzzleHardPrefab);
            }
        }

        for (int i = 0; i < puzzleDoors.Count; i++)
        {
            var controller = puzzleDoors[i].GetComponent<DoorController>();
            if (controller != null)
                controller.puzzlePrefab = toAssign[Mathf.Clamp(i, 0, toAssign.Count - 1)];
        }
    }

    private void UpdateTravellerSpawn()
    {
        if (travellerSpawn == null) return;

        // ✅ ספאון במרכז התא (1,1)
        travellerSpawn.position = CellCenterWorld(StartCell.x, StartCell.y, 0.5f);
    }

  

    // ================================================================
    //   BFS PATHFINDING
    // ================================================================
    private List<Vector2Int> FindPathBFS(Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> empty = new();
        if (!InBounds(start) || !InBounds(goal)) return empty;
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
                if (!InBounds(nxt)) continue;
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

    private bool InBounds(Vector2Int c)
    {
        return c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;
    }
}
