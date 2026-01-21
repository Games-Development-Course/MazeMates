using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ResourceManager : NetworkBehaviour
{
    public static ResourceManager Instance;

    [Header("Bomb Settings")]
    public int bombRemoveMaxSteps = 6;

    [Header("Prefabs")]
    public GameObject heartPrefab;
    public GameObject lifebuoyEffectPrefab;
    
    [Header("Bomb Removal Limits")]
    [SerializeField] private int maxBombRemoveSteps = 3; // לדוגמה

    private TutorialManager tutorial;

    // ============================================================
    // DEBUG Bomb Selection
    // ============================================================
    [Header("DEBUG Bomb Selection")]
    [SerializeField] private bool debugBombPick = true;
    [SerializeField] private bool debugDrawGizmos = true;
    [SerializeField] private float debugGizmoSeconds = 6f;

#if UNITY_EDITOR
    private struct BombDebugRow
    {
        public GameObject bomb;
        public Vector2Int bombCell;
        public bool cellOpen;
        public bool reachable;
        public int steps;
        public float euclid;
        public float euclidSqr;
    }

    private float _dbgUntil = -1f;
    private Vector3 _dbgTravellerWorld;
    private Vector2Int _dbgTravellerCell;
    private readonly List<BombDebugRow> _dbgRows = new List<BombDebugRow>(64);
    private GameObject _dbgBestBomb;
    private int _dbgBestSteps;

    private void DebugStoreSnapshot(
        Vector3 travellerWorld,
        Vector2Int travellerCell,
        List<BombDebugRow> rows,
        GameObject bestBomb,
        int bestSteps)
    {
        if (!debugDrawGizmos) return;

        _dbgUntil = Time.realtimeSinceStartup + Mathf.Max(0.1f, debugGizmoSeconds);
        _dbgTravellerWorld = travellerWorld;
        _dbgTravellerCell = travellerCell;

        _dbgRows.Clear();
        _dbgRows.AddRange(rows);

        _dbgBestBomb = bestBomb;
        _dbgBestSteps = bestSteps;
    }

    private void OnDrawGizmos()
    {
        if (!debugDrawGizmos) return;
        if (_dbgUntil <= 0f) return;
        if (Time.realtimeSinceStartup > _dbgUntil) return;

        var maze = FindFirstObjectByType<MazeGenerator3D>();
        if (maze == null) return;

        float cs = maze.CellSize;

        // Traveller world point
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(_dbgTravellerWorld + Vector3.up * 0.2f, 0.25f);

        // Traveller cell
        DrawCellBox(maze, _dbgTravellerCell, cs, Color.cyan);

        // Bombs
        for (int i = 0; i < _dbgRows.Count; i++)
        {
            var r = _dbgRows[i];
            if (r.bomb == null) continue;

            Color c = (!r.cellOpen) ? Color.magenta : (r.reachable ? Color.yellow : Color.gray);

            DrawCellBox(maze, r.bombCell, cs, c);

            // bomb -> cell center
            Gizmos.color = c;
            Gizmos.DrawLine(r.bomb.transform.position, CellCenterWorld(maze, r.bombCell, cs));

            // traveller -> bomb
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawLine(_dbgTravellerWorld, r.bomb.transform.position);

            // highlight chosen
            if (r.bomb == _dbgBestBomb)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(r.bomb.transform.position + Vector3.up * 0.3f, 0.28f);
                DrawCellBox(maze, r.bombCell, cs, Color.red);
            }
        }
    }
    private struct BombPick
    {
        public GameObject bomb;
        public int steps;
    }


    private static void DrawCellBox(MazeGenerator3D maze, Vector2Int c, float cs, Color col)
    {
        Gizmos.color = col;
        Vector3 center = CellCenterWorld(maze, c, cs);
        Vector3 size = new Vector3(cs, 0.08f, cs);
        Gizmos.DrawWireCube(center, size);
    }

    private static Vector3 CellCenterWorld(MazeGenerator3D maze, Vector2Int c, float cs)
    {
        // cell center in MAZE-LOCAL then transform to world (rotation-aware)
        return maze.transform.TransformPoint(new Vector3((c.x + 0.5f) * cs, 0.05f, (c.y + 0.5f) * cs));
    }
#endif

    // ============================================================
    // INITIALIZATION
    // ============================================================

    public override void OnNetworkSpawn()
    {
        Instance = this;

        // אל תסמוך שזה קיים כבר כאן (כי TM יכול להיווצר דינמית אחרי זה)
        tutorial = null;

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[ResourceManager] NetworkSpawn | Server={IsServer} | Client={IsClient}"
        );
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private TutorialManager EnsureTutorial()
    {
        if (tutorial == null)
        {
            tutorial = Object.FindFirstObjectByType<TutorialManager>();
            if (tutorial == null)
            {
                Debug.LogFormat(
                    LogType.Warning,
                    LogOption.NoStacktrace,
                    null,
                    "[ResourceManager] EnsureTutorial: TutorialManager NOT FOUND"
                );
            }
            else
            {
                Debug.LogFormat(
                    LogType.Log,
                    LogOption.NoStacktrace,
                    null,
                    "[ResourceManager] EnsureTutorial: TutorialManager FOUND"
                );
            }
        }

        return tutorial;
    }

    // ============================================================
    // PUBLIC API (Navigator)
    // ============================================================

    public void TryRemoveBomb()
    {
        if (!IsServer)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[CLIENT] Sending RequestRemoveBombRpc");
            RequestRemoveBombServerRpc();
            return;
        }

        ServerRemoveBomb();
    }

    public void TryPlaceHeart()
    {
        if (!IsServer)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[CLIENT] Sending RequestPlaceHeartRpc");
            RequestPlaceHeartServerRpc();
            return;
        }

        ServerPlaceHeart();
    }

    public void TryUseLifebuoy()
    {
        if (!IsServer)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[CLIENT] Sending RequestUseLifebuoyRpc");
            RequestUseLifebuoyServerRpc();
            return;
        }

        ServerUseLifebuoy();
    }

    // ============================================================
    // BOMB REMOVAL — SERVER LOGIC
    // ============================================================

    [ClientRpc]
    private void NavSetBombSpotlightClientRpc(bool on, ClientRpcParams rpcParams = default)
    {
        NavigatorSpotlights.I?.SetNearBomb(on);
    }

    private static ClientRpcParams MakeAllNonServerClientsTargetParams()
    {
        var nm = NetworkManager.Singleton;
        var ids = nm.ConnectedClientsIds;

        var list = new List<ulong>(ids.Count);
        foreach (var id in ids)
            if (id != NetworkManager.ServerClientId)
                list.Add(id);

        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = list.ToArray() }
        };
    }

  

    // ✅ World -> Cell that RESPECTS maze transform (position + rotation)
    private static Vector2Int WorldToCell_TransformAware(MazeGenerator3D maze, Vector3 worldPos)
    {
        float cs = maze.CellSize;
        Vector3 local = maze.transform.InverseTransformPoint(worldPos);
        int x = Mathf.FloorToInt(local.x / cs);
        int y = Mathf.FloorToInt(local.z / cs);
        return new Vector2Int(x, y);
    }

    // ------------------------------------------------------------
    // FIND CLOSEST BOMB BY GRID PATH
    // ------------------------------------------------------------
    private GameObject FindClosestBombByGridPath(
     MazeGenerator3D maze,
     Vector3 travellerWorldPos,
     Vector3 travellerProbePos,
     out int pickedSteps)
    {
        pickedSteps = int.MaxValue;

        float cs = maze.CellSize;

        // Traveller cell: נעדיף probe אם הוא הגיוני, אחרת travellerWorldPos
        Vector3 basePos = (travellerProbePos != Vector3.zero) ? travellerProbePos : travellerWorldPos;
        Vector2Int travellerCell = WorldToCell_TransformAware(maze, basePos);

        if (!maze.IsCellOpen(travellerCell))
        {
            // fallback: נסיון snap מהנקודה שנבחרה
            int r = 6; // מספיק בדרך כלל; אם תרצה נעשה חישוב רדיוס לפי bounds כמו קודם
            if (!maze.TrySnapToNearestOpenCell(basePos, r, out travellerCell))
            {
                Debug.LogWarning($"[SERVER][BOMBDBG] traveller snap failed. basePos={basePos} WorldToCell={travellerCell}");
                return null;
            }
        }

        Debug.Log($"[SERVER][BOMBDBG] travellerCell={travellerCell} basePos={basePos}");

        var distMap = maze.GetDistancesFromCell(travellerCell);
        if (distMap == null || distMap.Count == 0)
        {
            Debug.LogWarning("[SERVER][BOMBDBG] distMap empty");
            return null;
        }

        // Collect bombs
        var candidates = new List<GameObject>();
        var seen = new HashSet<GameObject>();

        try
        {
            var tagged = GameObject.FindGameObjectsWithTag("Bomb");
            foreach (var b in tagged)
                if (b != null && seen.Add(b))
                    candidates.Add(b);
        }
        catch { }

        var pickups = FindObjectsByType<PickupObject>(FindObjectsSortMode.None);
        foreach (var p in pickups)
            if (p != null && p.type == PickupObject.PickupType.Bomb && seen.Add(p.gameObject))
                candidates.Add(p.gameObject);

        Debug.Log($"[SERVER][BOMBDBG] Bomb candidates count={candidates.Count}");
        if (candidates.Count == 0) return null;

        GameObject bestObj = null;
        int bestSteps = int.MaxValue;
        float bestEuclidSqr = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            var bomb = candidates[i];
            if (bomb == null) continue;

            // ✅ דטרמיניסטי: world->cell
            Vector3 bp = bomb.transform.position;
            Vector2Int bombCell = WorldToCell_TransformAware(maze, bp);

            if (!maze.IsCellOpen(bombCell))
            {
                // snap אם יצא תא חסום
                int r = 6;
                if (!maze.TrySnapToNearestOpenCell(bp, r, out bombCell))
                {
                    Debug.Log($"[SERVER][BOMBDBG] bomb skip snapFail name='{bomb.name}' id={bomb.GetInstanceID()} world={bp} cell={bombCell}");
                    continue;
                }
            }

            if (!distMap.TryGetValue(bombCell, out int steps))
            {
                Debug.Log($"[SERVER][BOMBDBG] bomb unreachable name='{bomb.name}' id={bomb.GetInstanceID()} world={bp} cell={bombCell}");
                continue;
            }

            float euclidSqr = (bp - basePos).sqrMagnitude;
            float euclid = Mathf.Sqrt(euclidSqr);

            Debug.Log($"[SERVER][BOMBDBG] bomb name='{bomb.name}' id={bomb.GetInstanceID()} world={bp} cell={bombCell} steps={steps} euclid={euclid:F2}");

            // ✅ בחירה לפי BFS בלבד, euclid רק לשובר שוויון
            if (steps < bestSteps || (steps == bestSteps && euclidSqr < bestEuclidSqr))
            {
                bestSteps = steps;
                bestEuclidSqr = euclidSqr;
                bestObj = bomb;
            }
        }

        Debug.Log($"[SERVER][BOMBDBG] PICK best='{(bestObj ? bestObj.name : "NULL")}' id={(bestObj ? bestObj.GetInstanceID() : 0)} steps={bestSteps} euclid={Mathf.Sqrt(bestEuclidSqr):F2}");

        pickedSteps = bestSteps;
        return bestObj;
    }
    private Transform ResolveTravellerTransformServer(MazeGenerator3D maze, GameManager gm)
    {
        // 0) מה ש-GameManager אומר
        Transform t = gm != null && gm.traveller != null ? gm.traveller.transform : null;
        if (IsUsableTravellerCandidate(maze, t))
            return t;

        // 1) חיפוש גנרי: כל אובייקט עם CharacterController + NetworkObject
        Transform best = null;
        float bestSqr = float.PositiveInfinity;

        var controllers = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        foreach (var cc in controllers)
        {
            if (cc == null) continue;

            var no = cc.GetComponent<NetworkObject>();
            if (no == null || !no.IsSpawned) continue;

            Transform tr = cc.transform;
            Vector3 p = tr.position;

            // מסננים זבל
            if (p == Vector3.zero) continue;

            float sqr = (p - maze.transform.position).sqrMagnitude;

            Debug.Log($"[SERVER][BOMBDBG] travellerCandidate name='{tr.name}' owner={no.OwnerClientId} " +
                      $"isOwner={no.IsOwner} pos={p} distToMaze={Mathf.Sqrt(sqr):F2}");

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = tr;
            }
        }

        if (best != null)
        {
            Debug.Log($"[SERVER][BOMBDBG] ResolveTravellerTransformServer -> PICK '{best.name}' pos={best.position} distToMaze={Mathf.Sqrt(bestSqr):F2}");
            return best;
        }

        // 2) fallback אחרון (מה שיש)
        Debug.LogWarning("[SERVER][BOMBDBG] ResolveTravellerTransformServer -> NO candidate found, fallback to gm.traveller (may be null)");
        return t;
    }

    private bool IsUsableTravellerCandidate(MazeGenerator3D maze, Transform t)
    {
        if (maze == null || t == null) return false;
        if (t.position == Vector3.zero) return false;

        // אם הוא ממש רחוק מהמבוך — כנראה זה הנווט/דמה
        float d = Vector3.Distance(t.position, maze.transform.position);
        return d < 200f;
    }


    // מחשב רדיוס Snap דטרמיניסטי לפי גודל המודל (לא ניסוי וטעייה)
    private int ComputeDeterministicSnapRadiusFromModel(MazeGenerator3D maze, GameObject go)
    {
        if (!GridCellAssignment.TryGetModelWorldBounds(go, out var b))
            return 2; // fallback קטן

        float cs = maze.CellSize;
        float rx = b.extents.x / cs;
        float rz = b.extents.z / cs;

        int r = Mathf.CeilToInt(Mathf.Max(rx, rz)) + 1;
        return Mathf.Clamp(r, 1, 12);
    }

    // (ישן) Euclid בלבד — נשאר אם תרצה להשוות
    private GameObject FindClosestBomb(Vector3 origin, float maxRange)
    {
        GameObject closest = null;
        float best = Mathf.Infinity;

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[SERVER] Scanning bombs...");

        GameObject[] tagged = GameObject.FindGameObjectsWithTag("Bomb");
        foreach (var b in tagged)
        {
            if (b == null) continue;
            float d = Vector3.Distance(origin, b.transform.position);
            if (d < best && d <= maxRange)
            {
                best = d;
                closest = b;
            }
        }

        var pickups = FindObjectsByType<PickupObject>(FindObjectsSortMode.None);
        foreach (var p in pickups)
        {
            if (p == null) continue;
            if (p.type != PickupObject.PickupType.Bomb) continue;

            float d = Vector3.Distance(origin, p.transform.position);
            if (d < best && d <= maxRange)
            {
                best = d;
                closest = p.gameObject;
            }
        }

        if (closest == null)
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "[SERVER] No bomb found");
        else
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"[SERVER] Closest bomb = {closest.name}");

        return closest;
    }

    // ============================================================
    // HEART LOGIC
    // ============================================================

    private void ServerPlaceHeart()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.traveller == null)
            return;

        if (gm.HeartPlacements <= 0)
        {
            NavNoHeartsLeftRpc();
            return;
        }

        Vector3 pos = gm.traveller.transform.position + gm.traveller.transform.forward * 1f;

        GameObject h = Instantiate(heartPrefab, pos, Quaternion.identity);
        NetworkObject no = h.GetComponent<NetworkObject>();
        if (no != null)
            no.Spawn();

        gm.HeartPlacements--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);

        var tm = EnsureTutorial();
        if (tm != null)
            tm.NotifyNavigatorPlacedHeart();
    }

    // ============================================================
    // LIFEBOUY LOGIC
    // ============================================================

    private void ServerUseLifebuoy()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return;

        if (gm.lifebuoys <= 0)
        {
            NavNoLifebuoysRpc();
            return;
        }

        if (!gm.inPuzzle || gm.activePuzzleDoor == null)
        {
            NavLifebuoyOnlyInPuzzleRpc();
            return;
        }

        var tm = EnsureTutorial();
        if (tm != null)
            tm.NotifyNavigatorGaveLifebuoy();

        gm.activePuzzleDoor?.GetPuzzle()?.RevealRandomHint();
        RevealHintClientRpc();

        if (gm != null && gm.activePuzzleDoor != null)
        {
            var pad = gm.activePuzzleDoor.GetComponentInChildren<PadTrigger>(true);
            if (pad != null)
                pad.NotifyHintUsed_Server();
        }

        gm.lifebuoys--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);
    }
    private void ServerRemoveBomb()
    {
        Debug.Log($"[SERVER][BOMBDBG] ServerRemoveBomb frame={Time.frameCount} time={Time.time:F3}");

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.traveller == null)
        {
            Debug.LogWarning("[SERVER] Traveller missing");
            NavNoTravellerRpc();
            return;
        }

        if (gm.BombRemovals <= 0)
        {
            Debug.LogWarning("[SERVER] No BombRemovals left");
            NavNoBombAttemptsRpc();
            return;
        }

        var maze = Object.FindFirstObjectByType<MazeGenerator3D>();
        if (maze == null)
        {
            Debug.LogWarning("[SERVER] MazeGenerator3D not found");
            NavNoBombFoundRpc();
            return;
        }

        // ✅ במקום gm.traveller.transform ישר:
        Transform travellerTr = ResolveTravellerTransformServer(maze, gm);
        if (travellerTr == null)
        {
            Debug.LogWarning("[SERVER][BOMBDBG] travellerTr NULL even after resolve");
            NavNoTravellerRpc();
            return;
        }

        Vector3 travellerPos = travellerTr.position;

        // probe לרצפה (אופציונלי)
        Vector3 probe = travellerPos;
        bool hitFloor = Physics.Raycast(travellerPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 30f);
        if (hitFloor) probe = hit.point;

        Debug.Log($"[SERVER][BOMBDBG] travellerTr='{travellerTr.name}' travellerPos={travellerPos} probe={probe} hitFloor={hitFloor}");

        int pickedSteps;
        GameObject bombObj = FindClosestBombByGridPath(maze, travellerPos, probe, out pickedSteps);

        if (bombObj == null)
        {
            Debug.LogWarning("[SERVER] No bomb found near traveller");
            NavNoBombFoundRpc();
            return;
        }

        // ✅ MAX STEPS LIMIT
        if (pickedSteps > maxBombRemoveSteps)
        {
            Debug.LogWarning($"[SERVER][BOMBDBG] DENY remove: steps={pickedSteps} > max={maxBombRemoveSteps}");
            // אם אין לך RPC ייעודי, אפשר להשאיר את זה ככה (רק לא להסיר).
            // אם תרצה הודעה נפרדת ב-HUD לנווט, תגיד לי איך אתה מציג הודעות ואני אתן RPC קטן.
            return;
        }

        Debug.Log($"[SERVER][BOMBDBG] REMOVING bomb='{bombObj.name}' id={bombObj.GetInstanceID()} steps={pickedSteps} pos={bombObj.transform.position}");
        NavSetBombSpotlightClientRpc(false, MakeAllNonServerClientsTargetParams());

        NetworkObject no = bombObj.GetComponent<NetworkObject>();
        if (no != null)
            no.Despawn(true);
        else
            Destroy(bombObj);

        gm.BombRemovals--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);

        var tm = EnsureTutorial();
        if (tm != null)
            tm.NotifyNavigatorRemovedBomb();
    }


    // ============================================================
    // RPC – CLIENT → SERVER
    // ============================================================

    [ServerRpc(RequireOwnership = false)]
    private void RequestRemoveBombServerRpc()
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[SERVER] RequestRemoveBombRpc received (1 call)");
        ServerRemoveBomb();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RevealHintClientRpc()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return;

        if (gm.traveller != null && gm.traveller.GetComponent<NetworkObject>().IsOwner)
        {
            gm.activePuzzleDoor?.GetPuzzle()?.RevealRandomHint();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceHeartServerRpc()
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[SERVER] RequestPlaceHeartRpc received");
        ServerPlaceHeart();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestUseLifebuoyServerRpc()
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[SERVER] RequestUseLifebuoyRpc received");
        ServerUseLifebuoy();
    }

    // ============================================================
    // RPC – SERVER → CLIENTS — Resource Sync
    // ============================================================

    [Rpc(SendTo.Everyone)]
    private void SyncResourceCountsRpc(int lifebuoys, int hearts, int bombs)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return;

        gm.lifebuoys = lifebuoys;
        gm.HeartPlacements = hearts;
        gm.BombRemovals = bombs;

        HUDManager.Instance?.UpdateHUDs();
    }

    // ============================================================
    // RPC – SERVER → CLIENTS — HUD messages
    // ============================================================

    [Rpc(SendTo.Everyone)]
    private void NavNoHeartsLeftRpc() => HUDManager.Instance?.NavNoHeartsLeft();

    [Rpc(SendTo.Everyone)]
    private void NavNoTravellerRpc() => HUDManager.Instance?.NavNoTraveller();

    [Rpc(SendTo.Everyone)]
    private void NavNoBombAttemptsRpc() => HUDManager.Instance?.NavNoBombAttempts();

    [Rpc(SendTo.Everyone)]
    private void NavNoBombFoundRpc() => HUDManager.Instance?.NavNoBombFound();

    [Rpc(SendTo.Everyone)]
    private void NavNoLifebuoysRpc() => HUDManager.Instance?.NavNoLifebuoys();

    [Rpc(SendTo.Everyone)]
    private void NavLifebuoyOnlyInPuzzleRpc() => HUDManager.Instance?.NavLifebuoyOnlyInPuzzle();
}
