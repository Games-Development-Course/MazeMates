using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator3D : MonoBehaviour
{
    [Header("Maze Settings")]
    public int width = 20;
    public int height = 20;
    public float cellSize = 2f;

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

    void Start()
    {
        CreateHierarchyFolders();

        GenerateMaze();
        BuildMaze();
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
    //   BUILD WALLS
    // ================================================================
    void BuildMaze()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y])
                {
                    Vector3 pos = new Vector3(x * cellSize, 1, y * cellSize);
                    Instantiate(wallPrefab, pos, Quaternion.identity, wallsRoot);
                }
            }
        }
    }

    // ================================================================
    //   DOOR PLACEMENT
    // ================================================================
    List<GameObject> PlaceDoors()
    {
        float minDist = 4f;

        List<GameObject> puzzleDoorInstances = new List<GameObject>();

        List<DoorSpot> spots = DoorPlacement.FromCarvedWalls(grid, carvedWalls, width, height);

        if (spots.Count == 0)
        {
            Debug.LogWarning("No valid door spots found!");
            return puzzleDoorInstances;
        }

        List<DoorSpot> used = new List<DoorSpot>();

        // EXIT DOOR
        Vector2Int exitTarget = new Vector2Int(width - 2, height - 2);
        var exitSpot = DoorPlacement.FindClosestSpot(spots, exitTarget, used, minDist);

        if (exitSpot != null)
        {
            SpawnDoor(exitDoorPrefab, exitSpot);
            used.Add(exitSpot);
        }

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
            .CompareTo(Vector2.Distance(new Vector2(1, 1), a.cell)));

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

        ResourcePlacement.PlaceResources(grid, pathCells, blocked, cellSize, resourcesRoot, heartPrefab, heartsAmount);
        ResourcePlacement.PlaceResources(grid, pathCells, blocked, cellSize, resourcesRoot, bombPrefab, bombsAmount);
        ResourcePlacement.PlaceResources(grid, pathCells, blocked, cellSize, resourcesRoot, keyPrefab, keysAmount);
    }

    // ================================================================
    //   DOOR SPAWNING
    // ================================================================
    GameObject SpawnDoor(GameObject prefab, DoorSpot spot)
    {
        if (prefab == null) return null;

        Vector3 pos = new Vector3(spot.cell.x * cellSize, 0, spot.cell.y * cellSize);

        return Instantiate(prefab, pos, spot.rotation, doorsRoot);
    }

    // ================================================================
    //   ASSIGN PUZZLE PREFABS TO PUZZLE DOORS
    // ================================================================
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
            {
                controller.puzzlePrefab = puzzlePrefabs[i];
                Debug.Log("Assigned puzzle prefab to: " + puzzleDoors[i].name);
            }
        }
    }
                
}
