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

    [Tooltip("NORMAL/Puzzle magnitude in degrees. Keep POSITIVE (e.g. 90). Code chooses +/- automatically.")]
    public float openAngle = 90f;

    public float openSpeed = 3f;

    private Coroutine _hintReminderCo;
    private bool _puzzleActiveServer;

    [Header("Disable On Open")]
    [Tooltip("A NON-trigger collider on the door leaf/body (can be disabled to prevent spam).")]
    [SerializeField] private Collider doorCollider;

    [Tooltip("The PAD trigger collider (disabled AFTER door fully opens).")]
    [SerializeField] private Collider doorPadCollider;

    [Header("Pivot (Optional Override)")]
    [Tooltip("Optional hinge pivot (door leaf should be child). Leave empty to auto-create pivot.")]
    [SerializeField] private Transform pivotOverride;

    // ============================================================
    // EXIT DOOR (CINEMATIC) — ONLY USED WHEN doorType == Exit
    // ============================================================
    [Header("Exit Door (Manual)")]
    [Tooltip("Optional: a separate object to spin (cosmetic). If null, skip spin.")]
    [SerializeField] private Transform exitCinematicSpinTarget;

    [Tooltip("Optional: hinge pivot for Exit door. If set, it overrides pivotOverride for Exit doors.")]
    [SerializeField] private Transform exitDoorPivotOverride;

    [Tooltip("Manual open angle for Exit door. SIGN MATTERS: +90 or -90 (your choice).")]
    [SerializeField] private float exitOpenAngle = 90f;

    [SerializeField] private float exitSpinDegrees = 90f;
    [SerializeField] private float exitSpinDuration = 1.25f;

    // ============================================================
    // PUZZLE
    // ============================================================
    [Header("Puzzle Settings")]
    [Tooltip("Runtime-loaded puzzle prefab (Traveller side). Set via SetPuzzlePrefabServer().")]
    public GameObject puzzlePrefab;

    [Tooltip("Navigator preview sprite. Prefer Puzzle.originalImage; fallback to prefab OriginalImage child.")]
    public Sprite navigatorPreview;

    [Header("Resources Folders")]
    [Tooltip("Resources folder for Puzzle ScriptableObjects (e.g. Assets/Resources/Puzzles/...).")]
    [SerializeField] private string puzzleDefsResourcesFolder = "Puzzles";

    [Tooltip("Resources folder for Puzzle prefabs (e.g. Assets/Resources/Puzzles/...).")]
    [SerializeField] private string puzzlePrefabsResourcesFolder = "Puzzles";

    // --- Replication: Puzzle SO path (authoritative on server) ---
    private readonly NetworkVariable<FixedString128Bytes> puzzleDefPath =
        new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Puzzle puzzleDef;

    // --- Replication: Puzzle prefab path (Traveller puzzle prefab) ---
    private readonly NetworkVariable<FixedString128Bytes> puzzlePrefabPath =
        new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public List<GameObject> spawnedHints = new List<GameObject>();

    private Transform pivot;
    private IDoor door;

    private Transform _doorModelForSign;

    private Quaternion _closedPivotLocalRot;
    private Quaternion _closedPivotWorldRot;
    private bool _closedPivotCached;

    private PadTrigger pad;
    private Coroutine _tvApplyRoutine;

    [Header("Puzzle-TV Robustness")]
    [SerializeField] private int tvApplyRetries = 90;
    [SerializeField] private float tvApplyRetryDelaySeconds = 0f;

    private Coroutine _normalOpenCo;

    private bool _interactionDisabled;
    private bool _padDisabled;

    private void Awake()
    {
        // Intentionally empty
    }

    public override void OnNetworkSpawn()
    {
        pad = GetComponentInChildren<PadTrigger>(true);

        EnsureColliders();
        EnsurePivot();
        CacheClosedRotationsIfNeeded();

        puzzlePrefabPath.OnValueChanged += OnPuzzlePrefabPathChanged;
        puzzleDefPath.OnValueChanged += OnPuzzleDefPathChanged;

        // Load from net vars if already set (late join / host reload)
        if (!puzzlePrefabPath.Value.IsEmpty)
            EnsurePuzzlePrefabLoadedFromNet(puzzlePrefabPath.Value);

        if (!puzzleDefPath.Value.IsEmpty)
            EnsurePuzzleDefLoadedFromNet(puzzleDefPath.Value);

        // Prefer SO for preview if available
        if (doorType == DoorType.Puzzle && navigatorPreview == null)
            navigatorPreview = GetPreferredPreviewSprite();

        InitDoorLogic();
    }

    public override void OnNetworkDespawn()
    {
        puzzlePrefabPath.OnValueChanged -= OnPuzzlePrefabPathChanged;
        puzzleDefPath.OnValueChanged -= OnPuzzleDefPathChanged;
    }

    private void CacheClosedRotationsIfNeeded()
    {
        if (_closedPivotCached) return;
        if (pivot == null) return;

        _closedPivotLocalRot = pivot.localRotation;
        _closedPivotWorldRot = pivot.rotation;
        _closedPivotCached = true;
    }

    private void EnsureColliders()
    {
        if (doorCollider == null)
        {
            var cols = GetComponentsInChildren<Collider>(true);
            doorCollider = cols.FirstOrDefault(c =>
                c != null &&
                !c.isTrigger &&
                c.gameObject != null &&
                c.gameObject.name.ToLowerInvariant().Contains("door") &&
                !c.gameObject.name.ToLowerInvariant().Contains("pad")
            );

            if (doorCollider == null)
            {
                doorCollider = cols.FirstOrDefault(c =>
                    c != null &&
                    !c.isTrigger &&
                    c.gameObject != null &&
                    !c.gameObject.name.ToLowerInvariant().Contains("pad")
                );
            }
        }

        if (doorPadCollider == null)
        {
            if (pad != null)
            {
                var c = pad.GetComponent<Collider>();
                if (c != null) doorPadCollider = c;
                else doorPadCollider = pad.GetComponentInChildren<Collider>(true);
            }

            if (doorPadCollider == null)
            {
                var cols = GetComponentsInChildren<Collider>(true);
                doorPadCollider = cols.FirstOrDefault(c =>
                    c != null &&
                    c.gameObject != null &&
                    c.gameObject.name.ToLowerInvariant().Contains("pad")
                );
            }
        }
    }

    private void DisableDoorInteraction()
    {
        if (_interactionDisabled) return;
        _interactionDisabled = true;

        EnsureColliders();
        if (doorCollider != null)
            doorCollider.enabled = false;
    }

    private void DisablePadColliderAfterOpen()
    {
        if (_padDisabled) return;
        _padDisabled = true;

        EnsureColliders();
        if (doorPadCollider != null)
            doorPadCollider.enabled = false;
    }

    // ============================================================
    // PUZZLE ASSIGNMENT (SERVER)
    // ============================================================

    /// <summary>
    /// Assign the ScriptableObject puzzle (Resources path is replicated).
    /// Put assets under Assets/Resources/Puzzles/...
    /// </summary>
    public void SetPuzzleServer(Puzzle puzzle)
    {
        if (!IsServer) return;
        if (puzzle == null) return;

        puzzleDef = puzzle;

        string path = string.IsNullOrWhiteSpace(puzzleDefsResourcesFolder)
            ? puzzle.name
            : $"{puzzleDefsResourcesFolder}/{puzzle.name}";

        puzzleDefPath.Value = path;

        // Prefer SO preview for navigator
        if (navigatorPreview == null && puzzle.originalImage != null)
            navigatorPreview = puzzle.originalImage;
    }

    /// <summary>
    /// Assign the puzzle prefab used by the Traveller-side puzzle UI.
    /// Put prefabs under Assets/Resources/Puzzles/...
    /// </summary>
    public void SetPuzzlePrefabServer(GameObject prefab)
    {
        if (!IsServer) return;
        if (prefab == null) return;

        puzzlePrefab = prefab;

        string path = string.IsNullOrWhiteSpace(puzzlePrefabsResourcesFolder)
            ? prefab.name
            : $"{puzzlePrefabsResourcesFolder}/{prefab.name}";

        puzzlePrefabPath.Value = path;

        // If we still don't have preview, try extracting from prefab
        if (navigatorPreview == null)
            navigatorPreview = GetPreferredPreviewSprite();
    }

    private void OnPuzzlePrefabPathChanged(FixedString128Bytes _, FixedString128Bytes newVal)
    {
        EnsurePuzzlePrefabLoadedFromNet(newVal);

        if (doorType == DoorType.Puzzle && navigatorPreview == null)
            navigatorPreview = GetPreferredPreviewSprite();
    }

    private void OnPuzzleDefPathChanged(FixedString128Bytes _, FixedString128Bytes newVal)
    {
        EnsurePuzzleDefLoadedFromNet(newVal);

        if (doorType == DoorType.Puzzle && navigatorPreview == null)
            navigatorPreview = GetPreferredPreviewSprite();
    }

    private Sprite GetPreferredPreviewSprite()
    {
        // Prefer ScriptableObject original image
        if (puzzleDef != null && puzzleDef.originalImage != null)
            return puzzleDef.originalImage;

        // Fallback to prefab extraction
        return ExtractPreviewFromPrefab();
    }

    private void StopHintReminder_Server()
    {
        _puzzleActiveServer = false;

        if (_hintReminderCo != null)
        {
            StopCoroutine(_hintReminderCo);
            _hintReminderCo = null;
        }

        SetNavigatorHintSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
    }

    // ============================================================
    // NAVIGATOR TV SCREEN
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

    private void InitDoorLogic()
    {
        switch (doorType)
        {
            case DoorType.Puzzle:
                EnsurePuzzlePrefabLoadedFromNet(puzzlePrefabPath.Value);
                EnsurePuzzleDefLoadedFromNet(puzzleDefPath.Value);

                if (navigatorPreview == null)
                    navigatorPreview = GetPreferredPreviewSprite();

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

    private void EnsurePivot()
    {
        if (doorType == DoorType.Exit && exitDoorPivotOverride != null)
        {
            pivot = exitDoorPivotOverride;

            if (_closedPivotLocalRot == default)
                _closedPivotLocalRot = pivot.localRotation;

            CacheClosedRotationsIfNeeded();

            if (_doorModelForSign == null)
                _doorModelForSign = GetDoorModelForSign();

            return;
        }

        if (pivotOverride != null)
        {
            pivot = pivotOverride;
            CacheClosedRotationsIfNeeded();

            if (_doorModelForSign == null)
                _doorModelForSign = GetDoorModelForSign();

            return;
        }

        if (pivot == null)
            FindOrCreatePivot();

        CacheClosedRotationsIfNeeded();
    }

    private void FindOrCreatePivot()
    {
        MeshFilter mf = GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(m => m != null && m.CompareTag("Door"));

        if (mf == null)
        {
            mf = GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(m =>
                {
                    if (m == null) return false;
                    string n = m.name.ToLowerInvariant();
                    return !n.Contains("trigger") && !n.Contains("pad");
                });
        }

        if (mf == null)
        {
            Debug.LogError(
                $"[DoorController] No suitable MeshFilter found for pivot on '{name}'. Tag moving mesh as 'Door' or assign Pivot Override.",
                this
            );
            return;
        }

        Transform doorModel = mf.transform;
        _doorModelForSign = doorModel;

        Bounds b = mf.sharedMesh.bounds;
        float half = b.size.x * 0.5f;

        Vector3 leftLocal = new Vector3(b.center.x - half, b.center.y, b.center.z);
        Vector3 pivotWorld = doorModel.TransformPoint(leftLocal);

        GameObject pivotObj = new GameObject("Pivot");
        pivotObj.transform.SetParent(transform, true);

        pivotObj.transform.localScale = Vector3.one;
        pivotObj.transform.position = pivotWorld;
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

        CacheClosedRotationsIfNeeded();

        Quaternion target = _closedPivotLocalRot * Quaternion.Euler(0f, angle, 0f);

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
        DisablePadColliderAfterOpen();

        _normalOpenCo = null;
    }

    private IEnumerator OpenRoutine(float angle)
    {
        EnsurePivot();
        if (pivot == null)
        {
            Debug.LogError($"[DoorController] OpenRoutine aborted: pivot is NULL on '{name}'.", this);
            yield break;
        }

        CacheClosedRotationsIfNeeded();

        Quaternion target = _closedPivotLocalRot * Quaternion.Euler(0f, angle, 0f);

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
        DisablePadColliderAfterOpen();
    }

    private IEnumerator ExitCinematicOpenRoutine(float chosenAngle)
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

        yield return OpenRoutine(chosenAngle);
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // --------- Away-from-traveller for NORMAL doors only ----------
    private float ChooseOpenAngleSign(Vector3 travellerWorldPos)
    {
        EnsurePivot();
        if (pivot == null) return Mathf.Abs(openAngle);

        CacheClosedRotationsIfNeeded();

        float mag = Mathf.Abs(openAngle);
        if (mag < 0.0001f) return 0f;

        if (!TryGetHandleEdgeWorldPoint(out Vector3 handleWorld0))
            return mag;

        Vector3 pivotPos = pivot.position;

        Vector3 axis = pivot.up;
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.up;
        axis.Normalize();

        Vector3 r0 = handleWorld0 - pivotPos;

        Quaternion qPlus = Quaternion.AngleAxis(+mag, axis);
        Quaternion qMinus = Quaternion.AngleAxis(-mag, axis);

        Vector3 handlePlus = pivotPos + qPlus * r0;
        Vector3 handleMinus = pivotPos + qMinus * r0;

        Vector3 t = travellerWorldPos;
        t.y = handleWorld0.y;

        float dPlus = (handlePlus - t).sqrMagnitude;
        float dMinus = (handleMinus - t).sqrMagnitude;

        return (dPlus >= dMinus) ? +mag : -mag;
    }

    private Transform GetDoorModelForSign()
    {
        if (_doorModelForSign != null)
            return _doorModelForSign;

        MeshFilter mf = GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(m => m != null && m.CompareTag("Door"));

        if (mf == null)
        {
            mf = GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(m =>
                {
                    if (m == null) return false;
                    string n = m.name.ToLowerInvariant();
                    return !n.Contains("trigger") && !n.Contains("pad");
                });
        }

        _doorModelForSign = (mf != null) ? mf.transform : null;
        return _doorModelForSign;
    }

    private bool TryGetHandleEdgeWorldPoint(out Vector3 handleWorld)
    {
        handleWorld = default;

        Transform doorModel = GetDoorModelForSign();
        if (doorModel == null)
            return false;

        var mf = doorModel.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return false;

        Bounds b = mf.sharedMesh.bounds;
        float halfX = b.size.x * 0.5f;

        Vector3 handleLocal = new Vector3(b.center.x + halfX, b.center.y, b.center.z);
        handleWorld = doorModel.TransformPoint(handleLocal);
        return true;
    }

    // ============================================================
    // RESOURCES LOADING
    // ============================================================

    private void EnsurePuzzlePrefabLoadedFromNet(FixedString128Bytes explicitPath)
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
            Debug.LogWarning(
                $"[PUZZLE-PREFAB] Resources.Load failed for '{path}'. Put prefab under Assets/Resources/{path}.prefab",
                this
            );
            return;
        }

        puzzlePrefab = loaded;
    }

    private void EnsurePuzzlePrefabLoadedFromNet()
    {
        EnsurePuzzlePrefabLoadedFromNet(default);
    }

    private void EnsurePuzzleDefLoadedFromNet(FixedString128Bytes explicitPath)
    {
        if (puzzleDef != null)
            return;

        FixedString128Bytes pathVal = !explicitPath.IsEmpty ? explicitPath : puzzleDefPath.Value;
        if (pathVal.IsEmpty)
            return;

        string path = pathVal.ToString();
        var loaded = Resources.Load<Puzzle>(path);

        if (loaded == null)
        {
            Debug.LogWarning(
                $"[PUZZLE-DEF] Resources.Load failed for '{path}'. Put asset under Assets/Resources/{path}.asset",
                this
            );
            return;
        }

        puzzleDef = loaded;

        // Update preview immediately if we didn't have one
        if (navigatorPreview == null && puzzleDef.originalImage != null)
            navigatorPreview = puzzleDef.originalImage;
    }

    private void EnsurePuzzleDefLoadedFromNet()
    {
        EnsurePuzzleDefLoadedFromNet(default);
    }

    private Sprite ExtractPreviewFromPrefab()
    {
        EnsurePuzzlePrefabLoadedFromNet();

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

        if (pad == null) pad = GetComponentInChildren<PadTrigger>(true);

        var tutorial = FindAnyObjectByType<TutorialManager>();
        if (tutorial != null && pad != null && pad.IsPlayerOnPad())
        {
            if (doorType == DoorType.Normal)
                tutorial.NotifyNavigatorOpenedNormalDoor();
            else if (doorType == DoorType.Exit)
                tutorial.NotifyNavigatorOpenedExitDoor();
        }

        if (pad != null)
            pad.NotifyDoorActionStartedOrOpened_Server();

        DisableDoorInteraction();

        // NORMAL: open away from traveller
        // EXIT: manual, based on prefab setting (exitOpenAngle sign)
        if (doorType == DoorType.Exit)
        {
            float chosenExit = exitOpenAngle; // SIGN MATTERS, you decide in inspector
            OpenExitDoorCinematicRpc(chosenExit);
            return;
        }

        Vector3 travellerPos = openerWorldPos;
        var gm = GameManager.Instance;
        if (gm != null && gm.traveller != null)
            travellerPos = gm.traveller.transform.position;

        float chosen = ChooseOpenAngleSign(travellerPos);
        OpenDoorRpc(chosen);
    }

    [Rpc(SendTo.Everyone)]
    private void OpenDoorRpc(float chosenAngle)
    {
        if (doorType != DoorType.Exit)
            StartNormalOpen(chosenAngle);
        else
            StartCoroutine(OpenRoutine(chosenAngle));
    }

    [Rpc(SendTo.Everyone)]
    private void OpenExitDoorCinematicRpc(float chosenAngle)
    {
        // Run cinematic on everyone, but only the server should trigger level end logic
        StartCoroutine(ExitCinematicOpenThenNotifyRoutine(chosenAngle));
    }

    private IEnumerator ExitCinematicOpenThenNotifyRoutine(float chosenAngle)
    {
        yield return ExitCinematicOpenRoutine(chosenAngle);

        // IMPORTANT: this RPC runs on everyone, so guard the game-ending logic
        if (!IsServer) yield break;

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
    // NAVIGATOR PREVIEW SHOW/CLEAR
    // ============================================================

    public void ShowNavigatorPreviewOnScreen(Sprite sprite)
    {
        navigatorPreview = sprite;

        bool showPuzzle = (navigatorPreview != null && navigatorPreview.texture != null);

        if (!IsServer)
            RequestSetNavigatorScreenServerRpc(showPuzzle);
        else
            SetNavigatorScreenClientRpc(showPuzzle, puzzleDefPath.Value, puzzlePrefabPath.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetNavigatorScreenServerRpc(bool showPuzzle)
    {
        SetNavigatorScreenClientRpc(showPuzzle, puzzleDefPath.Value, puzzlePrefabPath.Value);
    }

    [Rpc(SendTo.Everyone)]
    private void SetNavigatorScreenClientRpc(bool showPuzzle, FixedString128Bytes defPath, FixedString128Bytes prefabPath)
    {
        if (!showPuzzle)
        {
            ClearNavigatorScreen();
            return;
        }

        if (_tvApplyRoutine != null)
            StopCoroutine(_tvApplyRoutine);

        _tvApplyRoutine = StartCoroutine(ApplyPuzzleToTVRoutine(defPath, prefabPath));
    }

    private IEnumerator ApplyPuzzleToTVRoutine(FixedString128Bytes defPath, FixedString128Bytes prefabPath)
    {
        for (int i = 0; i < tvApplyRetries; i++)
        {
            // Prefer SO
            EnsurePuzzleDefLoadedFromNet(defPath);
            if (navigatorPreview == null && puzzleDef != null && puzzleDef.originalImage != null)
                navigatorPreview = puzzleDef.originalImage;

            // Fallback to prefab extraction
            EnsurePuzzlePrefabLoadedFromNet(prefabPath);
            if (navigatorPreview == null || navigatorPreview.texture == null)
            {
                var extracted = ExtractPreviewFromPrefab();
                if (extracted != null)
                    navigatorPreview = extracted;
            }

            if (navigatorPreview != null && navigatorPreview.texture != null)
            {
                ApplyTextureToNavigatorScreenSlot(navigatorPreview.texture);
                _tvApplyRoutine = null;
                yield break;
            }

            if (tvApplyRetryDelaySeconds > 0f)
                yield return new WaitForSeconds(tvApplyRetryDelaySeconds);
            else
                yield return null;
        }

        Debug.LogWarning(
            $"[DoorController] Cannot apply puzzle texture — no valid sprite after retries. defPath='{defPath.ToString()}' prefabPath='{prefabPath.ToString()}'",
            this
        );
        _tvApplyRoutine = null;
    }

    // ============================================================
    // PUZZLE DOOR OPEN (SERVER) -> Traveller owner opens UI
    // ============================================================
    [ServerRpc(RequireOwnership = false)]
    public void RequestOpenPuzzleDoorServerRpc()
    {
        if (!IsServer)
            return;

        if (pad == null) pad = GetComponentInChildren<PadTrigger>(true);
        if (pad != null)
            pad.NotifyPuzzleStarted_Server();

        EnsurePuzzlePrefabLoadedFromNet(puzzlePrefabPath.Value);
        EnsurePuzzleDefLoadedFromNet(puzzleDefPath.Value);

        if (navigatorPreview == null)
            navigatorPreview = GetPreferredPreviewSprite();

        bool canShow = navigatorPreview != null && navigatorPreview.texture != null;
        SetNavigatorScreenClientRpc(canShow, puzzleDefPath.Value, puzzlePrefabPath.Value);

        OpenPuzzleForTravellerClientRpc(NetworkObjectId);
    }

    [Rpc(SendTo.Everyone)]
    private void OpenPuzzleForTravellerClientRpc(ulong doorId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(doorId, out NetworkObject obj))
            return;

        DoorController door = obj.GetComponent<DoorController>();
        if (door == null)
            return;

        var gm = GameManager.Instance;
        if (gm == null || gm.traveller == null)
            return;

        var travellerNet = gm.traveller.GetComponent<NetworkObject>();
        if (travellerNet == null)
            return;

        if (travellerNet.IsOwner)
        {
            door.EnsurePuzzlePrefabLoadedFromNet();
            door.EnsurePuzzleDefLoadedFromNet();
            door.GetPuzzle()?.TryOpen();
        }
    }

    // ============================================================
    // Hint spotlight control
    // ============================================================
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

