// File: Assets/Scripts/Gameplay/Doors/DoorController.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class DoorController : NetworkBehaviour
{
    [Header("Door Settings")]
    public DoorType doorType;
    public float openAngle = 90f;
    public float openSpeed = 3f;

    private Coroutine _hintReminderCo;
    private bool _puzzleActiveServer;

    [Header("Pivot (Optional Override)")]
    [SerializeField] private Transform pivotOverride;

    // ============================================================
    // EXIT DOOR (CINEMATIC) — ONLY USED WHEN doorType == Exit
    // (UNCHANGED - per your request)
    // ============================================================
    [Header("Exit Door (Cinematic)")]
    [SerializeField] private Transform exitCinematicSpinTarget;
    [SerializeField] private Transform exitDoorPivotOverride;
    [SerializeField] private float exitSpinDegrees = 90f;
    [SerializeField] private float exitSpinDuration = 1.25f;

    [Header("Puzzle Settings")]
    public GameObject puzzlePrefab;
    public Sprite navigatorPreview; // auto-filled from OriginalImage if null

    [Header("Puzzle Prefab Replication (Resources)")]
    [SerializeField] private string puzzleResourcesFolder = "Puzzles";

    private readonly NetworkVariable<FixedString128Bytes> puzzlePrefabPath =
        new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public List<GameObject> spawnedHints = new List<GameObject>();

    private Transform pivot;
    private IDoor door;

    // --- Closed rotation caches ---
    // Local (used by Exit logic - keep as-is)
    private Quaternion _closedPivotLocalRot;

    // World (used by NORMAL door logic to guarantee Y-world rotation)
    private Quaternion _closedPivotWorldRot;
    private bool _closedPivotCached;

    private PadTrigger pad;

    private Coroutine _tvApplyRoutine;

    [Header("Puzzle-TV Robustness")]
    [SerializeField] private int tvApplyRetries = 90;               // ~1.5s @ 60fps
    [SerializeField] private float tvApplyRetryDelaySeconds = 0f;   // 0 = next frame

    // ============================================================
    // OPEN DIRECTION (ROBUST)
    // ============================================================
    private Transform _doorModelForSign;

    // Prevent multiple open coroutines fighting each other (NORMAL doors only)
    private Coroutine _normalOpenCo;

    private void Awake()
    {
        // Intentionally empty (TV handled elsewhere)
    }

    public override void OnNetworkSpawn()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[DOOR SPAWN] {name} | NetId={NetworkObjectId}"
        );

        pad = GetComponentInChildren<PadTrigger>();

        if (pivot == null)
            FindOrCreatePivot();

        CacheClosedRotationsIfNeeded();

        puzzlePrefabPath.OnValueChanged += OnPuzzlePrefabPathChanged;

        if (!puzzlePrefabPath.Value.IsEmpty)
            EnsurePuzzleLoadedFromNet(puzzlePrefabPath.Value);

        InitDoorLogic();
    }

    public override void OnNetworkDespawn()
    {
        puzzlePrefabPath.OnValueChanged -= OnPuzzlePrefabPathChanged;
    }

    private void CacheClosedRotationsIfNeeded()
    {
        if (_closedPivotCached) return;
        if (pivot == null) return;

        // Cache BOTH local+world. Exit uses local in your existing flow.
        _closedPivotLocalRot = pivot.localRotation;
        _closedPivotWorldRot = pivot.rotation;

        _closedPivotCached = true;
    }

    private void OnPuzzlePrefabPathChanged(FixedString128Bytes _, FixedString128Bytes newVal)
    {
        EnsurePuzzleLoadedFromNet(newVal);

        if (doorType == DoorType.Puzzle && navigatorPreview == null)
            navigatorPreview = ExtractPreviewFromPrefab();
    }

    private void StopHintReminder_Server()
    {
        _puzzleActiveServer = false;

        if (_hintReminderCo != null)
        {
            StopCoroutine(_hintReminderCo);
            _hintReminderCo = null;
        }

        // נכבה רמז בניקיון
        SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
    }

    // ---------------------------
    // Server API: set puzzle prefab + replicate
    // ---------------------------
    public void SetPuzzlePrefabServer(GameObject prefab)
    {
        if (!IsServer) return;
        if (prefab == null) return;

        puzzlePrefab = prefab;

        string path = string.IsNullOrWhiteSpace(puzzleResourcesFolder)
            ? prefab.name
            : $"{puzzleResourcesFolder}/{prefab.name}";

        puzzlePrefabPath.Value = path;

        if (navigatorPreview == null)
            navigatorPreview = ExtractPreviewFromPrefab();
    }

    // ============================================================
    // TV ACCESS (per-world, NO static)
    // ============================================================
    private NavigatorTVScreen GetLocalTV()
    {
        var tv = Object.FindFirstObjectByType<NavigatorTVScreen>(FindObjectsInactive.Include);

        if (tv == null)
        {
            Debug.LogWarning(
                $"[PUZZLE-TV] No NavigatorTVScreen found in this world | IsServer={IsServer} IsClient={IsClient} LocalId={NetworkManager.Singleton?.LocalClientId}",
                this
            );
        }

        return tv;
    }

    private void ApplyTextureToNavigatorScreenSlot(Texture tex)
    {
        var tv = GetLocalTV();
        if (tv == null) return;
        tv.Apply(tex);
    }

    private void ClearNavigatorScreen()
    {
        var tv = GetLocalTV();
        if (tv == null) return;
        tv.Clear();
    }

    // ============================================================
    // DOOR INITIALIZATION
    // ============================================================
    private void InitDoorLogic()
    {
        switch (doorType)
        {
            case DoorType.Puzzle:
                EnsurePuzzleLoadedFromNet(puzzlePrefabPath.Value);

                if (navigatorPreview == null)
                    navigatorPreview = ExtractPreviewFromPrefab();

                door = new PuzzleDoor(this);
                break;

            case DoorType.Normal:
                door = new NormalDoor(this);
                break;

            case DoorType.Exit:
                door = new ExitDoor(this);
                break;
        }
    }

    // ============================================================
    // INTERACTION
    // ============================================================

    public void Interact(Vector3 openerWorldPos)
    {
        if (doorType == DoorType.Puzzle)
            return;

        RequestOpenDoorServerRpc(openerWorldPos);
    }

    public void Interact()
    {
        if (doorType == DoorType.Puzzle)
            return;

        Vector3 openerPos = transform.position;
        var gm = GameManager.Instance;

        if (gm != null && gm.navigator != null)
            openerPos = gm.navigator.transform.position;
        else if (gm != null && gm.traveller != null)
            openerPos = gm.traveller.transform.position;

        RequestOpenDoorServerRpc(openerPos);
    }

    public bool TravellerIsOnPad() => pad != null && pad.IsPlayerOnPad();
    public bool IsOpen() => door != null && door.IsOpen();
    public PuzzleDoor GetPuzzle() => door as PuzzleDoor;

    // ============================================================
    // PIVOT
    // ============================================================
    private void EnsurePivot()
    {
        // Exit door override (ONLY Exit) — DO NOT TOUCH (kept same behavior)
        if (doorType == DoorType.Exit && exitDoorPivotOverride != null)
        {
            pivot = exitDoorPivotOverride;

            // keep existing behavior; do not refactor Exit caching
            if (_closedPivotLocalRot == default)
                _closedPivotLocalRot = pivot.localRotation;

            // but still cache world for normals if ever needed elsewhere
            CacheClosedRotationsIfNeeded();
            return;
        }

        if (pivotOverride != null)
        {
            pivot = pivotOverride;
            CacheClosedRotationsIfNeeded();
            return;
        }

        if (pivot == null)
            FindOrCreatePivot();

        CacheClosedRotationsIfNeeded();
    }

    private void FindOrCreatePivot()
    {
        MeshFilter mf = GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(m => m.CompareTag("Door"));

        if (mf == null)
        {
            mf = GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(m =>
                {
                    string n = m.name.ToLowerInvariant();
                    return !n.Contains("trigger") && !n.Contains("pad");
                });
        }

        if (mf == null)
        {
            Debug.LogError($"[DoorController] No suitable MeshFilter found for pivot on '{name}'. " +
                           $"Either tag the moving mesh as 'Door' or assign Pivot Override.", this);
            return;
        }

        Transform doorModel = mf.transform;
        _doorModelForSign = doorModel;

        Bounds b = mf.sharedMesh.bounds;
        float half = b.size.x * 0.5f;

        // LEFT edge
        Vector3 leftLocal = new Vector3(b.center.x - half, b.center.y, b.center.z);
        Vector3 pivotWorld = doorModel.TransformPoint(leftLocal);

        GameObject pivotObj = new GameObject("Pivot");
        pivotObj.transform.SetParent(transform, true);

        // IMPORTANT: Keep pivot scaled clean if possible
        pivotObj.transform.localScale = Vector3.one;

        pivotObj.transform.position = pivotWorld;

        // Keep original orientation (your logic). Normal door rotation will now be WORLD-UP so it won't flip to Z.
        pivotObj.transform.rotation = doorModel.rotation;

        foreach (Transform child in transform)
        {
            if (child == pivotObj.transform)
                continue;

            string cn = child.name.ToLowerInvariant();
            if (cn.Contains("trigger") || cn.Contains("pad"))
                continue;

            child.SetParent(pivotObj.transform, true);
        }

        pivot = pivotObj.transform;
        CacheClosedRotationsIfNeeded();
    }

    // ============================================================
    // NORMAL DOOR OPEN (WORLD-Y to avoid “rotates on Z”)
    // ============================================================
    private void StartNormalOpen(float angle)
    {
        if (_normalOpenCo != null)
            StopCoroutine(_normalOpenCo);

        _normalOpenCo = StartCoroutine(NormalOpenRoutine(angle));
    }


    private IEnumerator NormalOpenRoutine(float angle)
    {
        EnsurePivot();
        if (pivot == null)
        {
            Debug.LogError($"[DoorController] NormalOpenRoutine aborted: pivot is NULL on '{name}'.", this);
            yield break;
        }

        // cache closed LOCAL rotation once
        if (!_closedPivotCached)
        {
            _closedPivotLocalRot = pivot.localRotation;
            _closedPivotCached = true;
        }

        Quaternion target = _closedPivotLocalRot * Quaternion.Euler(0f, 0f, angle);

        while (Quaternion.Angle(pivot.localRotation, target) > 0.1f)
        {
            pivot.localRotation = Quaternion.Lerp(
                pivot.localRotation,
                target,
                Time.deltaTime * openSpeed
            );
            yield return null;
        }

        pivot.localRotation = target;
        _normalOpenCo = null;
    }

    // ============================================================
    // ORIGINAL OPEN ROUTINE (USED BY EXIT DOOR CINEMATIC) — UNCHANGED
    // ============================================================
    private IEnumerator OpenRoutine(float angle)
    {
        EnsurePivot();
        if (pivot == null)
        {
            Debug.LogError($"[DoorController] OpenRoutine aborted: pivot is NULL on '{name}'.", this);
            yield break;
        }

        Quaternion target = _closedPivotLocalRot * Quaternion.Euler(0f, 0f, angle);

        while (Quaternion.Angle(pivot.localRotation, target) > 0.1f)
        {
            pivot.localRotation = Quaternion.Lerp(
                pivot.localRotation,
                target,
                Time.deltaTime * openSpeed
            );
            yield return null;
        }

        pivot.localRotation = target;
    }

    // ============================================================
    // EXIT DOOR CINEMATIC — UNCHANGED
    // ============================================================
    private IEnumerator ExitCinematicOpenRoutine()
    {
        if (exitCinematicSpinTarget != null && exitSpinDuration > 0f && Mathf.Abs(exitSpinDegrees) > 0.01f)
        {
            Quaternion start = exitCinematicSpinTarget.localRotation;
            Quaternion end = start * Quaternion.Euler(0f, 0f, exitSpinDegrees);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, exitSpinDuration);
                float s = Smooth01(t);
                exitCinematicSpinTarget.localRotation = Quaternion.Slerp(start, end, s);
                yield return null;
            }

            exitCinematicSpinTarget.localRotation = end;
        }

        yield return OpenRoutine(90f);
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // ============================================================
    // Choose +openAngle / -openAngle so the DOOR LEAF opens AWAY
    // (Adjusted to also test WORLD-Y rotation to match new normal open)
    // ============================================================
    private float ChooseOpenAngleSign(Vector3 openerWorldPos)
    {
        EnsurePivot();
        if (pivot == null) return openAngle;

        if (_doorModelForSign == null)
            _doorModelForSign = (pivot.childCount > 0) ? pivot.GetChild(0) : pivot;

        var mf = _doorModelForSign.GetComponentInChildren<MeshFilter>(true);
        if (mf == null || mf.sharedMesh == null)
            return openAngle;

        Bounds b = mf.sharedMesh.bounds;
        Vector3 c = b.center;
        Vector3 e = b.extents;

        Vector3[] corners =
        {
            c + new Vector3(+e.x, +e.y, +e.z),
            c + new Vector3(+e.x, +e.y, -e.z),
            c + new Vector3(+e.x, -e.y, +e.z),
            c + new Vector3(+e.x, -e.y, -e.z),
            c + new Vector3(-e.x, +e.y, +e.z),
            c + new Vector3(-e.x, +e.y, -e.z),
            c + new Vector3(-e.x, -e.y, +e.z),
            c + new Vector3(-e.x, -e.y, -e.z),
        };

        Quaternion savedWorld = pivot.rotation;
        CacheClosedRotationsIfNeeded();
        Quaternion baseWorld = _closedPivotWorldRot;

        float Score(float angle)
        {
            pivot.rotation = baseWorld * Quaternion.AngleAxis(angle, Vector3.forward);

            float sum = 0f;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 wp = mf.transform.TransformPoint(corners[i]);
                sum += (wp - openerWorldPos).sqrMagnitude;
            }
            return sum / corners.Length;
        }

        float sPlus = Score(+openAngle);
        float sMinus = Score(-openAngle);

        pivot.rotation = savedWorld;

        return (sPlus >= sMinus) ? +openAngle : -openAngle;
    }

    // ============================================================
    // PUZZLE PREFAB LOAD (CLIENT)
    // ============================================================
    private void EnsurePuzzleLoadedFromNet(FixedString128Bytes explicitPath)
    {
        if (puzzlePrefab != null)
            return;

        FixedString128Bytes pathVal = !explicitPath.IsEmpty ? explicitPath : puzzlePrefabPath.Value;
        if (pathVal.IsEmpty)
            return;

        string path = pathVal.ToString();
        var loaded = Resources.Load<GameObject>(path);

        if (loaded == null)
        {
            Debug.LogWarning($"[PUZZLE-TV] Resources.Load failed for path '{path}'. " +
                             $"Put prefab under Assets/Resources/{path}.prefab", this);
            return;
        }

        puzzlePrefab = loaded;
    }

    private void EnsurePuzzleLoadedFromNet()
    {
        EnsurePuzzleLoadedFromNet(default);
    }

    private Sprite ExtractPreviewFromPrefab()
    {
        EnsurePuzzleLoadedFromNet();

        if (puzzlePrefab == null)
            return null;

        Transform original = puzzlePrefab.transform.Find("OriginalImage");
        if (original == null)
            return null;

        var img = original.GetComponentInChildren<UnityEngine.UI.Image>();
        return (img != null) ? img.sprite : null;
    }

    // ============================================================
    // RPC — OPEN NORMAL/EXIT DOOR
    // ============================================================
    [ServerRpc(RequireOwnership = false)]
    public void RequestOpenDoorServerRpc(Vector3 openerWorldPos)
    {
        if (!IsServer)
            return;

        var tutorial = FindAnyObjectByType<TutorialManager>();

        if (tutorial != null && pad != null && pad.IsPlayerOnPad())
        {
            if (doorType == DoorType.Normal)
                tutorial.NotifyNavigatorOpenedNormalDoor();
            else if (doorType == DoorType.Exit)
                tutorial.NotifyNavigatorOpenedExitDoor();
        }

        if (pad == null) pad = GetComponentInChildren<PadTrigger>(true);
        if (pad != null)
            pad.NotifyDoorActionStartedOrOpened_Server();

        if (doorType == DoorType.Exit)
        {
            // UNCHANGED (your flawless flow)
            StartCoroutine(ExitCinematicOpenRoutine());
            OpenExitDoorCinematicRpc();
            return;
        }

        float chosen = ChooseOpenAngleSign(openerWorldPos);
        OpenDoorRpc(chosen);
    }

    public void ForceHideHintSpotlight_Server()
    {
        if (!IsServer) return;
        SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
    }

    [Rpc(SendTo.Everyone)]
    private void OpenDoorRpc(float chosenAngle)
    {
        // Normal doors: world-Y open, no Z-axis surprises
        if (doorType != DoorType.Exit)
            StartNormalOpen(chosenAngle);
        else
            StartCoroutine(OpenRoutine(chosenAngle)); // not expected in your flow
    }

    [Rpc(SendTo.Everyone)]
    private void OpenExitDoorCinematicRpc()
    {
        // Play the cinematic locally, then notify local GameManager that level ended
        StartCoroutine(ExitCinematicOpenThenNotifyRoutine());
    }

    private IEnumerator ExitCinematicOpenThenNotifyRoutine()
    {
        yield return ExitCinematicOpenRoutine();

        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.EndLevel();
        }
        else
        {
            Debug.LogWarning("[DoorController] Exit opened but GameManager.Instance is null; cannot notify level end.");
        }
    }

    // ============================================================
    // PUBLIC API FOR PUZZLE-TV
    // ============================================================
    public void ShowNavigatorPreviewOnScreen(Sprite sprite)
    {
        navigatorPreview = sprite;

        bool showPuzzle = sprite != null && sprite.texture != null;

        if (!IsServer)
            RequestSetNavigatorScreenServerRpc(showPuzzle);
        else
            SetNavigatorScreenClientRpc(showPuzzle, puzzlePrefabPath.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetNavigatorScreenServerRpc(bool showPuzzle)
    {
        SetNavigatorScreenClientRpc(showPuzzle, puzzlePrefabPath.Value);
    }

    [Rpc(SendTo.Everyone)]
    private void SetNavigatorScreenClientRpc(bool showPuzzle, FixedString128Bytes prefabPath)
    {
        if (!showPuzzle)
        {
            ClearNavigatorScreen();
            return;
        }

        if (_tvApplyRoutine != null)
            StopCoroutine(_tvApplyRoutine);

        _tvApplyRoutine = StartCoroutine(ApplyPuzzleToTVRoutine(prefabPath));
    }

    private IEnumerator ApplyPuzzleToTVRoutine(FixedString128Bytes prefabPath)
    {
        for (int i = 0; i < tvApplyRetries; i++)
        {
            EnsurePuzzleLoadedFromNet(prefabPath);

            if (navigatorPreview == null || navigatorPreview.texture == null)
            {
                var extracted = ExtractPreviewFromPrefab();
                if (extracted != null)
                    navigatorPreview = extracted;
            }

            if (navigatorPreview != null && navigatorPreview.texture != null)
            {
                ApplyTextureToNavigatorScreenSlot(navigatorPreview.texture);

                Debug.LogFormat(
                    LogType.Log,
                    LogOption.NoStacktrace,
                    null,
                    $"[DoorController] Applied puzzle texture '{navigatorPreview.name}' on '{name}' (retries={i})"
                );

                _tvApplyRoutine = null;
                yield break;
            }

            if (tvApplyRetryDelaySeconds > 0f)
                yield return new WaitForSeconds(tvApplyRetryDelaySeconds);
            else
                yield return null;
        }

        Debug.LogFormat(
            LogType.Warning,
            LogOption.NoStacktrace,
            null,
            $"[DoorController] Cannot apply puzzle texture — no valid sprite on {name} after retries. path='{prefabPath.ToString()}'"
        );

        _tvApplyRoutine = null;
    }

    // ============================================================
    // PUZZLE OPEN — RPC
    // ============================================================
    [ServerRpc(RequireOwnership = false)]
    public void RequestOpenPuzzleDoorServerRpc()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[PUZZLE-RPC] RequestOpenPuzzleDoorRpc CALLED on {(IsServer ? "SERVER" : "CLIENT")} | door={name}"
        );

        if (!IsServer)
            return;

        if (pad == null) pad = GetComponentInChildren<PadTrigger>(true);
        if (pad != null)
            pad.NotifyPuzzleStarted_Server();

        EnsurePuzzleLoadedFromNet(puzzlePrefabPath.Value);

        if (navigatorPreview == null)
            navigatorPreview = ExtractPreviewFromPrefab();

        bool canShow = navigatorPreview != null && navigatorPreview.texture != null;
        SetNavigatorScreenClientRpc(canShow, puzzlePrefabPath.Value);

        OpenPuzzleForTravellerClientRpc(NetworkObjectId);
    }

    [Rpc(SendTo.Everyone)]
    private void OpenPuzzleForTravellerClientRpc(ulong doorId)
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[PUZZLE-RPC] OpenPuzzleForTravellerClientRpc on {(IsServer ? "SERVER" : "CLIENT")} | doorId={doorId}"
        );

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(doorId, out NetworkObject obj))
        {
            Debug.LogFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                null,
                $"[PUZZLE-RPC] doorId {doorId} not found in SpawnedObjects"
            );
            return;
        }

        DoorController door = obj.GetComponent<DoorController>();
        if (door == null)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "[PUZZLE-RPC] NetworkObject has no DoorController");
            return;
        }

        var gm = GameManager.Instance;
        if (gm == null || gm.traveller == null)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "[PUZZLE-RPC] traveller uninitialized");
            return;
        }

        var travellerNet = gm.traveller.GetComponent<NetworkObject>();
        if (travellerNet == null)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "[PUZZLE-RPC] traveller has no NetworkObject");
            return;
        }

        if (travellerNet.IsOwner)
        {
            door.EnsurePuzzleLoadedFromNet();

            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[PUZZLE-RPC] Traveller owns this client — opening puzzle");
            door.GetPuzzle()?.TryOpen();
        }
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

    [ClientRpc]
    private void SetNavigatorHintSpotlightTargetClientRpc(bool on, ClientRpcParams rpcParams = default)
    {
        NavigatorSpotlights.I?.SetHintReady(on);
    }
}
