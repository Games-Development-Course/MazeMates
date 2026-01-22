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

    [Header("Pivot (Optional Override)")]
    [SerializeField] private Transform pivotOverride;

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
    private Quaternion _closedPivotLocalRot;

    private PadTrigger pad;
    [SerializeField] private bool isExitDoor = false;

    private Coroutine _tvApplyRoutine;

    [Header("Puzzle-TV Robustness")]
    [SerializeField] private int tvApplyRetries = 90;               // ~1.5s @ 60fps
    [SerializeField] private float tvApplyRetryDelaySeconds = 0f;   // 0 = next frame

    // ============================================================
    // OPEN DIRECTION (ROBUST)
    // We keep the pivot EXACTLY as-is. Only choose +angle or -angle
    // so the door opens outward from the opener.
    // This version DOES NOT assume the door leaf is along local X.
    // ============================================================
    private Transform _doorModelForSign;

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

        if (pivot != null)
            _closedPivotLocalRot = pivot.localRotation;

        puzzlePrefabPath.OnValueChanged += OnPuzzlePrefabPathChanged;

        if (!puzzlePrefabPath.Value.IsEmpty)
            EnsurePuzzleLoadedFromNet(puzzlePrefabPath.Value);

        InitDoorLogic();
    }

    public override void OnNetworkDespawn()
    {
        puzzlePrefabPath.OnValueChanged -= OnPuzzlePrefabPathChanged;
    }

    private void OnPuzzlePrefabPathChanged(FixedString128Bytes _, FixedString128Bytes newVal)
    {
        EnsurePuzzleLoadedFromNet(newVal);

        if (doorType == DoorType.Puzzle && navigatorPreview == null)
            navigatorPreview = ExtractPreviewFromPrefab();
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

    // ✅ Preferred: call this from the opener (Navigator/Traveller) and pass THEIR position.
    public void Interact(Vector3 openerWorldPos)
    {
        if (doorType == DoorType.Puzzle)
            return;

        RequestOpenDoorServerRpc(openerWorldPos);
    }

    // Fallback only (kept for compatibility): tries navigator first, then traveller.
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
        if (pivotOverride != null)
        {
            pivot = pivotOverride;
            return;
        }

        if (pivot == null)
            FindOrCreatePivot();
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

        // Cache model for choosing sign
        _doorModelForSign = doorModel;

        Bounds b = mf.sharedMesh.bounds;
        float half = b.size.x * 0.5f;

        // pivot is created at LEFT edge (as your original logic)
        Vector3 leftLocal = new Vector3(b.center.x - half, b.center.y, b.center.z);
        Vector3 pivotWorld = doorModel.TransformPoint(leftLocal);

        GameObject pivotObj = new GameObject("Pivot");
        pivotObj.transform.SetParent(transform, true);
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
    }

    private IEnumerator OpenRoutine(float angle)
    {
        EnsurePivot();
        if (pivot == null)
        {
            Debug.LogError($"[DoorController] OpenRoutine aborted: pivot is NULL on '{name}'.", this);
            yield break;
        }

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
    }

    // ============================================================
    // Choose +openAngle / -openAngle so the DOOR LEAF opens AWAY
    // from the opener position. Pivot does not change.
    //
    // Robust method:
    // - Samples the 8 corners of the DOOR MODEL mesh bounds in local space
    // - Scores opening by average distance from opener (bigger => opens away)
    // - Does NOT assume hinge axis orientation in mesh local space
    // ============================================================
    private float ChooseOpenAngleSign(Vector3 openerWorldPos)
    {
        EnsurePivot();
        if (pivot == null) return openAngle;

        // Pick a stable model transform for bounds sampling
        if (_doorModelForSign == null)
            _doorModelForSign = (pivot.childCount > 0) ? pivot.GetChild(0) : pivot;

        // Find a MeshFilter under the door model for bounds
        var mf = _doorModelForSign.GetComponentInChildren<MeshFilter>(true);
        if (mf == null || mf.sharedMesh == null)
            return openAngle;

        Bounds b = mf.sharedMesh.bounds;
        Vector3 c = b.center;
        Vector3 e = b.extents;

        // 8 corners in mesh local
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

        Quaternion saved = pivot.localRotation;
        Quaternion baseRot = _closedPivotLocalRot;

        float Score(float angle)
        {
            pivot.localRotation = baseRot * Quaternion.Euler(0f, angle, 0f);

            float sum = 0f;
            for (int i = 0; i < corners.Length; i++)
            {
                // Important: use the MeshFilter's transform (since bounds are in mf's local space)
                Vector3 wp = mf.transform.TransformPoint(corners[i]);
                sum += (wp - openerWorldPos).sqrMagnitude;
            }
            return sum / corners.Length;
        }

        float sPlus = Score(+openAngle);
        float sMinus = Score(-openAngle);

        pivot.localRotation = saved;

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
        if(!IsServer)
            return;

        var tutorial = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (tutorial != null && pad != null && pad.IsPlayerOnPad())
        {
            if(doorType == DoorType.Normal)
                tutorial.NotifyNavigatorOpenedNormalDoor();
            else if(doorType == DoorType.Exit)
            {
                tutorial.NotifyNavigatorOpenedExitDoor();
                isExitDoor = true;
            }
        }
        float choosen = ChooseOpenAngleSign(openerWorldPos);
        StartCoroutine(OpenRoutine(choosen));
        OpenDoorRpc(choosen);
        if (isExitDoor)
        {
            var flow = LevelFlowManager.Instance;
            if (flow != null)
                flow.EndLevelWin_Server();
        }
    }

    [Rpc(SendTo.Everyone)]
    private void OpenDoorRpc(float choosenAngel)
    {
        StartCoroutine(OpenRoutine(choosenAngel));
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
}
