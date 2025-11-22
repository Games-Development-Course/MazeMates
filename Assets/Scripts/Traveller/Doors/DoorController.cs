using UnityEngine;
using System.Collections;

public interface IDoor
{
    bool IsOpen();
    void TryOpen();
}

public enum DoorType
{
    Normal,
    Puzzle,
    Exit
}

public class DoorController : MonoBehaviour
{
    public DoorType doorType;
    public float openAngle = 90f;
    public float openSpeed = 3f;

    public Canvas puzzleCanvas;
    public DraggablePiece[] pieces;
    public Transform[] targets;

    private Transform pivot;
    private IDoor door;

    public GameObject puzzleRoot;


    void Awake()
    {
        FindOrCreatePivot();

        switch (doorType)
        {
            case DoorType.Normal:
                door = new NormalDoor(this);
                break;

            case DoorType.Puzzle:
                door = new PuzzleDoor(this, puzzleCanvas, pieces, targets);
                break;

            case DoorType.Exit:
                door = new ExitDoor(this);
                break;
        }
    }

    // --------------------------------------------------------
    //  Pivot Calculator using LOCAL mesh bounds (correct!)
    // --------------------------------------------------------
    private void FindOrCreatePivot()
    {
        // אם כבר קיים Pivot ידני – משתמשים בו
        Transform p = transform.Find("Pivot");
        if (p != null)
        {
            pivot = p;
            return;
        }

        // מחפשים MeshRenderer שמתאים דווקא לדלת
        MeshRenderer[] allMeshes = GetComponentsInChildren<MeshRenderer>();
        MeshRenderer doorMesh = null;

        foreach (var m in allMeshes)
        {
            if (m.gameObject.name.ToLower().Contains("door"))
            {
                doorMesh = m;
                break;
            }
        }

        if (doorMesh == null)
        {
            Debug.LogError("No door mesh found — name must contain 'Door'", this);
            return;
        }

        // --------  Local bounds (המשמעותי ביותר) --------
        MeshFilter mf = doorMesh.GetComponent<MeshFilter>();
        Bounds localBounds = mf.sharedMesh.bounds;   //  LOCAL, לא WORLD

        float width = localBounds.size.x;

        // מיקום הצד השמאלי הלוקאלי
        Vector3 localLeft = new Vector3(
            localBounds.center.x - width * 0.5f,
            localBounds.center.y,
            localBounds.center.z
        );

        // יוצרים פיבוט חדש
        GameObject pivotObj = new GameObject("Pivot");
        pivotObj.transform.SetParent(transform);

        // ממפים קואורדינטות local bounds  local position אמיתית בחלל של הדלת
        Vector3 worldLeft = doorMesh.transform.TransformPoint(localLeft);
        pivotObj.transform.position = worldLeft;

        pivotObj.transform.localRotation = Quaternion.identity;


        // ------------------------------------------------
        //  מעבירים רק את אובייקטי הדלת לפיבוט
        //   (לא Portal, לא Quad, לא ExitTrigger)
        // ------------------------------------------------
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (child == pivotObj.transform)
                continue;

            if (child.GetComponent<PadTrigger>() != null)
                continue;

            string lower = child.name.ToLower();
            if (lower.Contains("portal") || lower.Contains("quad") || lower.Contains("poster"))
                continue;

            child.SetParent(pivotObj.transform, true);
        }

        pivot = pivotObj.transform;
    }

    // --------------------------------------------------------
    public void Interact()
    {
        door.TryOpen();
    }

    public bool IsOpen()
    {
        return door.IsOpen();
    }

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

    public PuzzleDoor GetPuzzle()
    {
        return door as PuzzleDoor;
    }
}
