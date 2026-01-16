    // Assets/Scripts/Maze/MazeGenerator3D.cs
    using System.Collections;
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
        [SerializeField] private float doorPrefabYawOffset = 0f; // use ONLY if prefab faces wrong direction

        [Header("Layers")]
        [SerializeField] private int wallsLayer = 12; 
        [SerializeField] private int doorsLayer = 14; 

        [Header("Traveller Spawn")]
        [SerializeField] private Transform travellerSpawn;

        // -------------------------------
        // Public API for deterministic spawning (read-only)
        // -------------------------------
        public Transform TravellerSpawn => travellerSpawn;

        public bool IsReady { get; private set; }
        public event System.Action Ready;
        public float MazeWorldWidth => width * cellSize;
        public float MazeWorldHeight => height * cellSize;


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
        [SerializeField] private float doorYawOffset = 0f; // try 90 or -90

        [Header("Puzzles (Prefabs)")]
        [SerializeField] private GameObject puzzleEasyPrefab;
        [SerializeField] private GameObject puzzleMediumPrefab;
        [SerializeField] private GameObject puzzleHardPrefab;     // HARD #1
        [SerializeField] private GameObject puzzleHardPrefab2;    // ✅ HARD #2 (NEW)

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
            int cx = Mathf.FloorToInt(local.x / cellSize);
            int cy = Mathf.FloorToInt(local.z / cellSize);
            return new Vector2Int(cx, cy);
        }

        private void Start()
        {
            // תמיד למשוך קונפיג (גם בקליינט)
            PullConfigIfExists();

            puzzleDoorsAmount = (difficulty == 2) ? 2 : 1;

            Random.InitState(seed);

            CreateHierarchyFolders();
            GenerateMaze();

            grid[StartCell.x, StartCell.y] = false;
            if (!pathCells.Contains(StartCell)) pathCells.Add(StartCell);

            ComputeForcedExitCells_FarthestBorderAdjacent();
            OpenForcedExit();

            // ✅ לבנות קירות/קרקע גם בקליינט כדי שיהיה מה לראות + מינימאפ
            BuildMaze();
            CreateGround();

            AlignMazeToNavigatorEntrance();

            // ❗️דברים שצריכים להיות רק בשרת:
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                UpdateTravellerSpawn();

                List<GameObject> puzzleDoorInstances = PlaceDoors();
                PlaceResources();
                StartCoroutine(ComputeBombRemovalsAfterResources());
                AssignPuzzlesToPuzzleDoors(puzzleDoorInstances);
            }

            MarkReady();
        }

        private IEnumerator ComputeBombRemovalsAfterResources()
        {
            // מחכה פריים אחד כדי לוודא שכל המשאבים הונחו
            yield return null;

            // אם PlaceResources משתמש בעוד קורוטינות – בטוח יותר:
            yield return new WaitForEndOfFrame();

            ComputeAndApplyBombRemovalsRuntime();
        }

        private void PullConfigIfExists()
        {
            var cfg = GameConfigNet.Instance;
            if (cfg == null) return;

            width = cfg.MazeWidth.Value;
            height = cfg.MazeHeight.Value;

            heartsAmount = cfg.Hearts.Value;
            bombsAmount = cfg.Bombs.Value;
            keysAmount = cfg.Keys.Value;

            normalDoorsAmount = cfg.NormalDoors.Value;
            puzzleDoorsAmount = cfg.PuzzleDoors.Value; // overridden by difficulty rule above

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

            ground.transform.localPosition = new Vector3(groundWidth / 2f, 0f, groundHeight / 2f);
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

                    Vector3 worldPos = CellCenterWorld(x, y, 1f);
                    GameObject wall = Instantiate(wallPrefab, worldPos, Quaternion.identity, wallsRoot);

                    SetLayerRecursive(wall, wallsLayer);

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
        //   DOORS (selection logic moved to DoorPlacement)
        // ================================================================
        private List<GameObject> PlaceDoors()
        {
            Debug.Log(
                $"[Doors] WANT normal={normalDoorsAmount}, puzzle={puzzleDoorsAmount} | " +
                $"difficulty={difficulty} | seed={seed} | size={width}x{height}"
            );

            // clear corridor direction for "don't place near traveller start"
            Vector2Int forwardDir = GetStartCorridorDir();

            // min dist relaxation in CELL units
            float[] distSteps = { 4f, 3f, 2f, 1f, 0.5f, 0f };

            var plan = DoorPlacement.PlanDoors(
                grid: grid,
                carvedWalls: carvedWalls,
                width: width,
                height: height,
                startCell: StartCell,
                forcedExitCell: forcedExitCell,
                wantNormal: normalDoorsAmount,
                wantPuzzle: puzzleDoorsAmount,
                keepClearStepsForward: 3,
                startForwardDir: forwardDir,
                minDistStepsCells: distSteps,
                nearPathManhattanRadius: 1
            );

            Debug.Log($"[Doors] PLAN placed normal={plan.PlacedNormal}/{plan.WantNormal}, puzzle={plan.PlacedPuzzle}/{plan.WantPuzzle}");

            List<GameObject> puzzleDoorInstances = new();

            // Spawn puzzle doors first
            for (int i = 0; i < plan.Puzzle.Count; i++)
            {
                var go = SpawnDoorWorld(puzzleDoorPrefab, plan.Puzzle[i]);
                if (go != null) puzzleDoorInstances.Add(go);
            }

            // Spawn normal doors
            for (int i = 0; i < plan.Normal.Count; i++)
            {
                SpawnDoorWorld(normalDoorPrefab, plan.Normal[i]);
            }

            if (plan.PlacedPuzzle < plan.WantPuzzle)
                Debug.LogWarning($"[Doors] Could only plan {plan.PlacedPuzzle}/{plan.WantPuzzle} puzzle doors (after relaxing).");

            if (plan.PlacedNormal < plan.WantNormal)
                Debug.LogWarning($"[Doors] Could only plan {plan.PlacedNormal}/{plan.WantNormal} normal doors (after relaxing).");

            return puzzleDoorInstances;
        }

        private GameObject SpawnDoorWorld(GameObject prefab, DoorSpot spot)
        {
            if (prefab == null) return null;

            var nm = NetworkManager.Singleton;
            if (nm != null && !nm.IsServer)
                return null;

            Vector3 world = CellCenterWorld(spot.cell.x, spot.cell.y, 0f);

            Quaternion rot =
                transform.rotation *
                spot.rotation *
                Quaternion.Euler(0f, doorPrefabYawOffset, 0f);

            var go = Instantiate(prefab, world, rot);
            SetLayerRecursive(go, doorsLayer);


            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"[Doors] Prefab '{prefab.name}' is missing NetworkObject.");
                Destroy(go);
                return null;
            }

            netObj.Spawn(true);
            spawnedDoors.Add(go);
            return go;
        }

        // ================================================================
        //   RESOURCES
        // ================================================================
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
                startCell: StartCell,
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

            // Prefer the direction that leads along the solution path
            List<Vector2Int> solution = DoorPlacement.FindPathBFS(grid, width, height, StartCell, forcedExitCell);
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
        //   PUZZLE ASSIGNMENT (NEW RULES)
        // ================================================================
        private void AssignPuzzlesToPuzzleDoors(List<GameObject> puzzleDoors)
        {
            if (puzzleDoors == null || puzzleDoors.Count == 0) return;

            int desired = (difficulty == 2) ? 2 : 1;
            int count = Mathf.Min(desired, puzzleDoors.Count);

            for (int i = 0; i < count; i++)
            {
                var controller = puzzleDoors[i].GetComponent<DoorController>();
                if (controller == null) continue;

                GameObject chosen = null;

                if (difficulty == 0)
                    chosen = puzzleEasyPrefab;
                else if (difficulty == 1)
                    chosen = puzzleMediumPrefab;
                else
                    chosen = (i == 0) ? puzzleHardPrefab : (puzzleHardPrefab2 != null ? puzzleHardPrefab2 : puzzleHardPrefab);

                if (chosen == null)
                {
                    Debug.LogWarning($"[Doors] Puzzle prefab is NULL for difficulty={difficulty}, index={i}.");
                    continue;
                }

                controller.SetPuzzlePrefabServer(chosen);
            }
        }

        // ================================================================
        //   TRAVELLER SPAWN
        // ================================================================
        private void UpdateTravellerSpawn()
        {
            if (travellerSpawn == null)
                return;

            Vector3 spawnPos = CellCenterWorld(StartCell.x, StartCell.y, 0.5f);
            spawnPos += transform.right * 0.5f;

            travellerSpawn.position = spawnPos;

            Vector2Int dir = GetClosestOpenNeighborDir(StartCell);

            Vector3 lookDirWorld;

            if (dir == Vector2Int.zero)
            {
                lookDirWorld = transform.forward;
            }
            else
            {
                Vector3 from = CellCenterWorld(StartCell.x, StartCell.y, 0.5f);
                Vector3 to = CellCenterWorld(
                    StartCell.x + dir.x,
                    StartCell.y + dir.y,
                    0.5f
                );

                lookDirWorld = (to - from);
            }

            lookDirWorld.y = 0f;

            if (lookDirWorld.sqrMagnitude > 0.0001f)
                travellerSpawn.rotation =
                    Quaternion.LookRotation(lookDirWorld.normalized, Vector3.up);
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

        // ================================================================
        //   Bounds helper
        // ================================================================
        private bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;

        // ================================
        //   BombRemovals by Difficulty
        // ================================
        private void ComputeAndApplyBombRemovalsRuntime()
        {
            // server only
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;

            var cfg = GameConfigNet.Instance;
            if (cfg == null) return;

            // collect bomb/key cells from spawned scene objects under Resources folder
            List<Vector2Int> bombCells = CollectResourceCellsOfType("Bomb");
            List<Vector2Int> keyCells = CollectResourceCellsOfType("Key");
            Debug.Log($"[Maze] bombs={bombCells.Count}, keys={keyCells.Count}, diff={cfg.Difficulty.Value}");

            int totalBombs = bombCells.Count;

            // EASY: removals == total bombs
            int easy = totalBombs;

            // HARD: bombs on shortest path Start->Exit + bombs on shortest path Start->NearestKey
            int bombsToExit = CountBombsOnShortestPath(StartCell, forcedExitCell, bombCells);

            int bombsToKey = 0;
            if (keyCells.Count > 0)
                bombsToKey = CountBombsOnShortestPathToNearestTarget(StartCell, keyCells, bombCells);

            int hard = Mathf.Max(0, bombsToExit + bombsToKey);

            // MEDIUM: midpoint between easy & hard
            int medium = Mathf.RoundToInt((easy + hard) * 0.5f);

            int diff = cfg.Difficulty.Value;
            int result = (diff == 0) ? easy : (diff == 1 ? medium : hard);

            // update networked config (you added this ServerRpc in GameConfigNet)
            cfg.SetBombRemovalsRuntimeServerRpc(result);

            Debug.Log($"[Maze] BombRemovals computed: easy={easy}, medium={medium}, hard={hard}, chosen={result} (diff={diff})");
        }

        // Collect cells by scanning objects spawned under resourcesRoot.
        // Tries PickupObject.PickupType first; if missing, falls back to name contains.
        // Collect cells by scanning ALL descendants under resourcesRoot (not only direct children).
        // Prefers PickupObject.PickupType; falls back to name contains.
        // MazeGenerator3D.cs
        // Replace your existing CollectResourceCellsOfType(string typeName) with this version.
        // Goal: do NOT depend on ResourcesRoot parenting (works even when NGO unparents NetworkObjects).

        private List<Vector2Int> CollectResourceCellsOfType(string typeName)
        {
            var cells = new List<Vector2Int>();
            string needle = typeName.ToLowerInvariant();

            // 1) Scan the ENTIRE scene for PickupObject of the requested type
            var pickups = FindObjectsByType<PickupObject>(FindObjectsSortMode.None);
            if (pickups != null && pickups.Length > 0)
            {
                for (int i = 0; i < pickups.Length; i++)
                {
                    var p = pickups[i];
                    if (p == null) continue;

                    // Prefer enum match (Bomb/Key/Heart)
                    if (!p.type.ToString().Equals(typeName, System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    Vector2Int c = WorldToCell(p.transform.position);
                    if (InBounds(c))
                        cells.Add(c);
                }

                // If we found via PickupObject, that's the most reliable
                if (cells.Count > 0)
                    return cells;
            }

            // 2) Backup: scan by Tag (Bomb / Key) across the scene
            // (Only works if you actually set these tags on the prefabs)
            try
            {
                var tagged = GameObject.FindGameObjectsWithTag(typeName);
                if (tagged != null && tagged.Length > 0)
                {
                    for (int i = 0; i < tagged.Length; i++)
                    {
                        var go = tagged[i];
                        if (go == null) continue;

                        Vector2Int c = WorldToCell(go.transform.position);
                        if (InBounds(c))
                            cells.Add(c);
                    }

                    if (cells.Count > 0)
                        return cells;
                }
            }
            catch
            {
                // Tag may not exist; ignore safely
            }

            // 3) Last resort: scan all root objects by name contains (bomb/key)
            // (This is only for safety; prefer PickupObject / Tag)
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

                    string n = t.name;
                    if (string.IsNullOrEmpty(n)) continue;
                    if (!n.ToLowerInvariant().Contains(needle)) continue;

                    Vector2Int c = WorldToCell(t.position);
                    if (InBounds(c))
                        cells.Add(c);
                }
            }

            return cells;
        }

        private int CountBombsOnShortestPath(Vector2Int start, Vector2Int goal, List<Vector2Int> bombCells)
        {
            var path = FindShortestPathCells(start, goal);
            if (path == null || path.Count == 0) return 0;

            var bombs = new HashSet<Vector2Int>(bombCells);
            int count = 0;

            // exclude start cell
            for (int i = 1; i < path.Count; i++)
                if (bombs.Contains(path[i])) count++;

            return count;
        }

        private int CountBombsOnShortestPathToNearestTarget(Vector2Int start, List<Vector2Int> targets, List<Vector2Int> bombCells)
        {
            List<Vector2Int> bestPath = null;

            for (int i = 0; i < targets.Count; i++)
            {
                var p = FindShortestPathCells(start, targets[i]);
                if (p == null || p.Count == 0) continue;

                if (bestPath == null || p.Count < bestPath.Count)
                    bestPath = p;
            }

            if (bestPath == null) return 0;

            var bombs = new HashSet<Vector2Int>(bombCells);
            int count = 0;

            for (int i = 1; i < bestPath.Count; i++)
                if (bombs.Contains(bestPath[i])) count++;

            return count;
        }

        // BFS shortest path using your grid (grid[x,y] == true means WALL, false means OPEN)
        private List<Vector2Int> FindShortestPathCells(Vector2Int start, Vector2Int goal)
        {
            if (!InBounds(start) || !InBounds(goal)) return null;
            if (grid[start.x, start.y]) return null; // wall
            if (grid[goal.x, goal.y]) return null;   // wall

            var q = new Queue<Vector2Int>();
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int>();

            q.Enqueue(start);
            visited.Add(start);

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
                if (cur == goal) break;

                for (int i = 0; i < dirs.Length; i++)
                {
                    var nxt = cur + dirs[i];
                    if (!InBounds(nxt)) continue;
                    if (grid[nxt.x, nxt.y]) continue; // wall
                    if (visited.Contains(nxt)) continue;

                    visited.Add(nxt);
                    prev[nxt] = cur;
                    q.Enqueue(nxt);
                }
            }

            if (!visited.Contains(goal)) return null;

            var path = new List<Vector2Int>();
            var p = goal;
            path.Add(p);

            while (p != start)
            {
                p = prev[p];
                path.Add(p);
            }

            path.Reverse();
            return path;
        }

    }
