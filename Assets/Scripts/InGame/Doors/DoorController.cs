using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Door Settings")]
    public DoorType doorType;
    public float openAngle = 90f;
    public float openSpeed = 3f;

    [Header("Puzzle Settings")]
    public GameObject puzzlePrefab;
    public Sprite navigatorPreview;

    private Transform pivot;
    private IDoor door;
    private PadTrigger pad;

    private Vector3 pivotOriginalLocalPos;
    private Quaternion pivotOriginalLocalRot;

    private void Awake()
    {
        pad = GetComponentInChildren<PadTrigger>();
        FindOrCreatePivot();

        if (doorType == DoorType.Puzzle)
        {
            navigatorPreview = ExtractPreviewFromPrefab();   // ← HERE!!!
        }

        switch (doorType)
        {
            case DoorType.Puzzle:
                door = new PuzzleDoor(this, puzzlePrefab, navigatorPreview);
                break;

            case DoorType.Normal:
                door = new NormalDoor(this);
                break;

            case DoorType.Exit:
                door = new ExitDoor(this);
                break;

            default:
                Debug.LogError("Unknown door type on " + gameObject.name);
                break;
        }
    }

    private Sprite ExtractPreviewSprite()
    {
        if (puzzlePrefab == null) return null;

        Transform original = puzzlePrefab.transform.Find("OriginalImage");
        if (original == null) return null;

        var img = original.GetComponentInChildren<UnityEngine.UI.Image>();
        return img != null ? img.sprite : null;
    }

    public bool TravellerIsOnPad() =>
        pad != null && pad.IsPlayerOnPad();

    public void Interact()
    {
        if (doorType == DoorType.Puzzle)
            door.TryOpen();
        else
            StartOpeningDoor(openAngle);
    }

    public bool IsOpen() => door.IsOpen();

    public void PuzzleSolved()
    {
        if (doorType == DoorType.Puzzle)
            ((PuzzleDoor)door).PuzzleSolved();
    }

    public void StartOpeningDoor(float angle)
    {
        StartCoroutine(OpenRoutine(angle));
    }



    private IEnumerator OpenRoutine(float angle)
    {
        Quaternion startRot = pivot.localRotation;
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

    private void FindOrCreatePivot()
    {
        // מוצאים את ה-Mesh האמיתי של הדלת
        MeshFilter mf = GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(m => m.CompareTag("Door"));

        if (mf == null)
        {
            Debug.LogError("DoorController: no MeshFilter with tag 'Door' found!");
            return;
        }

        Transform doorModel = mf.transform;
        Bounds b = mf.sharedMesh.bounds;

        // נקודת הקצה השמאלית של הדלת — זה עבד אצלך פיקס
        float half = b.size.x * 0.5f;
        Vector3 leftLocal = new Vector3(
            b.center.x - half,
            b.center.y,
            b.center.z
        );

        // ממירים לוורלד — זה הציר הישן שעבד מצוין
        Vector3 leftWorld = doorModel.TransformPoint(leftLocal);

        // יוצרים pivot חדש
        GameObject pivotObj = new GameObject("Pivot");
        pivotObj.transform.SetParent(transform);
        pivotObj.transform.position = leftWorld;
        pivotObj.transform.rotation = doorModel.rotation; // <-- הכי חשוב!

        // מעבירים את כל הילדים לפיווט — כמו שהיה קודם
        foreach (Transform child in transform)
        {
            if (child == pivotObj.transform) continue;

            string n = child.name.ToLower();
            if (n.Contains("trigger")) continue;
            if (n.Contains("pad")) continue;
            if (n.Contains("portal")) continue;

            child.SetParent(pivotObj.transform, true);
        }

        pivot = pivotObj.transform;
    }
    private Sprite ExtractPreviewFromPrefab()
    {
        if (puzzlePrefab == null)
            return null;

        // חפש ילד בשם "OriginalImage"
        Transform original = puzzlePrefab.transform.Find("OriginalImage");
        if (original == null)
        {
            Debug.LogError("OriginalImage not found inside " + puzzlePrefab.name);
            return null;
        }

        // קח את הקומפוננט Image
        var img = original.GetComponentInChildren<UnityEngine.UI.Image>();
        if (img == null)
        {
            Debug.LogError("No Image found under OriginalImage in " + puzzlePrefab.name);
            return null;
        }

        return img.sprite;
    }

    public void StartSlidingIntoWall()
    {
        StartCoroutine(SlideIntoWallRoutine());
    }

    private IEnumerator SlideIntoWallRoutine()
    {
        float duration = 1.0f;
        float t = 0;

        Vector3 startPos = pivot.localPosition;
        Vector3 targetPos = startPos + pivot.transform.right * -0.8f;

        while (t < duration)
        {
            t += Time.deltaTime;
            pivot.localPosition = Vector3.Lerp(startPos, targetPos, t / duration);
            yield return null;
        }

        pivot.localPosition = targetPos;
    }

    public PuzzleDoor GetPuzzle() => door as PuzzleDoor;

    public List<GameObject> spawnedHints = new List<GameObject>();
}
