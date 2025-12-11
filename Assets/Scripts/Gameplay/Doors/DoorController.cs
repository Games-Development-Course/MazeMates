// DoorController.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class DoorController : NetworkBehaviour
{
    [Header("Door Settings")]
    public DoorType doorType;
    public float openAngle = 90f;
    public float openSpeed = 3f;

    [Header("Puzzle Settings")]
    public GameObject puzzlePrefab;
    public Sprite navigatorPreview; // auto-filled from OriginalImage if null

    [Header("Navigator TV Screen")]
    [Tooltip("MeshRenderer של ה-Quad שעליו מוצגת התמונה בחדר הנווט")]
    public MeshRenderer navigatorScreenQuad;          // אפשר להשאיר ריק ולהסתמך על Tag
    public int navigatorScreenMaterialIndex = 0;
    [Range(0.3f, 1f)]
    public float tvZoom = 1f;

    [Tooltip("Tag של ה-Quad של הטלוויזיה בחדר הנווט (למשל NavigatorScreenTV)")]
    [SerializeField] private string navigatorScreenTag = "NavigatorScreenTV";

    // OLD SYSTEM SUPPORT
    public List<GameObject> spawnedHints = new List<GameObject>();

    private Transform pivot;
    private IDoor door;
    private PadTrigger pad;

    // אינסטנס של המטריאל שניצור בזמן ריצה
    private Material navigatorScreenMaterialInstance;

    private void Awake()
    {
        // אם לא שויך Quad ביד – ננסה למצוא אחד לפי Tag
        TryFindNavigatorScreenQuad();
    }

    public override void Spawned()
    {
        base.Spawned();

        Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
            $"[DOOR SPAWN] {name} | NetId={Object.Id}");

        pad = GetComponentInChildren<PadTrigger>();

        if (pivot == null)
            FindOrCreatePivot();

        InitDoorLogic();
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
            {
                navigatorScreenQuad = tvObj.GetComponent<MeshRenderer>();
            }
        }

        if (navigatorScreenQuad == null)
        {
            Debug.LogWarning(
                $"[PUZZLE-TV] navigatorScreenQuad is NULL on {name} – " +
                "תוודא של-Quad של המסך יש Tag '" + navigatorScreenTag + "' " +
                "או שגררת אותו ידנית ל-Inspector.",
                this);
        }
    }

    // ============================================================
    // MATERIAL HANDLING
    // ============================================================

    /// <summary>
    /// מחליף את המטריאל של המסך ל-URP/Unlit ומכניס לתוכו את ה-Texture של הפאזל.
    /// נקרא בצד הלקוח אחרי שה-StateAuthority החליט להציג את הפאזל.
    /// </summary>
    private void ApplyUnlitMaterial(Texture tex)
    {
        if (tex == null)
            return;

        if (navigatorScreenQuad == null)
        {
            // נסיון נוסף למצוא את המסך בזמן ריצה
            TryFindNavigatorScreenQuad();
        }

        if (navigatorScreenQuad == null)
        {
            Debug.LogWarning("[PUZZLE-TV] ApplyUnlitMaterial: navigatorScreenQuad עדיין NULL על " + name, this);
            return;
        }

        var mr = navigatorScreenQuad;

        // יוצרים מטריאל חדש מבוסס URP/Unlit
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null)
        {
            Debug.LogError("[PUZZLE-TV] לא נמצא Shader 'Universal Render Pipeline/Unlit'", mr);
            return;
        }

        navigatorScreenMaterialInstance = new Material(unlitShader);
        navigatorScreenMaterialInstance.SetTexture("_BaseMap", tex);

        // אפשר פשוט להחליף את המטריאל (לא משנה לנו שאר הסלוטים במקרה הזה)
        mr.material = navigatorScreenMaterialInstance;

        Debug.Log("[PUZZLE-TV] Applied UNLIT material with puzzle texture on navigator screen (" +
                  mr.gameObject.name + ")", mr);
    }

    // ============================================================
    // DOOR INITIALIZATION
    // ============================================================

    private void InitDoorLogic()
    {
        switch (doorType)
        {
            case DoorType.Puzzle:
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

        // כל קליינט יכול לבקש, אבל הביצוע אצל StateAuthority
        RequestOpenDoorRpc();
    }

    public bool TravellerIsOnPad() => pad != null && pad.IsPlayerOnPad();
    public bool IsOpen() => door != null && door.IsOpen();
    public PuzzleDoor GetPuzzle() => door as PuzzleDoor;

    private Sprite ExtractPreviewFromPrefab()
    {
        if (puzzlePrefab == null) return null;

        Transform original = puzzlePrefab.transform.Find("OriginalImage");
        if (original == null) return null;

        var img = original.GetComponentInChildren<Image>();
        if (img != null && img.sprite != null)
        {
            Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
                $"[DoorController] Extracted preview sprite '{img.sprite.name}' from puzzle '{puzzlePrefab.name}'");
        }
        else
        {
            Debug.LogFormat(UnityEngine.LogType.Warning, LogOption.NoStacktrace, null,
                $"[DoorController] OriginalImage found but no sprite in '{puzzlePrefab.name}'");
        }

        return img != null ? img.sprite : null;
    }

    // ============================================================
    // RPC — OPEN NORMAL/EXIT DOOR
    // ============================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RequestOpenDoorRpc(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        var tutorial = FindAnyObjectByType<TutorialManager>();

        if (tutorial != null && pad != null && pad.IsPlayerOnPad())
        {
            if (doorType == DoorType.Normal)
                tutorial.NotifyNavigatorOpenedNormalDoor();
            else if (doorType == DoorType.Exit)
                tutorial.NotifyNavigatorOpenedExitDoor();
        }

        // StateAuthority מפעיל את האנימציה לכולם
        OpenDoorRpc();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void OpenDoorRpc(RpcInfo info = default)
    {
        StartCoroutine(OpenRoutine(openAngle));
    }

    private IEnumerator OpenRoutine(float angle)
    {
        if (pivot == null)
            yield break;

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
    // GENERATE DOOR PIVOT
    // ============================================================

    private void FindOrCreatePivot()
    {
        MeshFilter mf = GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(m => m.CompareTag("Door"));

        if (mf == null)
        {
            Debug.LogError("DoorController: No child with tag 'Door' found.");
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
            if (child == pivotObj.transform) continue;
            if (child.name.ToLower().Contains("trigger")) continue;
            if (child.name.ToLower().Contains("pad")) continue;

            child.SetParent(pivotObj.transform, true);
        }

        pivot = pivotObj.transform;
    }

    // ============================================================
    // STATIC DOOR LOOKUP HELPERS
    // ============================================================

    public static DoorController FindDoorPlayerIsOn()
    {
        foreach (var door in UnityEngine.Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
        {
            if (door.TravellerIsOnPad())
                return door;
        }
        return null;
    }

    public static DoorController FindDoorPlayerIsOn(GameObject _) =>
        FindDoorPlayerIsOn();

    public static DoorController FindDoorPlayerIsOn(DoorType type)
    {
        foreach (var door in UnityEngine.Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
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

        foreach (var door in UnityEngine.Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
        {
            if (!door.TravellerIsOnPad()) continue;

            float dist = Vector3.Distance(position, door.transform.position);
            if (dist < minDist && dist <= maxDistance)
            {
                nearest = door;
                minDist = dist;
            }
        }
        return nearest;
    }

    public static DoorController FindNearestDoorOnPad(DoorType type, Vector3 position, float maxDistance = 5f)
    {
        DoorController nearest = null;
        float minDist = float.MaxValue;

        foreach (var door in UnityEngine.Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
        {
            if (door.doorType != type) continue;
            if (!door.TravellerIsOnPad()) continue;

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
        foreach (var door in UnityEngine.Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None))
        {
            if (door.doorType == DoorType.Puzzle && door.TravellerIsOnPad())
                return door;
        }
        return null;
    }

    // ============================================================
    // PUBLIC API FOR PUZZLE
    // ============================================================

    public void ShowNavigatorPreviewOnScreen(Sprite sprite)
    {
        navigatorPreview = sprite;
        bool showPuzzle = sprite != null && sprite.texture != null;

        if (!Object.HasStateAuthority)
            RequestSetNavigatorScreenRpc(showPuzzle);
        else
            SetNavigatorScreenRpc(showPuzzle);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestSetNavigatorScreenRpc(bool showPuzzle, RpcInfo info = default)
    {
        SetNavigatorScreenRpc(showPuzzle);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SetNavigatorScreenRpc(bool showPuzzle, RpcInfo info = default)
    {
        if (!showPuzzle)
        {
            // פה בעתיד אפשר להחזיר Noise למסך
            return;
        }

        if (navigatorPreview == null || navigatorPreview.texture == null)
        {
            var extracted = ExtractPreviewFromPrefab();
            if (extracted != null)
                navigatorPreview = extracted;
        }

        if (navigatorPreview != null && navigatorPreview.texture != null)
        {
            ApplyUnlitMaterial(navigatorPreview.texture);
            Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
                $"[DoorController] Applied UNLIT puzzle texture '{navigatorPreview.name}' on '{name}'");
        }
        else
        {
            Debug.LogFormat(UnityEngine.LogType.Warning, LogOption.NoStacktrace, null,
                $"[DoorController] Cannot apply puzzle texture — no valid sprite on {name}");
        }
    }

    // ============================================================
    // PUZZLE OPEN — RPC
    // ============================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RequestOpenPuzzleDoorRpc(RpcInfo info = default)
    {
        Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
            $"[PUZZLE-RPC] RequestOpenPuzzleDoorRpc CALLED on {(Object.HasStateAuthority ? "STATE-AUTHORITY" : "CLIENT")} | door={name}");

        if (!Object.HasStateAuthority) return;

        OpenPuzzleForTravellerRpc();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void OpenPuzzleForTravellerRpc(RpcInfo info = default)
    {
        Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
            $"[PUZZLE-RPC] OpenPuzzleForTravellerRpc on {(Object.HasStateAuthority ? "STATE-AUTHORITY" : "CLIENT")} | door={name}");

        var gm = GameManager.Instance;
        if (gm == null || gm.traveller == null)
        {
            Debug.LogFormat(UnityEngine.LogType.Warning, LogOption.NoStacktrace, null,
                "[PUZZLE-RPC] traveller uninitialized");
            return;
        }

        var travellerNet = gm.traveller.GetComponent<NetworkObject>();
        if (travellerNet == null)
        {
            Debug.LogFormat(UnityEngine.LogType.Warning, LogOption.NoStacktrace, null,
                "[PUZZLE-RPC] traveller has no NetworkObject");
            return;
        }

        // הפאזל נפתח רק בלקוח שהטרוולר שלו מחזיק InputAuthority
        if (travellerNet.HasInputAuthority)
        {
            Debug.LogFormat(UnityEngine.LogType.Log, LogOption.NoStacktrace, null,
                "[PUZZLE-RPC] Traveller owns this client — opening puzzle");
            GetPuzzle()?.TryOpen();
        }
    }
}
