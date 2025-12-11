// ResourceManager.cs (Fusion 2)
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class ResourceManager : NetworkBehaviour
{
    public static ResourceManager Instance;

    [Header("Bomb Settings")]
    public float bombRemoveRange = 4f;

    [Header("Prefabs (Fusion NetworkPrefabRef)")]
    public NetworkPrefabRef heartPrefab;
    public NetworkPrefabRef lifebuoyEffectPrefab;

    private TutorialManager tutorial;

    // ============================================================
    // INITIALIZATION
    // ============================================================

    public override void Spawned()
    {
        base.Spawned();

        Instance = this;
        tutorial = FindFirstObjectByType<TutorialManager>();

        Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
            $"[ResourceManager] Spawned | StateAuthority={Object.HasStateAuthority} | RunnerMode={Runner.GameMode}");
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
        if (!Object.HasStateAuthority)
        {
            Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
                "[CLIENT] TryRemoveBomb → RequestRemoveBombRpc");
            RequestRemoveBombRpc();
            return;
        }

        ServerRemoveBomb();
    }

    public void TryPlaceHeart()
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
                "[CLIENT] TryPlaceHeart → RequestPlaceHeartRpc");
            RequestPlaceHeartRpc();
            return;
        }

        ServerPlaceHeart();
    }

    public void TryUseLifebuoy()
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
                "[CLIENT] TryUseLifebuoy → RequestUseLifebuoyRpc");
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
        Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
            "[STATE] ServerRemoveBomb called");

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.traveller == null)
        {
            Debug.LogFormat(UnityEngine.LogType.Warning, LogOption.NoStacktrace, null,
                "[STATE] Traveller missing");
            NavNoTravellerRpc();
            return;
        }

        if (gm.BombRemovals <= 0)
        {
            Debug.LogFormat(UnityEngine.LogType.Warning, LogOption.NoStacktrace, null,
                "[STATE] No BombRemovals left");
            NavNoBombAttemptsRpc();
            return;
        }

        Transform traveller = gm.traveller.transform;
        GameObject bombObj = FindClosestBomb(traveller.position, bombRemoveRange);

        if (bombObj == null)
        {
            Debug.LogFormat(UnityEngine.LogType.Warning, LogOption.NoStacktrace, null,
                "[STATE] No bomb found near traveller");
            NavNoBombFoundRpc();
            return;
        }

        Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
            $"[STATE] Removing bomb: {bombObj.name}");

        var no = bombObj.GetComponent<NetworkObject>();
        if (no != null && Runner != null)
            Runner.Despawn(no);
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

        Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
            "[STATE] Scanning bombs...");

        // 1) Bomb prefabs with tag
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

        // 2) PickupObjects of type Bomb
        var pickups = FindObjectsByType<PickupObject>(FindObjectsSortMode.None);
        foreach (var p in pickups)
        {
            if (p == null) continue;
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
            Debug.LogFormat(UnityEngine.LogType.Warning, LogOption.NoStacktrace, null,
                "[STATE] No bomb found");
        }
        else
        {
            Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
                $"[STATE] Closest bomb = {closest.name}");
        }

        return closest;
    }

    // ============================================================
    // HEART LOGIC
    // ============================================================

    private void ServerPlaceHeart()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.traveller == null || Runner == null) return;

        if (gm.HeartPlacements <= 0)
        {
            NavNoHeartsLeftRpc();
            return;
        }

        Vector3 pos = gm.traveller.transform.position +
                      gm.traveller.transform.forward * 1f;

        // Fusion spawn
        Runner.Spawn(heartPrefab, pos, Quaternion.identity, Object.StateAuthority);

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
        if (gm == null) return;

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

        // טוטוריאל
        tutorial?.NotifyNavigatorGaveLifebuoy();

        // רמז – תמיד רק על ה-StateAuthority
        gm.activePuzzleDoor?.GetPuzzle()?.RevealRandomHint();

        // שליחת רמז גם ללקוח של המטייל
        RevealHintRpc();

        // הורדת כמות
        gm.lifebuoys--;
        SyncResourceCountsRpc(gm.lifebuoys, gm.HeartPlacements, gm.BombRemovals);
    }

    // ============================================================
    // RPC – CLIENT → STATEAUTHORITY
    // ============================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestRemoveBombRpc(RpcInfo info = default)
    {
        Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
            "[STATE] RequestRemoveBombRpc received");
        ServerRemoveBomb();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestPlaceHeartRpc(RpcInfo info = default)
    {
        Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
            "[STATE] RequestPlaceHeartRpc received");
        ServerPlaceHeart();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestUseLifebuoyRpc(RpcInfo info = default)
    {
        Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
            "[STATE] RequestUseLifebuoyRpc received");
        ServerUseLifebuoy();
    }

    // ============================================================
    // RPC – STATEAUTHORITY → ALL — Hint
    // ============================================================

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RevealHintRpc(RpcInfo info = default)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        // רק המטייל צריך לראות חלקי פאזל
        if (gm.traveller != null)
        {
            var travellerNo = gm.traveller.GetComponent<NetworkObject>();
            if (travellerNo != null && travellerNo.HasInputAuthority)
            {
                gm.activePuzzleDoor?.GetPuzzle()?.RevealRandomHint();
            }
        }
    }

    // ============================================================
    // RPC – STATEAUTHORITY → ALL — Resource Sync
    // ============================================================

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SyncResourceCountsRpc(int lifebuoys, int hearts, int bombs, RpcInfo info = default)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        gm.lifebuoys = lifebuoys;
        gm.HeartPlacements = hearts;
        gm.BombRemovals = bombs;

        HUDManager.Instance?.UpdateHUDs();
    }

    // ============================================================
    // RPC – STATEAUTHORITY → ALL — HUD messages
    // ============================================================

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void NavNoHeartsLeftRpc(RpcInfo info = default)
        => HUDManager.Instance?.NavNoHeartsLeft();

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void NavNoTravellerRpc(RpcInfo info = default)
        => HUDManager.Instance?.NavNoTraveller();

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void NavNoBombAttemptsRpc(RpcInfo info = default)
        => HUDManager.Instance?.NavNoBombAttempts();

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void NavNoBombFoundRpc(RpcInfo info = default)
        => HUDManager.Instance?.NavNoBombFound();

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void NavNoLifebuoysRpc(RpcInfo info = default)
        => HUDManager.Instance?.NavNoLifebuoys();

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void NavLifebuoyOnlyInPuzzleRpc(RpcInfo info = default)
        => HUDManager.Instance?.NavLifebuoyOnlyInPuzzle();
}
