// Assets/Scripts/Maze/MazeGenerator3D.cs
using System.Collections.Generic;
using Unity.Netcode;
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

    private Vector2Int forcedExitCell;     // border cell (open)
    private Vector2Int forcedExitWallCell; // wall cell where the victory door sits
    private Vector3 forcedExitDoorLocalPos;
    private Quaternion forcedExitDoorRot;

    private int normalDoorsAmount = 3;
    private int puzzleDoorsAmount = 2;

    private int heartsAmount = 3;
    private int bombsAmount = 2;
    private int keysAmount = 2;

    private int difficulty = 0; // 0 easy, 1 medium, 2 hard
    private int seed = 0;

    private void Start()
    {
        // Server sets config, but we generate locally on ALL peers with same seed/config
        PullConfigIfExists();

        Random.InitState(seed);

        CreateHierarchyFolders();

        GenerateMaze();
        BuildMaze();
        CreateGround();

        // Force exit near "top" side like your current layout (adapted):
        // We keep the same logical choice, BUT we align the entire MazeGenerator transform
        // so the victory door ends up at navigatorEntranceAnchor, regardless of maze size.
        ComputeForcedExitCells();

        // Ensure the door cell and the open cell behind it are open
        OpenForcedExit();

        // Align maze root so victory door matches navigator entrance anchor
        AlignMazeToNavigatorEntrance();

        UpdateTravellerSpawn();


        // Place doors/resources (delegation to DoorPlacement + ResourcePlacement static classes)
        List<GameObject> puzzleDoorInstances = PlaceDoors();

        PlaceResources();

        AssignPuzzlesToPuzzleDoors(puzzleDoorInstances);
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
        if (seed == 0) seed = 1234567; // deterministic fallback if host forgot to set
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

        GameObject ground = Instantiate(groundPrefab);
        ground.name = "Ground";
        ground.transform.SetParent(transform, false);

        float groundWidth = width * cellSize;
        float groundHeight = height * cellSize;

        ground.transform.localPosition = new Vector3(
            (groundWidth / 2f) - (cellSize / 2f),
            0,
            (groundHeight / 2f) - (cellSize / 2f)
        );

        ground.transform.localScale = new Vector3(groundWidth, groundHeight, 1);
    }

    // ================================================================
    //   MAZE GENERATION (DFS CARVE)
    // ================================================================
    void GenerateMaze()
    {
        // מומלץ: לאפשר רק מידות אי-זוגיות
        if (width % 2 == 0) width++;
        if (height % 2 == 0) height++;

        grid = new bool[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = true;

        DFS(1, 1);
    }

    void DFS(int x, int y)
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

    bool IsInside(int x, int y)
    {
        return x > 0 && y > 0 && x < width - 1 && y < height - 1;
    }

    void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }


    // ================================================================
    //   BUILD WALLS (instantiate all WALL cells)
    // ================================================================
    private void BuildMaze()
    {
        // clear old children if you re-run in editor play mode
        foreach (Transform c in wallsRoot) Destroy(c.gameObject);
        foreach (Transform c in doorsRoot) Destroy(c.gameObject);
        foreach (Transform c in resourcesRoot) Destroy(c.gameObject);

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (!grid[x, y]) continue;

                Vector3 pos = new Vector3(x * cellSize, 1, y * cellSize);
                GameObject wall = Instantiate(wallPrefab, pos, Quaternion.identity, wallsRoot);

                SetLayerRecursive(wall, wallsLayer); // <-- חשוב!!
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
    //   FORCED EXIT + ALIGNMENT TO NAVIGATOR ENTRANCE
    // ================================================================
    private void ComputeForcedExitCells()
    {
        // We force the "victory door wall" to be on the top border (y = height - 1),
        // at x = width - 2 (like you had), but it works for any odd size.
        forcedExitCell = new Vector2Int(width - 2, height - 2);      // open cell inside
        forcedExitWallCell = new Vector2Int(width - 2, height - 1);  // wall on the border

        // door sits centered on wall cell, facing "out" (+Z)
        forcedExitDoorLocalPos = new Vector3(forcedExitWallCell.x * cellSize, 0, forcedExitWallCell.y * cellSize + (cellSize * 0.5f));
        forcedExitDoorRot = Quaternion.LookRotation(Vector3.forward); // points +Z
    }

    private void OpenForcedExit()
    {
        // make sure inside open cell is open
        grid[forcedExitCell.x, forcedExitCell.y] = false;
        if (!pathCells.Contains(forcedExitCell))
            pathCells.Add(forcedExitCell);

        // keep wall cell as wall in grid (needed for DoorPlacement),
        // but remove its instantiated wall GO if it exists later (we built walls already).
        // We'll "delete wall GO at forcedExitWallCell" and then spawn win door there.
        RemoveWallGOAtCell(forcedExitWallCell);

        // also ensure the cell adjacent inward is open (already is)
        // (no need to open the wall cell itself)
    }

    private void RemoveWallGOAtCell(Vector2Int wallCell)
    {
        foreach (Transform w in wallsRoot)
        {
            Vector2Int c = new(
                Mathf.RoundToInt(w.localPosition.x / cellSize),
                Mathf.RoundToInt(w.localPosition.z / cellSize)
            );

            if (c == wallCell)
            {
                Destroy(w.gameObject);
                return;
            }
        }
    }

    private void AlignMazeToNavigatorEntrance()
    {
        if (navigatorEntranceAnchor == null)
        {
            // still place the victory door in local space
            SpawnVictoryDoorLocal();
            return;
        }

        // compute current world position of door if we spawned it at local pos:
        // worldDoorPos = transform.TransformPoint(localDoorPos)
        // want worldDoorPos == navigatorEntranceAnchor.position
        Vector3 target = navigatorEntranceAnchor.position;
        Vector3 current = transform.TransformPoint(forcedExitDoorLocalPos);

        Vector3 delta = target - current;
        transform.position += delta + new Vector3(1f, 0.5f, 0.5f);


        // now spawn door at aligned position
        SpawnVictoryDoorLocal();
    }

    private void SpawnVictoryDoorLocal()
    {
        if (winDoorPrefab == null) return;
        Vector3 pos = forcedExitDoorLocalPos;
        Instantiate(winDoorPrefab, transform.TransformPoint(pos), forcedExitDoorRot, doorsRoot);
    }

    // ================================================================
    //   DOORS
    //   - Victory door already placed & aligned
    //   - Puzzle doors must be on solution path
    //   - Normal doors spread evenly
    // ================================================================
    private List<GameObject> PlaceDoors()
    {
        float minDist = 4f;
        List<GameObject> puzzleDoorInstances = new();

        // Collect possible door wall-spots
        List<DoorSpot> spots = DoorPlacement.FromCarvedWalls(grid, carvedWalls, width, height);

        // Mark forced exit wall cell as "used" so we don't place another door on it
        List<DoorSpot> used = new();
        used.Add(new DoorSpot { cell = forcedExitWallCell, rotation = forcedExitDoorRot });

        // Solve path from start to forcedExitCell
        List<Vector2Int> solutionPath = FindPathBFS(new Vector2Int(1, 1), forcedExitCell);
        HashSet<Vector2Int> pathSet = new(solutionPath);

        // Puzzle doors: only where both adjacent open cells are on the solution path
        List<DoorSpot> puzzleCandidates = DoorPlacement.FilterOnPath(spots, pathSet);
        List<DoorSpot> puzzlePicked = DoorPlacement.PickEvenlySpaced(puzzleCandidates, puzzleDoorsAmount, minDist);

        foreach (var s in puzzlePicked)
        {
            if (!DoorPlacement.IsValidSpot(s, used, minDist)) continue;
            RemoveWallGOAtCell(s.cell);
            var go = SpawnDoorWorld(puzzleDoorPrefab, s);
            if (go != null) puzzleDoorInstances.Add(go);
            used.Add(s);
        }

        // Normal doors: any other valid spot, spread evenly
        // Remove puzzle-picked from pool
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
            RemoveWallGOAtCell(s.cell);
            SpawnDoorWorld(normalDoorPrefab, s);
            used.Add(s);
        }

        return puzzleDoorInstances;
    }

    private GameObject SpawnDoorWorld(GameObject prefab, DoorSpot spot)
    {
        if (prefab == null) return null;

        Vector3 local = new Vector3(spot.cell.x * cellSize, 0, spot.cell.y * cellSize);
        Vector3 world = transform.TransformPoint(local);

        return Instantiate(prefab, world, spot.rotation, doorsRoot);
    }

    // ================================================================
    //   RESOURCES (spread evenly, avoid doors)
    // ================================================================
    private void PlaceResources()
    {
        HashSet<Vector2Int> blocked = new();

        foreach (Transform child in doorsRoot)
        {
            Vector3 local = transform.InverseTransformPoint(child.position);
            Vector2Int c = new(
                Mathf.RoundToInt(local.x / cellSize),
                Mathf.RoundToInt(local.z / cellSize)
            );
            blocked.Add(c);
        }

        ResourcePlacement.PlaceResources(grid, pathCells, blocked, cellSize, resourcesRoot, heartPrefab, heartsAmount);
        ResourcePlacement.PlaceResources(grid, pathCells, blocked, cellSize, resourcesRoot, bombPrefab, bombsAmount);
        ResourcePlacement.PlaceResources(grid, pathCells, blocked, cellSize, resourcesRoot, keyPrefab, keysAmount);
    }

    // ================================================================
    //   PUZZLE ASSIGNMENT
    //   - Easy: 1x Easy per puzzle door
    //   - Medium: 1x Medium per puzzle door
    //   - Hard: two Hard puzzles total (if there are >=2 puzzle doors)
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
            // HARD: must get 2
            if (puzzleHardPrefab != null)
            {
                int hardCount = Mathf.Min(2, puzzleDoors.Count);
                for (int i = 0; i < hardCount; i++) toAssign.Add(puzzleHardPrefab);

                // remaining (if any): also hard (or leave null)
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

        Vector3 localCellCenter = new Vector3(
            1 * cellSize + cellSize * 0.5f,
            0f,
            1 * cellSize + cellSize * 0.5f
        );

        Vector3 worldPos = transform.TransformPoint(localCellCenter);
        travellerSpawn.position = worldPos;
    }


    // ================================================================
    //   BFS PATHFINDING ON OPEN CELLS
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
                if (grid[nxt.x, nxt.y]) continue; // wall

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
