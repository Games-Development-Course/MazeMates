// Assets/Scripts/Gameplay/Doors/DoorController.cs
// FIX: removed [Server] attribute (not part of NGO). Use IsServer checks instead.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Collections;
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

    [Header("Navigator TV Screen")]
    [Tooltip("MeshRenderer של ה-Quad שעליו מוצגת התמונה בחדר הנווט")]
    public MeshRenderer navigatorScreenQuad; // אפשר להשאיר ריק ולהסתמך על Tag
    public int navigatorScreenMaterialIndex = 0;

    [Range(0.3f, 1f)]
    public float tvZoom = 1f;

    [Tooltip("Tag של ה-Quad של הטלוויזיה בחדר הנווט (למשל NavigatorScreenTV)")]
    [SerializeField] private string navigatorScreenTag = "NavigatorScreenTV";

    // ✅ IMPORTANT:
    // WebGL clients don't get runtime-assigned "puzzlePrefab" from server automatically.
    // We replicate a Resources path string, and each client loads the prefab locally.
    // Put your puzzle prefabs under: Assets/Resources/Puzzles/<PrefabName>.prefab (default folder is "Puzzles")
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
    private PadTrigger pad;

    private Material navigatorScreenMaterialInstance;

    private void Awake()
    {
        TryFindNavigatorScreenQuad();
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

        // ✅ subscribe before init (for late join too)
        puzzlePrefabPath.OnValueChanged += OnPuzzlePrefabPathChanged;

        // apply immediately for late joiners
        if (!puzzlePrefabPath.Value.IsEmpty)
            EnsurePuzzleLoadedFromNet();

        InitDoorLogic();
    }

    public override void OnNetworkDespawn()
    {
        puzzlePrefabPath.OnValueChanged -= OnPuzzlePrefabPathChanged;
    }

    private void OnPuzzlePrefabPathChanged(FixedString128Bytes _, FixedString128Bytes __)
    {
        EnsurePuzzleLoadedFromNet();

        if (doorType == DoorType.Puzzle && navigatorPreview == null)
            navigatorPreview = ExtractPreviewFromPrefab();
    }

    // ---------------------------
    // Server API: set puzzle prefab + replicate
    // ---------------------------
    // FIX: no [Server] attribute in NGO – just guard with IsServer.
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
        Bounds b = mf.sharedMesh.bounds;
        float half = b.size.x * 0.5f;

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

        Quaternion target = Quaternion.Euler(0, angle, 0);

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
    // FIND NAVIGATOR SCREEN QUAD
    // ============================================================
    private void TryFindNavigatorScreenQuad()
    {
        if (navigatorScreenQuad != null)
            return;

        if (!string.IsNullOrEmpty(navigatorScreenTag))
        {
            GameObject tvObj = GameObject.FindGameObjectWithTag(navigatorScreenTag);
            if (tvObj != null)
                navigatorScreenQuad = tvObj.GetComponent<MeshRenderer>();
        }

        if (navigatorScreenQuad == null)
        {
            Debug.LogWarning(
                $"[PUZZLE-TV] navigatorScreenQuad is NULL on {name} – "
                + "תוודא של-Quad של המסך יש Tag '"
                + navigatorScreenTag
                + "' או שגררת אותו ידנית ל-Inspector.",
                this
            );
        }
    }

    // ============================================================
    // MATERIAL HANDLING
    // ============================================================
    private void ApplyUnlitMaterial(Texture tex)
    {
        if (tex == null) return;

        if (navigatorScreenQuad == null)
            TryFindNavigatorScreenQuad();

        if (navigatorScreenQuad == null)
        {
            Debug.LogWarning(
                "[PUZZLE-TV] ApplyUnlitMaterial: navigatorScreenQuad עדיין NULL על " + name,
                this
            );
            return;
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null)
        {
            Debug.LogError("[PUZZLE-TV] לא נמצא Shader 'Universal Render Pipeline/Unlit'", navigatorScreenQuad);
            return;
        }

        navigatorScreenMaterialInstance = new Material(unlitShader);

        navigatorScreenMaterialInstance.SetTexture("_BaseMap", tex);
        navigatorScreenMaterialInstance.SetTexture("_MainTex", tex);

        navigatorScreenQuad.material = navigatorScreenMaterialInstance;

        Debug.Log(
            "[PUZZLE-TV] Applied UNLIT material with puzzle texture on navigator screen ("
            + navigatorScreenQuad.gameObject.name
            + ")",
            navigatorScreenQuad
        );
    }

    // ============================================================
    // DOOR INITIALIZATION
    // ============================================================
    private void InitDoorLogic()
    {
        switch (doorType)
        {
            case DoorType.Puzzle:
                EnsurePuzzleLoadedFromNet();

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
    public void Interact()
    {
        if (doorType == DoorType.Puzzle)
            return;
        RequestOpenDoorServerRpc();
    }

    public bool TravellerIsOnPad() => pad != null && pad.IsPlayerOnPad();
    public bool IsOpen() => door != null && door.IsOpen();
    public PuzzleDoor GetPuzzle() => door as PuzzleDoor;

    // ============================================================
    // PUZZLE PREFAB LOAD (CLIENT)
    // ============================================================
    private void EnsurePuzzleLoadedFromNet()
    {
        if (puzzlePrefab != null)
            return;

        if (puzzlePrefabPath.Value.IsEmpty)
            return;

        string path = puzzlePrefabPath.Value.ToString();
        var loaded = Resources.Load<GameObject>(path);

        if (loaded == null)
        {
            Debug.LogWarning($"[PUZZLE-TV] Resources.Load failed for path '{path}'. " +
                             $"Put prefab under Assets/Resources/{path}.prefab", this);
            return;
        }

        puzzlePrefab = loaded;
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
    public void RequestOpenDoorServerRpc()
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

        StartCoroutine(OpenRoutine(openAngle));
        OpenDoorRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void OpenDoorRpc()
    {
        StartCoroutine(OpenRoutine(openAngle));
    }

    // ============================================================
    // STATIC DOOR LOOKUP HELPERS
    // ============================================================
    public static DoorController FindDoorPlayerIsOn()
    {
        foreach (var door in Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
        {
            if (door.TravellerIsOnPad())
                return door;
        }
        return null;
    }

    public static DoorController FindDoorPlayerIsOn(GameObject _) => FindDoorPlayerIsOn();

    public static DoorController FindDoorPlayerIsOn(DoorType type)
    {
        foreach (var door in Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
        {
            if (door.doorType == type && door.TravellerIsOnPad())
                return door;
        }
        return null;
    }

    public static DoorController FindNearestDoorOnPad(Vector3 position, float maxDistance = 5f)
    {
        DoorController nearest = null;
        float minDist = float.MaxValue;

        foreach (var door in Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
        {
            if (!door.TravellerIsOnPad())
                continue;

            float dist = Vector3.Distance(position, door.transform.position);
            if (dist < minDist && dist <= maxDistance)
            {
                nearest = door;
                minDist = dist;
            }
        }
        return nearest;
    }

    public static DoorController FindNearestDoorOnPad(
        DoorType type,
        Vector3 position,
        float maxDistance = 5f
    )
    {
        DoorController nearest = null;
        float minDist = float.MaxValue;

        foreach (var door in Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
        {
            if (door.doorType != type)
                continue;
            if (!door.TravellerIsOnPad())
                continue;

            float dist = Vector3.Distance(position, door.transform.position);
            if (dist < minDist && dist <= maxDistance)
            {
                nearest = door;
                minDist = dist;
            }
        }
        return nearest;
    }

    public static DoorController FindPuzzleDoorWithTravellerOnPad()
    {
        foreach (var door in Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
        {
            if (door.doorType == DoorType.Puzzle && door.TravellerIsOnPad())
                return door;
        }
        return null;
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
            return;

        EnsurePuzzleLoadedFromNet();

        if (navigatorPreview == null || navigatorPreview.texture == null)
        {
            var extracted = ExtractPreviewFromPrefab();
            if (extracted != null)
                navigatorPreview = extracted;
        }

        if (navigatorPreview != null && navigatorPreview.texture != null)
        {
            ApplyUnlitMaterial(navigatorPreview.texture);
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                $"[DoorController] Applied UNLIT puzzle texture '{navigatorPreview.name}' on '{name}'"
            );
        }
        else
        {
            Debug.LogFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                null,
                $"[DoorController] Cannot apply puzzle texture — no valid sprite on {name}"
            );
        }
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
    