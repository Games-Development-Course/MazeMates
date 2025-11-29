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

    private void Awake()
    {
        pad = GetComponentInChildren<PadTrigger>();
        FindOrCreatePivot();
    }

    // ---------------------------------------------------------
    // CREATE IDoor ONLY AFTER MazeGenerator assigns puzzlePrefab
    // ---------------------------------------------------------
    private void Start()
    {
        if (doorType == DoorType.Puzzle)
        {
            navigatorPreview = ExtractPreviewFromPrefab();
            door = new PuzzleDoor(this);
        }
        else if (doorType == DoorType.Normal)
        {
            door = new NormalDoor(this);
        }
        else if (doorType == DoorType.Exit)
        {
            door = new ExitDoor(this);
        }
    }

    // ---------------------------------------------------------
    private Sprite ExtractPreviewFromPrefab()
    {
        if (puzzlePrefab == null)
            return null;

        Transform original = puzzlePrefab.transform.Find("OriginalImage");
        if (original == null)
        {
            Debug.LogError("OriginalImage not found inside " + puzzlePrefab.name);
            return null;
        }

        var img = original.GetComponentInChildren<UnityEngine.UI.Image>();
        if (img == null)
        {
            Debug.LogError("No Image found under OriginalImage in " + puzzlePrefab.name);
            return null;
        }

        return img.sprite;
    }

    // ---------------------------------------------------------
    public bool TravellerIsOnPad() => pad != null && pad.IsPlayerOnPad();

    public void Interact()
    {
        door?.TryOpen();
    }

    public bool IsOpen() => door != null && door.IsOpen();

    public void PuzzleSolved()
    {
        if (doorType == DoorType.Puzzle && door is PuzzleDoor pd)
            pd.PuzzleSolved();
    }

    // ---------------------------------------------------------
    public void StartOpeningDoor(float angle) => StartCoroutine(OpenRoutine(angle));

    private IEnumerator OpenRoutine(float angle)
    {
        Quaternion target = Quaternion.Euler(0, angle, 0);

        while (Quaternion.Angle(pivot.localRotation, target) > 0.1f)
        {
            pivot.localRotation = Quaternion.Lerp(pivot.localRotation, target, Time.deltaTime * openSpeed);
            yield return null;
        }

        pivot.localRotation = target;
    }

    // ---------------------------------------------------------
    private void FindOrCreatePivot()
    {
        MeshFilter mf = GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(m => m.CompareTag("Door"));

        if (mf == null)
        {
            Debug.LogError("DoorController: no MeshFilter with tag 'Door' found!");
            return;
        }

        Transform doorModel = mf.transform;
        Bounds b = mf.sharedMesh.bounds;

        float half = b.size.x * 0.5f;
        Vector3 leftLocal = new Vector3(b.center.x - half, b.center.y, b.center.z);

        Vector3 leftWorld = doorModel.TransformPoint(leftLocal);

        GameObject pivotObj = new GameObject("Pivot");
        pivotObj.transform.SetParent(transform);
        pivotObj.transform.position = leftWorld;
        pivotObj.transform.rotation = doorModel.rotation;

        foreach (Transform child in transform)
        {
            if (child == pivotObj.transform)
                continue;

            string n = child.name.ToLower();
            if (n.Contains("trigger")) continue;
            if (n.Contains("pad")) continue;
            if (n.Contains("portal")) continue;

            child.SetParent(pivotObj.transform, true);
        }

        pivot = pivotObj.transform;
    }

    // ---------------------------------------------------------
    public PuzzleDoor GetPuzzle() => door as PuzzleDoor;

    public List<GameObject> spawnedHints = new List<GameObject>();


    // ---------------------------------------------------------
    public void StartSlidingIntoWall() => StartCoroutine(SlideIntoWallRoutine());

    private IEnumerator SlideIntoWallRoutine()
    {
        float duration = 1f;
        float t = 0;

        Vector3 startPos = pivot.localPosition;
        Vector3 endPos = startPos + pivot.transform.right * -1.1f;

        while (t < duration)
        {
            t += Time.deltaTime;
            pivot.localPosition = Vector3.Lerp(startPos, endPos, t / duration);
            yield return null;
        }

        pivot.localPosition = endPos;
    }
}
