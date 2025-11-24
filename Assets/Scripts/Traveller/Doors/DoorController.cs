using System.Collections;
using System.Collections.Generic;
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
        // מוצאים MeshFilter כדי לקבל את גבולות הדלת
        MeshFilter mf = GetComponentInChildren<MeshFilter>();
        if (mf == null)
        {
            Debug.LogError("No MeshFilter found under door!", this);
            return;
        }

        Transform doorModel = mf.transform;
        Bounds localBounds = mf.sharedMesh.bounds;

        float width = localBounds.size.x;

        // מחשבים את הצד שמאל של הדלת
        Vector3 leftLocal = new Vector3(
            localBounds.center.x - width * 0.5f,
            localBounds.center.y,
            localBounds.center.z
        );

        Vector3 leftWorld = doorModel.TransformPoint(leftLocal);

        // פיבוט חדש
        GameObject pivotObj = new GameObject("Pivot");
        pivotObj.transform.SetParent(transform);
        pivotObj.transform.position = leftWorld;
        pivotObj.transform.localRotation = Quaternion.identity;

        // נזיז רק דברים ש*שייכים* לדלת
        foreach (Transform child in transform)
        {
            if (child == pivotObj.transform)
                continue;

            string name = child.name.ToLower();

            // לא מזיזים Portal, Trigger או Pad
            if (name.Contains("portal")) continue;
            if (name.Contains("trigger")) continue;
            if (name.Contains("pad")) continue;

            // כן מזיזים את מודל הדלת ותוספות שלה
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
