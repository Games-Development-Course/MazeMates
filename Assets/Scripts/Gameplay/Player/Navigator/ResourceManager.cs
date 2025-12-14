using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ResourceManager : NetworkBehaviour
{
    public static ResourceManager Instance;

    [Header("Bomb Settings")]
    public float bombRemoveRange = 4f;

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
        tutorial = FindFirstObjectByType<TutorialManager>();

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

    // ============================================================
    // PUBLIC API (Navigator)
    // ============================================================

    public void TryRemoveBomb()
    {
        if (!IsServer)
        {
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "[CLIENT] Sending RequestRemoveBombRpc"
            );
            RequestRemoveBombRpc();
            return;
        }

        ServerRemoveBomb();
    }

    public void TryPlaceHeart()
    {
        if (!IsServer)
        {
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "[CLIENT] Sending RequestPlaceHeartRpc"
            );
            RequestPlaceHeartRpc();
            return;
        }

        ServerPlaceHeart();
    }

    public void TryUseLifebuoy()
    {
        if (!IsServer)
        {
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "[CLIENT] Sending RequestUseLifebuoyRpc"
            );
            RequestUseLifebuoyRpc();
            return;
        }

        ServerUseLifebuoy();
    }

    // ============================================================
    // BOMB REMOVAL — SERVER LOGIC
    // ============================================================

    private void ServerRemoveBomb()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[SERVER] ServerRemoveBomb called"
        );

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.traveller == null)
        {
            Debug.LogFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                null,
                "[SERVER] Traveller missing"
            );
            NavNoTravellerRpc();
            return;
        }

        if (gm.BombRemovals <= 0)
        {
            Debug.LogFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                null,
                "[SERVER] No BombRemovals left"
            );
            NavNoBombAttemptsRpc();
            return;
        }

        Transform traveller = gm.traveller.transform;
        GameObject bombObj = FindClosestBomb(traveller.position, bombRemoveRange);

        if (bombObj == null)
        {
            Debug.LogFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                null,
                "[SERVER] No bomb found near traveller"
            );
            NavNoBombFoundRpc();
            return;
        }

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[SERVER] Removing bomb: {bombObj.name}"
        );

        NetworkObject no = bombObj.GetComponent<NetworkObject>();
        if (no != null)
            no.Despawn(true);
        else
            Destroy(bombObj);

        gm.BombRemovals--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);

        tutorial?.NotifyNavigatorRemovedBomb();
    }

    // ------------------------------------------------------------
    // FIND CLOSEST BOMB
    // ------------------------------------------------------------

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
            Debug.LogFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                null,
                "[SERVER] No bomb found"
            );
        }
        else
        {
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                $"[SERVER] Closest bomb = {closest.name}"
            );
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

        tutorial?.NotifyNavigatorPlacedHeart();
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

        // טותוריאל
        tutorial?.NotifyNavigatorGaveLifebuoy();

        // רמז – תמיד רק על השרת
        gm.activePuzzleDoor?.GetPuzzle()?.RevealRandomHint();

        // שליחת רמז גם ללקוח של המטייל
        RevealHintClientRpc();

        // הורדת כמות
        gm.lifebuoys--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);
    }

    // ============================================================
    // RPC – CLIENT → SERVER
    // ============================================================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestRemoveBombRpc()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[SERVER] RequestRemoveBombRpc received"
        );
        ServerRemoveBomb();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RevealHintClientRpc()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return;

        // רק המטייל צריך לראות חלקי פאזל
        if (gm.traveller != null && gm.traveller.GetComponent<NetworkObject>().IsOwner)
        {
            gm.activePuzzleDoor?.GetPuzzle()?.RevealRandomHint();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPlaceHeartRpc()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[SERVER] RequestPlaceHeartRpc received"
        );
        ServerPlaceHeart();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestUseLifebuoyRpc()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[SERVER] RequestUseLifebuoyRpc received"
        );
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
