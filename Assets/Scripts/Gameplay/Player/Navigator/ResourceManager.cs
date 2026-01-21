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

    private TutorialManager tutorial;

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

    private void ServerRemoveBomb()
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[SERVER] ServerRemoveBomb called");

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.traveller == null)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "[SERVER] Traveller missing");
            NavNoTravellerRpc();
            return;
        }

        if (gm.BombRemovals <= 0)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "[SERVER] No BombRemovals left");
            NavNoBombAttemptsRpc();
            return;
        }

        Transform traveller = gm.traveller.transform;
        var maze = Object.FindFirstObjectByType<MazeGenerator3D>();
        Vector3 probe = traveller.position;

        // להוריד לרצפה עם Raycast
        if (Physics.Raycast(traveller.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 20f))
        {
            probe = hit.point;
        }

        // עכשיו עושים חיפוש לפי מיקום על הרצפה
        GameObject bombObj = FindClosestBombByGridPath(maze, probe, bombRemoveMaxSteps);

        if (bombObj == null)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "[SERVER] No bomb found near traveller");
            NavNoBombFoundRpc();
            return;
        }

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"[SERVER] Removing bomb: {bombObj.name}");
        NavSetBombSpotlightClientRpc(false, MakeAllNonServerClientsTargetParams());

        NetworkObject no = bombObj.GetComponent<NetworkObject>();
        if (no != null)
            no.Despawn(true);
        else
            Destroy(bombObj);

        gm.BombRemovals--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);

        // ✅ קריטי: אל תסמוך על cache
        var tm = EnsureTutorial();
        if (tm != null)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[ResourceManager] Notifying tutorial: NavigatorRemoveBomb");
            tm.NotifyNavigatorRemovedBomb();
        }
    }

    // ------------------------------------------------------------
    // FIND CLOSEST BOMB
    // ------------------------------------------------------------
    private GameObject FindClosestBombByGridPath(MazeGenerator3D maze, Vector3 travellerWorldPos, int maxSteps)
    {
        if (maze == null)
        {
            Debug.LogWarning("[SERVER] MazeGenerator3D not found.");
            return null;
        }

        // 1) Start cell (walkable) for traveller
        if (!maze.TryGetWalkCellFromWorld(travellerWorldPos, snapRadius: 6, out var travellerCell))
        {
            Debug.LogWarning("[SERVER] Traveller not on walkable cell (even after snap).");
            return null;
        }

        // 2) BFS distances from traveller cell
        var distMap = maze.GetDistancesFromCell(travellerCell);
        if (distMap == null || distMap.Count == 0)
        {
            Debug.LogWarning("[SERVER] distMap empty.");
            return null;
        }

        // 3) Collect bombs
        var candidates = new List<GameObject>();
        var seen = new HashSet<GameObject>();

        GameObject[] tagged;
        try { tagged = GameObject.FindGameObjectsWithTag("Bomb"); }
        catch { tagged = System.Array.Empty<GameObject>(); }

        foreach (var b in tagged)
            if (b != null && seen.Add(b)) candidates.Add(b);

        var pickups = FindObjectsByType<PickupObject>(FindObjectsSortMode.None);
        foreach (var p in pickups)
            if (p != null && p.type == PickupObject.PickupType.Bomb && seen.Add(p.gameObject))
                candidates.Add(p.gameObject);

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[SERVER] No bomb candidates found (Tag=Bomb or PickupObject type=Bomb).");
            return null;
        }

        // 4) Choose by shortest PATH distance (steps)
        GameObject bestObj = null;
        int bestSteps = int.MaxValue;
        float bestEuclidSqr = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            var bomb = candidates[i];
            if (bomb == null) continue;

            // Snap bomb to nearest walkable cell (small radius is enough)
            if (!maze.TryGetWalkCellFromWorld(bomb.transform.position, snapRadius: 2, out var bombCell))
                continue; // bomb not on/near walkable cell -> ignore

            if (!distMap.TryGetValue(bombCell, out int steps))
                continue; // not reachable in grid

            if (steps > maxSteps)
                continue; // too far

            float euclidSqr = (bomb.transform.position - travellerWorldPos).sqrMagnitude;

            if (steps < bestSteps || (steps == bestSteps && euclidSqr < bestEuclidSqr))
            {
                bestSteps = steps;
                bestEuclidSqr = euclidSqr;
                bestObj = bomb;
            }
        }

        Debug.Log($"[SERVER] Best bomb by PATH: {(bestObj ? bestObj.name : "NULL")} steps={bestSteps} max={maxSteps}");
        return bestObj;
    }


    private GameObject FindClosestBomb(Vector3 origin, float maxRange)
    {
        GameObject closest = null;
        float best = Mathf.Infinity;

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[SERVER] Scanning bombs...");

        // 1) Bomb prefabs with tag
        GameObject[] tagged = GameObject.FindGameObjectsWithTag("Bomb");
        foreach (var b in tagged)
        {
            if (b == null)
                continue;
            float d = Vector3.Distance(origin, b.transform.position);
            if (d < best && d <= maxRange)
            {
                best = d;
                closest = b;
            }
        }

        // 2) PickupObjects of type Bomb
        var pickups = FindObjectsByType<PickupObject>(FindObjectsSortMode.None);
        foreach (var p in pickups)
        {
            if (p == null)
                continue;
            if (p.type != PickupObject.PickupType.Bomb)
                continue;

            float d = Vector3.Distance(origin, p.transform.position);
            if (d < best && d <= maxRange)
            {
                best = d;
                closest = p.gameObject;
            }
        }

        if (closest == null)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "[SERVER] No bomb found");
        }
        else
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"[SERVER] Closest bomb = {closest.name}");
        }

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
            if (gm != null && gm.activePuzzleDoor != null)
            {
                var pad = gm.activePuzzleDoor.GetComponentInChildren<PadTrigger>(true);
                if (pad != null)
                    pad.NotifyHintUsed_Server();
            }

        }
        gm.lifebuoys--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);
    }

    // ============================================================
    // RPC – CLIENT → SERVER
    // ============================================================

    [ServerRpc(RequireOwnership = false)]
    private void RequestRemoveBombServerRpc()
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[SERVER] RequestRemoveBombRpc received");
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
