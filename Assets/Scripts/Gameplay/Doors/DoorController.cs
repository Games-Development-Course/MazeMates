// Assets/Scripts/Gameplay/Doors/DoorController.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;


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
    // PUZZLE (legacy prefab + new SO definition)
    // ============================================================
    [Header("Puzzle (Legacy Prefab - optional)")]
    public GameObject puzzlePrefab; // legacy (old puzzle prefab UI)
    public Sprite navigatorPreview; // used by NavigatorTVScreen

    [Header("Puzzle Definition (ScriptableObject in Resources)")]
    [SerializeField] private string puzzleDefResourcesFolder = "Puzzles";

    private readonly NetworkVariable<FixedString128Bytes> puzzleDefPath = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ✅ actual loaded puzzle definition
    public Puzzle puzzleDefinition { get; private set; }

    // ✅ COMPAT: what PuzzleDoor expects
    public Puzzle PuzzleDef => puzzleDefinition;

    // ============================================================
    // Internals
    // ============================================================
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

        // Puzzle SO replication
        puzzleDefPath.OnValueChanged += OnPuzzleDefPathChanged;
        if (!puzzleDefPath.Value.IsEmpty)
            EnsurePuzzleDefinitionLoadedFromNet(puzzleDefPath.Value);

        InitDoorLogic();
    }

    public override void OnNetworkDespawn()
    {
        puzzleDefPath.OnValueChanged -= OnPuzzleDefPathChanged;
    }

    // ============================================================
    // PUBLIC API (used by other scripts)
    // ============================================================

    // ✅ Old code expects Interact() without args
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

    // ✅ Some code expects Interact(Vector3)
    public void Interact(Vector3 openerWorldPos)
    {
        if (doorType == DoorType.Puzzle)
            return;

        RequestOpenDoorServerRpc(openerWorldPos);
    }

    public bool TravellerIsOnPad() => pad != null && pad.IsPlayerOnPad();
    public bool IsOpen() => door != null && door.IsOpen();
    public PuzzleDoor GetPuzzle() => door as PuzzleDoor;

    // (optional legacy)
    public void SetPuzzlePrefabServer(GameObject prefab)
    {
        if (!IsServer) return;
        if (prefab == null) return;

        // Legacy-only: traveller prefab puzzle UI (if you still use it)
        puzzlePrefab = prefab;

        if (navigatorPreview == null)
            navigatorPreview = ExtractPreviewFromPrefab();
    }

    // ✅ MazeGenerator calls this (server)
    public void SetPuzzleDefinitionServer(Puzzle puzzle)
    {
        if (!IsServer) return;
        if (puzzle == null) return;

        puzzleDefinition = puzzle;

        // Build Resources path: "Puzzles/Puzzle_XXX" (without extension)
        string path = string.IsNullOrWhiteSpace(puzzleDefResourcesFolder)
            ? puzzle.name
            : $"{puzzleDefResourcesFolder}/{puzzle.name}";

        puzzleDefPath.Value = path;

        // navigator preview: prefer SO originalImage if exists
        if (puzzleDefinition != null && puzzleDefinition.originalImage != null)
            navigatorPreview = puzzleDefinition.originalImage;
    }

    // ============================================================
    // Puzzle Definition load (clients)
    // ============================================================
    private void OnPuzzleDefPathChanged(FixedString128Bytes _, FixedString128Bytes newVal)
    {
        // allow reload if changed
        puzzleDefinition = null;
        EnsurePuzzleDefinitionLoadedFromNet(newVal);

        if (navigatorPreview == null && puzzleDefinition != null && puzzleDefinition.originalImage != null)
            navigatorPreview = puzzleDefinition.originalImage;
    }

    private void EnsurePuzzleDefinitionLoadedFromNet(FixedString128Bytes explicitPath)
    {
        if (puzzleDefinition != null) return;

        FixedString128Bytes pathVal = !explicitPath.IsEmpty ? explicitPath : puzzleDefPath.Value;
        if (pathVal.IsEmpty)
        {
            Debug.LogWarning("[DoorController] Puzzle definition is not set and cannot be loaded.", this);
            return;
        }

        string path = pathVal.ToString();
        var loaded = Resources.Load<Puzzle>(path);
        if (loaded == null)
        {
            Debug.LogWarning(
                $"[PuzzleDef] Resources.Load failed for path '{path}'. Put Puzzle asset under Assets/Resources/{path}.asset",
                this
            );
            return;
        }

        puzzleDefinition = loaded;
    }

    private void EnsurePuzzleDefinitionLoadedFromNet()
    {
        EnsurePuzzleDefinitionLoadedFromNet(default);
    }

    // ============================================================
    // Navigator TV helpers (uses NavigatorTVScreen in scene)
    // ============================================================
    private NavigatorTVScreen GetLocalTV()
    {
        var tv = Object.FindFirstObjectByType<NavigatorTVScreen>(FindObjectsInactive.Include);
        if (tv == null)
        {
            Debug.LogWarning(
                $"[PUZZLE-TV] No NavigatorTVScreen found | IsServer={IsServer} IsClient={IsClient} LocalId={NetworkManager.Singleton?.LocalClientId}",
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

    public void ShowNavigatorPreviewOnScreen(Sprite sprite)
    {
        navigatorPreview = sprite;

        bool showPuzzle = sprite != null && sprite.texture != null;

        if (!IsServer)
            RequestSetNavigatorScreenServerRpc(showPuzzle);
        else
            SetNavigatorScreenClientRpc(showPuzzle);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetNavigatorScreenServerRpc(bool showPuzzle)
    {
        SetNavigatorScreenClientRpc(showPuzzle);
    }

    [Rpc(SendTo.Everyone)]
    private void SetNavigatorScreenClientRpc(bool showPuzzle)
    {
        if (!showPuzzle)
        {
            ClearNavigatorScreen();
            return;
        }

        if (_tvApplyRoutine != null)
            StopCoroutine(_tvApplyRoutine);

        _tvApplyRoutine = StartCoroutine(ApplyPuzzleToTVRoutine());
    }

    private IEnumerator ApplyPuzzleToTVRoutine()
    {
        for (int i = 0; i < tvApplyRetries; i++)
        {
            // Prefer SO image
            if (navigatorPreview == null)
            {
                EnsurePuzzleDefinitionLoadedFromNet();
                if (puzzleDefinition != null && puzzleDefinition.originalImage != null)
                    navigatorPreview = puzzleDefinition.originalImage;
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

        Debug.LogWarning($"[DoorController] Cannot apply puzzle texture — no valid sprite after retries. door='{name}'", this);
        _tvApplyRoutine = null;
    }

    // ============================================================
    // Door logic init
    // ============================================================
    private void InitDoorLogic()
    {
        switch (doorType)
        {
            case DoorType.Puzzle:
                // Make sure we have a definition if replicated
                EnsurePuzzleDefinitionLoadedFromNet();

                if (navigatorPreview == null && puzzleDefinition != null && puzzleDefinition.originalImage != null)
                    navigatorPreview = puzzleDefinition.originalImage;

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

    private Sprite ExtractPreviewFromPrefab()
    {
        if (puzzleDefinition != null && puzzleDefinition.originalImage != null)
            return puzzleDefinition.originalImage;

        if (puzzlePrefab == null) return null;

        var img = puzzlePrefab.GetComponentInChildren<Image>(true);
        if (img != null && img.sprite != null)
            return img.sprite;

        return null;
    }

    // ============================================================
    // Pivot / Colliders
    // ============================================================
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

    private void EnsurePivot()
    {
        if (doorType == DoorType.Exit && exitDoorPivotOverride != null)
        {
            pivot = exitDoorPivotOverride;
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

    // ============================================================
    // Opening logic (Normal/Exit)
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

        CacheClosedRotationsIfNeeded();
        Quaternion target = _closedPivotLocalRot * Quaternion.Euler(0f, angle, 0f);

        while (Quaternion.Angle(pivot.localRotation, target) > 0.1f)
        {
            pivot.localRotation = Quaternion.Lerp(pivot.localRotation, target, Time.deltaTime * openSpeed);
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
            pivot.localRotation = Quaternion.Lerp(pivot.localRotation, target, Time.deltaTime * openSpeed);
            yield return null;
        }

        pivot.localRotation = target;
        DisablePadColliderAfterOpen();
    }

    private IEnumerator ExitCinematicOpenRoutine(float chosenAngle)
    {
        var sfx = GetComponent<DoorOpenSfx>();
        if (sfx != null) sfx.TriggerOpenSfxOnce();
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
    // RPC — OPEN NORMAL/EXIT DOOR
    // ============================================================
    [ServerRpc(RequireOwnership = false)]
    public void RequestOpenDoorServerRpc(Vector3 openerWorldPos)
    {
        if (!IsServer) return;

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

        if (doorType == DoorType.Exit)
        {
            float chosenExit = exitOpenAngle;
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
        StartCoroutine(ExitCinematicOpenThenNotifyRoutine(chosenAngle));
    }

    private IEnumerator ExitCinematicOpenThenNotifyRoutine(float chosenAngle)
    {
        yield return ExitCinematicOpenRoutine(chosenAngle);

        if (!IsServer) yield break;

        var gm = GameManager.Instance;
        if (gm != null) gm.EndLevel();
        else Debug.LogWarning("[DoorController] Exit opened but GameManager.Instance is null; cannot notify level end.");
    }

    // ============================================================
    // PUZZLE OPEN (navigator TV + traveller open)
    // ============================================================
    [ServerRpc(RequireOwnership = false)]
    public void RequestOpenPuzzleDoorServerRpc()
    {
        if (!IsServer) return;

        if (pad == null) pad = GetComponentInChildren<PadTrigger>(true);
        if (pad != null)
            pad.NotifyPuzzleStarted_Server();

        EnsurePuzzleDefinitionLoadedFromNet();

        if (navigatorPreview == null && puzzleDefinition != null && puzzleDefinition.originalImage != null)
            navigatorPreview = puzzleDefinition.originalImage;

        bool canShow = navigatorPreview != null && navigatorPreview.texture != null;
        SetNavigatorScreenClientRpc(canShow);

        OpenPuzzleForTravellerClientRpc(NetworkObjectId);
    }

    [Rpc(SendTo.Everyone)]
    private void OpenPuzzleForTravellerClientRpc(ulong doorId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(doorId, out NetworkObject obj))
            return;

        DoorController door = obj.GetComponent<DoorController>();
        if (door == null) return;

        var gm = GameManager.Instance;
        if (gm == null || gm.traveller == null) return;

        var travellerNet = gm.traveller.GetComponent<NetworkObject>();
        if (travellerNet == null) return;

        if (travellerNet.IsOwner)
        {
            door.EnsurePuzzleDefinitionLoadedFromNet();
            door.GetPuzzle()?.TryOpen();
        }
    }

    [ClientRpc]
    private void SetNavigatorHintSpotlightTargetClientRpc(bool on, ClientRpcParams rpcParams = default)
    {
        NavigatorSpotlights.I?.SetHintReady(on);
    }
}
