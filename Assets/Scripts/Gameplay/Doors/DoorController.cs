// DoorController.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class DoorController : NetworkBehaviour
{
    [Header("Door Settings")]
    public DoorType doorType;
    public float openAngle = 90f;
    public float openSpeed = 3f;

    [Header("Puzzle Settings")]
    public GameObject puzzlePrefab;
    public Sprite navigatorPreview;          // ימולא אוטומטית מ-OriginalImage אם ריק

    [Header("Navigator TV Screen")]
    public MeshRenderer navigatorScreen;           // לא חובה – רק אם אתה רוצה גם זכוכית וכו'
    public MeshRenderer navigatorScreenQuad;       // ✅ ה-Quad שעליו נציג את התמונה
    public int navigatorScreenMaterialIndex = 0;   // בדרך כלל 0 ב-Quad
    [Range(0.3f, 1f)]
    public float tvZoom = 1f;                      // 1 = מלא, <1 = קצת זום־אאוט

    // אינסטנס של המטריאל ושל שם הפרופרטי לטקסטורה
    private Material navigatorMatInstance;
    private string colorMapProperty = "_BaseMap"; // אם השיידר לא URP Lit נעבור ל-_MainTex

    // OLD SYSTEM SUPPORT
    public List<GameObject> spawnedHints = new List<GameObject>();

    private Transform pivot;
    private IDoor door;
    private PadTrigger pad;

    // =====================================================================
    // LIFECYCLE
    // =====================================================================
    private void Awake()
    {
        // נסה למצוא את ה-Quad בשם ScreenQuad אם לא חיברת ידנית
        if (navigatorScreenQuad == null)
        {
            navigatorScreenQuad = GetComponentsInChildren<MeshRenderer>(true)
                .FirstOrDefault(m => m.name == "ScreenQuad");
        }

        // fallback ישן – לא חובה אבל לא מזיק
        if (navigatorScreen == null)
        {
            navigatorScreen = GetComponentInChildren<MeshRenderer>();
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[DOOR SPAWN] {name} | NetId={NetworkObjectId}");

        pad = GetComponentInChildren<PadTrigger>();

        if (pivot == null)
            FindOrCreatePivot();

        InitDoorLogic();
    }

    // =====================================================================
    // MATERIAL INSTANCE HELPERS
    // =====================================================================
    private void EnsureNavigatorMaterial()
    {
        if (navigatorMatInstance != null)
            return;

        // קודם כל נעדיף את ה-Quad, אחרת ניפול ל-navigatorScreen
        MeshRenderer targetRenderer = navigatorScreenQuad != null ? navigatorScreenQuad : navigatorScreen;

        if (targetRenderer == null)
        {
            Debug.LogWarning($"[DoorController] No screen renderer found on {name}");
            return;
        }

        var mats = targetRenderer.materials; // materials מחזיר כבר instances

        if (mats == null || mats.Length == 0)
        {
            Debug.LogWarning($"[DoorController] Renderer {targetRenderer.name} has NO materials");
            return;
        }

        int idx = Mathf.Clamp(navigatorScreenMaterialIndex, 0, mats.Length - 1);

        navigatorMatInstance = mats[idx];   // עובדים על האינסטנס הקיים
        mats[idx] = navigatorMatInstance;
        targetRenderer.materials = mats;

        // קובעים איזה property הוא ה"צבע" המרכזי של השיידר
        if (!navigatorMatInstance.HasProperty(colorMapProperty))
        {
            if (navigatorMatInstance.HasProperty("_BaseMap"))
                colorMapProperty = "_BaseMap";
            else if (navigatorMatInstance.HasProperty("_MainTex"))
                colorMapProperty = "_MainTex";
        }

        Debug.Log($"[DoorController] Using material '{navigatorMatInstance.name}' on {targetRenderer.name}, property={colorMapProperty}");
    }

    private void ApplyTextureToNavigatorScreen(Texture tex)
    {
        if (tex == null)
        {
            Debug.LogWarning($"[DoorController] texture is NULL on {name}");
            return;
        }

        EnsureNavigatorMaterial();
        if (navigatorMatInstance == null)
            return;

        // --- סט טקסטורה עיקרית (BaseMap / MainTex) ---
        if (navigatorMatInstance.HasProperty(colorMapProperty))
        {
            navigatorMatInstance.SetTexture(colorMapProperty, tex);

            // פליפ ב-Y (כמו קודם) + אפשרות זום־אאוט
            float s = Mathf.Clamp(tvZoom, 0.3f, 1f); // 1 = ממלא את כל ה-Quad
            Vector2 scale = new Vector2(1f * s, -1f * s);
            Vector2 offset = new Vector2(
                0.5f - 0.5f * s,
                0.5f + 0.5f * s
            );

            navigatorMatInstance.SetTextureScale(colorMapProperty, scale);
            navigatorMatInstance.SetTextureOffset(colorMapProperty, offset);
        }
        else
        {
            // fallback גנרי
            navigatorMatInstance.mainTexture = tex;

            float s = Mathf.Clamp(tvZoom, 0.3f, 1f);
            navigatorMatInstance.mainTextureScale = new Vector2(1f * s, -1f * s);
            navigatorMatInstance.mainTextureOffset = new Vector2(
                0.5f - 0.5f * s,
                0.5f + 0.5f * s
            );
        }

        // --- Emission כדי שלא יהיה כהה ---
        if (navigatorMatInstance.HasProperty("_EmissionMap"))
        {
            navigatorMatInstance.SetTexture("_EmissionMap", tex);
        }

        if (navigatorMatInstance.HasProperty("_EmissionColor"))
        {
            navigatorMatInstance.EnableKeyword("_EMISSION");
            navigatorMatInstance.SetColor("_EmissionColor", Color.white);
        }

        if (navigatorMatInstance.HasProperty("_Metallic"))
            navigatorMatInstance.SetFloat("_Metallic", 0f);
        if (navigatorMatInstance.HasProperty("_Smoothness"))
            navigatorMatInstance.SetFloat("_Smoothness", 0f);
    }

    // =====================================================================
    // DOOR LOGIC
    // =====================================================================
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

    // =====================================================================
    // INTERACTION
    // =====================================================================
    public void Interact()
    {
        if (doorType == DoorType.Puzzle)
            return; // פאזל נפתח רק ע"י הנווט

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

        var img = original.GetComponentInChildren<UnityEngine.UI.Image>();
        if (img != null && img.sprite != null)
        {
            Debug.Log($"[DoorController] Extracted preview sprite '{img.sprite.name}' from puzzle prefab '{puzzlePrefab.name}'");
        }
        else
        {
            Debug.LogWarning($"[DoorController] OriginalImage exists but has no sprite on prefab '{puzzlePrefab.name}'");
        }

        return img != null ? img.sprite : null;
    }

    // =====================================================================
    // RPC SYSTEM
    // =====================================================================
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestOpenDoorRpc()
    {
        if (!IsServer) return;

        StartCoroutine(OpenRoutine(openAngle));
        OpenDoorRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void OpenDoorRpc()
    {
        StartCoroutine(OpenRoutine(openAngle));
    }

    private IEnumerator OpenRoutine(float angle)
    {
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

    // =====================================================================
    // PIVOT יצירת
    // =====================================================================
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
        pivotObj.transform.SetParent(transform, worldPositionStays: true);
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

    // =====================================================================
    // STATIC HELPERS לשימוש מהנווט
    // =====================================================================

    // גרסה ללא פרמטר – תואמת קריאה כמו DoorController.FindDoorPlayerIsOn()
    public static DoorController FindDoorPlayerIsOn()
    {
        var doors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        foreach (var door in doors)
        {
            if (door.TravellerIsOnPad())
                return door;
        }
        return null;
    }

    // גרסה עם פרמטר GameObject – אם אי פעם יקראו עם player, עדיין יעבוד
    public static DoorController FindDoorPlayerIsOn(GameObject player)
    {
        // כרגע אנחנו לא מסננים לפי השחקן, רק לפי האם יש שחקן על הפד
        return FindDoorPlayerIsOn();
    }

    // גרסה כללית – לפי מיקום בלבד
    public static DoorController FindNearestDoorOnPad(Vector3 position, float maxDistance = 5f)
    {
        DoorController nearest = null;
        float minDist = float.MaxValue;

        var doors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        foreach (var door in doors)
        {
            if (!door.TravellerIsOnPad())
                continue;

            float dist = Vector3.Distance(position, door.transform.position);
            if (dist < minDist && dist <= maxDistance)
            {
                minDist = dist;
                nearest = door;
            }
        }

        return nearest;
    }

    // גרסה תואמת ל-NavigatorActions: DoorType + מיקום
    public static DoorController FindNearestDoorOnPad(DoorType type, Vector3 position, float maxDistance = 5f)
    {
        DoorController nearest = null;
        float minDist = float.MaxValue;

        var doors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        foreach (var door in doors)
        {
            if (door.doorType != type)
                continue;

            if (!door.TravellerIsOnPad())
                continue;

            float dist = Vector3.Distance(position, door.transform.position);
            if (dist < minDist && dist <= maxDistance)
            {
                minDist = dist;
                nearest = door;
            }
        }

        return nearest;
    }

    // =====================================================================
    // PUBLIC API מהפאזל
    // =====================================================================
    public void ShowNavigatorPreviewOnScreen(Sprite sprite)
    {
        bool showPuzzle = sprite != null && sprite.texture != null;

        if (!IsServer)
        {
            RequestSetNavigatorScreenServerRpc(showPuzzle);
        }
        else
        {
            SetNavigatorScreenClientRpc(showPuzzle);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSetNavigatorScreenServerRpc(bool showPuzzle)
    {
        SetNavigatorScreenClientRpc(showPuzzle);
    }

    [Rpc(SendTo.Everyone)]
    private void SetNavigatorScreenClientRpc(bool showPuzzle)
    {
        // כרגע תמיד מציגים את ה-preview אם קיים (או נשארים על הישן אם null)
        if (navigatorPreview != null && navigatorPreview.texture != null)
        {
            Texture texToShow = navigatorPreview.texture;
            ApplyTextureToNavigatorScreen(texToShow);
        }
        // אחרת לא עושים כלום – נשאר הטקסטורה האחרונה (ל-noise תשתמש בפתרון שלך אם יש)
    }
}
