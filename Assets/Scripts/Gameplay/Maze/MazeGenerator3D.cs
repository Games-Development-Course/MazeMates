using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator3D : MonoBehaviour
{
    [Header("Maze Settings")]
    public int width = 20;
    public int height = 20;
    public float cellSize = 2f;

    [Header("Ground")]
    public GameObject groundPrefab;


    [Header("Prefabs")]
    public GameObject wallPrefab;

    public GameObject normalDoorPrefab;
    public int normalDoorsAmount = 3;

    public GameObject puzzleDoorPrefab;
    public int puzzleDoorsAmount = 2;

    public GameObject exitDoorPrefab;

    [Header("Puzzle Prefabs (match puzzleDoorsAmount)")]
    public List<GameObject> puzzlePrefabs = new List<GameObject>();

    [Header("Resources")]
    public GameObject heartPrefab;
    public int heartsAmount = 3;

    public GameObject bombPrefab;
    public int bombsAmount = 2;

    public GameObject keyPrefab;
    public int keysAmount = 2;

    private Transform wallsRoot;
    private Transform doorsRoot;
    private Transform resourcesRoot;

    private bool[,] grid;
    private List<Vector2Int> pathCells = new List<Vector2Int>();
    private List<Vector2Int> carvedWalls = new List<Vector2Int>();

    // our forced exit position
    private Vector2Int forcedExitCell;

    void Start()
    {
        CreateHierarchyFolders();

        GenerateMaze();
        BuildMaze();

        // Ground generation – MUST be after BuildMaze()
        CreateGround();

        // ------------------------------------------
        // FORCE EXIT POSITION BEFORE PLACING DOORS
        // ------------------------------------------
        forcedExitCell = new Vector2Int(width - 2, height - 1);

        grid[forcedExitCell.x, forcedExitCell.y] = false;
        pathCells.Add(forcedExitCell);

        PlaceForcedExitDoor();

        List<GameObject> puzzleDoorInstances = PlaceDoors();

        PlaceResources();
        AssignPuzzlePrefabsToPuzzleDoors(puzzleDoorInstances);
    }


    // ================================================================
    //   CREATE FOLDER STRUCTURE
    // ================================================================
    void CreateHierarchyFolders()
    {
        Transform mazeRoot = transform;

        wallsRoot = mazeRoot.Find("Walls");
        if (wallsRoot == null)
        {
            wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(mazeRoot);
        }

        doorsRoot = mazeRoot.Find("Doors");
        if (doorsRoot == null)
        {
            doorsRoot = new GameObject("Doors").transform;
            doorsRoot.SetParent(mazeRoot);
        }

        resourcesRoot = mazeRoot.Find("Resources");
        if (resourcesRoot == null)
        {
            resourcesRoot = new GameObject("Resources").transform;
            resourcesRoot.SetParent(mazeRoot);
        }
    }
    void CreateGround()
    {
        if (groundPrefab == null)
        {
            Debug.LogWarning("Ground prefab is missing!");
            return;
        }

        // יצירת אובייקט חדש
        GameObject ground = Instantiate(groundPrefab);

        // שמים אותו הילד של המבוך
        ground.name = "Ground";
        ground.transform.SetParent(transform);

        // גודל הרצפה לפי מספר תאים * cellSize
        float groundWidth = width * cellSize;
        float groundHeight = height * cellSize;

        // מניחים אותו במרכז של כל המבוך
        ground.transform.localPosition = new Vector3(
            (groundWidth / 2f) - (cellSize / 2f),
            0,
            (groundHeight / 2f) - (cellSize / 2f)
        );

        // אם זו רצפה עם Scale רגיל (1,1,1) — נשנה Scale לפי הגודל
        ground.transform.localScale = new Vector3(groundWidth, groundHeight,1 );
    }


    // ================================================================
    //   MAZE GENERATION
    // ================================================================
    void GenerateMaze()
    {
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
            new Vector2Int(0, -2)
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
    //   BUILD WALLS
    // ================================================================
    void BuildMaze()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y])
                {
                    Vector3 pos = new Vector3(x * cellSize, 1, y * cellSize);
                    Instantiate(wallPrefab, pos, Quaternion.identity, wallsRoot);
                }
        int wx = width - 2;
        int wy = height - 1;

        // פותחים אותו ברשת
        grid[wx, wy] = false;

        // מוחקים את קיר ה-GameObject מהסצנה
        foreach (Transform w in wallsRoot)
        {
            Vector2Int c = new Vector2Int(
                Mathf.RoundToInt(w.position.x / cellSize),
                Mathf.RoundToInt(w.position.z / cellSize)
            );

            if (c.x == wx && c.y == wy)
            {
                Destroy(w.gameObject);
                break;
            }
        }

        Debug.Log($"Removed wall at ({wx}, {wy})");
    }

    // ================================================================
    //   FORCE EXIT DOOR
    // ================================================================
    void PlaceForcedExitDoor()
    {
        // 1) פותח את תיבת הדלת
        grid[forcedExitCell.x, forcedExitCell.y] = false;
        if (!pathCells.Contains(forcedExitCell))
            pathCells.Add(forcedExitCell);

        // 2) פותח את התא שמתחתיה (הקיר שמפריע)
        Vector2Int below = new Vector2Int(forcedExitCell.x, forcedExitCell.y - 1);
        if (below.y >= 0)
        {
            grid[below.x, below.y] = false;
            if (!pathCells.Contains(below))
                pathCells.Add(below);
        }

        // 3) מציב את הדלת בדיוק על הקצה התחתון של התיבה
        float half = cellSize * 0.5f;

        Vector3 exitPos = new Vector3(
            forcedExitCell.x * cellSize,
            0,
            forcedExitCell.y * cellSize - half   // זה נותן Z=9.5 במקרה של cellSize=1
        );

        // 4) מסובב שתפנה "למטה" (south / -Z)
        Quaternion rot = Quaternion.LookRotation(Vector3.forward);

        Instantiate(exitDoorPrefab, exitPos, rot, doorsRoot);
    }


    // ================================================================
    //   DOOR PLACEMENT (NORMAL + PUZZLE)
    //   * Exit door already handled *
    // ================================================================
    List<GameObject> PlaceDoors()
    {
        float minDist = 4f;
        List<GameObject> puzzleDoorInstances = new List<GameObject>();

        List<DoorSpot> spots = DoorPlacement.FromCarvedWalls(grid, carvedWalls, width, height);

        // List for already-used spots, including forced exit position
        List<DoorSpot> used = new List<DoorSpot>();

        // NORMAL DOORS
        for (int i = 0; i < normalDoorsAmount; i++)
        {
            var s = DoorPlacement.PickRandomSpot(spots, used, minDist);
            if (s == null) break;

            SpawnDoor(normalDoorPrefab, s);
            used.Add(s);
        }

        // PUZZLE DOORS
        spots.Sort((a, b) =>
            Vector2.Distance(new Vector2(1, 1), b.cell)
                .CompareTo(Vector2.Distance(new Vector2(1, 1), a.cell))
        );

        for (int i = 0; i < puzzleDoorsAmount; i++)
        {
            var s = DoorPlacement.PickDeepSpot(spots, used, minDist);
            if (s == null) break;

            GameObject pd = SpawnDoor(puzzleDoorPrefab, s);
            if (pd != null)
                puzzleDoorInstances.Add(pd);

            used.Add(s);
        }

        return puzzleDoorInstances;
    }

    GameObject SpawnDoor(GameObject prefab, DoorSpot spot)
    {
        if (prefab == null)
            return null;

        Vector3 pos = new Vector3(spot.cell.x * cellSize, 0, spot.cell.y * cellSize);
        return Instantiate(prefab, pos, spot.rotation, doorsRoot);
    }

    // ================================================================
    //   RESOURCE PLACEMENT
    // ================================================================
    void PlaceResources()
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();

        foreach (Transform child in doorsRoot)
        {
            Vector3 pos = child.position;
            Vector2Int c = new Vector2Int(
                Mathf.RoundToInt(pos.x / cellSize),
                Mathf.RoundToInt(pos.z / cellSize)
            );
            blocked.Add(c);
        }

        ResourcePlacement.PlaceResources(
            grid, pathCells, blocked, cellSize,
            resourcesRoot, heartPrefab, heartsAmount
        );
        ResourcePlacement.PlaceResources(
            grid, pathCells, blocked, cellSize,
            resourcesRoot, bombPrefab, bombsAmount
        );
        ResourcePlacement.PlaceResources(
            grid, pathCells, blocked, cellSize,
            resourcesRoot, keyPrefab, keysAmount
        );
    }

    void AssignPuzzlePrefabsToPuzzleDoors(List<GameObject> puzzleDoors)
    {
        if (puzzlePrefabs.Count != puzzleDoorsAmount)
        {
            Debug.LogError("Puzzle Prefabs list must match puzzleDoorsAmount.");
            return;
        }

        for (int i = 0; i < puzzleDoors.Count; i++)
        {
            var controller = puzzleDoors[i].GetComponent<DoorController>();
            if (controller != null)
                controller.puzzlePrefab = puzzlePrefabs[i];
        }
    }
}
