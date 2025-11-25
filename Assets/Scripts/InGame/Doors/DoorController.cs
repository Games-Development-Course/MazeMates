using System.Collections;
using System.Linq;
using UnityEngine;

public interface IDoor
{
    bool IsOpen();
    void TryOpen();
}

public enum DoorType
{
    Normal,
    Puzzle,
    Exit,
}

public class DoorController : MonoBehaviour
{
    [Header("Door Settings")]
    public DoorType doorType;
    public float openAngle = 90f;
    public float openSpeed = 3f;

    [Header("Puzzle Settings")]
    public Canvas puzzleCanvas;
    public DraggablePiece[] pieces;
    public Transform[] targets;
    public Sprite puzzleFullImage; // התמונה המלאה
    public GameObject navigatorFullImageObj;
    public GameObject puzzleOriginalImage;

    [Header("Puzzle Visuals")]
    public Sprite puzzleOriginalSprite;
    public Transform hintOverlaysParent; // יכיל את Hint1 Hint2 וכו'

    private Transform pivot;
    private IDoor door;
    private PadTrigger pad;

    public bool TravellerIsOnPad()
    {
        return pad != null && pad.IsPlayerOnPad();
    }

    private void Awake()
    {
        pad = GetComponentInChildren<PadTrigger>();
        FindOrCreatePivot();

        switch (doorType)
        {
            case DoorType.Normal:
                door = new NormalDoor(this);
                break;

            case DoorType.Puzzle:
                door = new PuzzleDoor(this, puzzleCanvas, pieces, targets, puzzleOriginalImage);

                break;

            case DoorType.Exit:
                door = new ExitDoor(this);
                break;
        }
    }

    private void FindOrCreatePivot()
    {
        MeshFilter mf = GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(m => m.CompareTag("Door"));

        if (mf == null)
        {
            Debug.LogError("No MeshFilter with tag 'Door' found!", this);
            return;
        }

        Transform doorModel = mf.transform;
        Bounds localBounds = mf.sharedMesh.bounds;

        float width = localBounds.size.x;
        Vector3 leftLocal = new Vector3(
            localBounds.center.x - width * 0.5f,
            localBounds.center.y,
            localBounds.center.z
        );

        Vector3 leftWorld = doorModel.TransformPoint(leftLocal);

        GameObject pivotObj = new GameObject("Pivot");
        pivotObj.transform.SetParent(transform);
        pivotObj.transform.position = leftWorld;
        pivotObj.transform.localRotation = Quaternion.identity;

        foreach (Transform child in transform)
        {
            if (child == pivotObj.transform)
                continue;

            string name = child.name.ToLower();
            if (name.Contains("portal"))
                continue;
            if (name.Contains("trigger"))
                continue;
            if (name.Contains("pad"))
                continue;

            child.SetParent(pivotObj.transform, true);
        }

        pivot = pivotObj.transform;
    }

    public void Interact()
    {
        Debug.Log("DOOR Interact CALLED for door " + name);

        if (!TravellerIsOnPad())
        {
            Debug.Log("CANNOT INTERACT — PLAYER NOT ON PAD");
            return;
        }

        if (DoorPadToggle.Instance != null && !DoorPadToggle.Instance.allowSpaceActivation)
        {
            Debug.Log("INTERACT BLOCKED BY TOGGLE");
            return;
        }

        Debug.Log("TRY OPEN DOOR NOW");
        door.TryOpen();
    }

    public bool IsOpen() => door.IsOpen();

    public PuzzleDoor GetPuzzle() => door as PuzzleDoor;

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
        Quaternion targetRot = Quaternion.Euler(0, angle, 0);

        while (Quaternion.Angle(pivot.localRotation, targetRot) > 0.1f)
        {
            pivot.localRotation = Quaternion.Lerp(
                pivot.localRotation,
                targetRot,
                Time.deltaTime * openSpeed
            );

            yield return null;
        }

        pivot.localRotation = targetRot;
    }

    public void StartSlidingIntoWall()
    {
        StartCoroutine(SlideRoutine());
    }

    private IEnumerator SlideRoutine()
    {
        Transform doorMesh = transform
            .GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.CompareTag("Door"));

        if (doorMesh == null)
        {
            Debug.LogError("Door Mesh (tag Door) not found!");
            yield break;
        }

        Transform doorModel = doorMesh.parent;

        if (doorModel == null)
        {
            Debug.LogError("Door mesh has no parent!");
            yield break;
        }

        Vector3 startPos = doorModel.localPosition;
        Vector3 endPos = startPos + new Vector3(0f, 0f, 1.1f);

        float duration = 2f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0, 1, t / duration);
            doorModel.localPosition = Vector3.Lerp(startPos, endPos, k);
            yield return null;
        }

        doorModel.localPosition = endPos;
    }
}
