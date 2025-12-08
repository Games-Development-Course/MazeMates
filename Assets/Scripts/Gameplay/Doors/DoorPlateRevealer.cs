using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DoorPlateRevealer : MonoBehaviour
{
    [Header("Target plate (the yellow button)")]
    public Transform plate;          // הכפתור שנמצא מתחת לרצפה

    [Header("Movement")]
    public float riseAmount = 0.3f;  // כמה להרים אותו מעל המיקום ההתחלתי
    public float riseSpeed = 4f;     // מהירות האנימציה

    [Header("Trigger filter")]
    public string playerTag = "Player"; // תג של השחקן שנכנס לקוליידר

    private Vector3 hiddenPos;
    private Vector3 shownPos;

    private bool isShown = false;
    private bool plateLocked = false;    // ✅ אחרי שהכפתור עצמו נלחץ – לא יורד יותר
    private int playersInside = 0;
    private Coroutine moveRoutine;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (plate == null)
        {
            Debug.LogWarning("[DoorPlateRevealer] plate is not assigned, using self.", this);
            plate = transform;
        }

        hiddenPos = plate.localPosition;
        shownPos = hiddenPos + Vector3.up * riseAmount;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (plateLocked) return;          // כבר הופעל סופית

        playersInside++;
        if (playersInside == 1 && !isShown)
        {
            isShown = true;
            StartMove(true);              // להרים את הכפתור
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (plateLocked) return;          // לא מורידים אחרי שהופעל

        playersInside--;
        if (playersInside < 0) playersInside = 0;

        if (playersInside == 0 && isShown)
        {
            isShown = false;
            StartMove(false);             // להחזיר מתחת לרצפה
        }
    }

    private void StartMove(bool show)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MovePlate(show));
    }

    private IEnumerator MovePlate(bool show)
    {
        Vector3 from = show ? hiddenPos : shownPos;
        Vector3 to = show ? shownPos : hiddenPos;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * riseSpeed;
            float lerp = Mathf.SmoothStep(0f, 1f, t);

            plate.localPosition = Vector3.Lerp(from, to, lerp);
            yield return null;
        }

        plate.localPosition = to;
        moveRoutine = null;
    }

    // ----------------------------------------------------------
    // לקרוא לפונקציה הזו מאירוע onPressed של הפלטה עצמה (FloorPressurePlateGlow)
    // כאשר שחקן דורך עליה.
    // ----------------------------------------------------------
    public void LockPlateUp()
    {
        plateLocked = true;
        isShown = true;
        playersInside = 0;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        // לסיים מיד את הדרך למעלה
        plate.localPosition = shownPos;
    }
}
