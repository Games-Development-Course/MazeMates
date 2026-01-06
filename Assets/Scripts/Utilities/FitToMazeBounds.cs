using UnityEngine;

[ExecuteAlways]
public class MinimapFitToMaze : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MazeGenerator3D maze; // גרור את ה-MazeGenerator3D לפה

    [Header("Camera")]
    [SerializeField] private Camera cam;           // אם ריק -> יקח Camera על אותו אובייקט
    [SerializeField] private float heightAbove = 30f; // גובה המצלמה מעל הרצפה
    [SerializeField] private float paddingWorld = 0f; // אם אתה רוצה ממש 0 - השאר 0

    [Header("Orientation")]
    [SerializeField] private bool forceTopDown = true;

    void OnEnable()
    {
        if (!cam) cam = GetComponent<Camera>();
        TryFit();
    }

    void LateUpdate()
    {
        // אם המבוך משתנה/מתיישר בזמן ריצה, זה יוודא התאמה
        TryFit();
    }

    private void TryFit()
    {
        if (!maze || !cam) return;
        FitCameraToMaze(maze, cam, heightAbove, paddingWorld, forceTopDown);
    }

    public static void FitCameraToMaze(MazeGenerator3D maze, Camera cam, float heightAbove, float paddingWorld, bool forceTopDown)
    {
        // חייבים להשתמש בנתונים "אחרי יישור" ולכן לוקחים את ה-transform של ה-maze
        float w = maze.MazeWorldWidth;
        float h = maze.MazeWorldHeight;

        // מרכז המבוך ב-LOCAL -> WORLD
        Vector3 localCenter = new Vector3(w * 0.5f, 0f, h * 0.5f);
        Vector3 worldCenter = maze.transform.TransformPoint(localCenter);

        // מכוונים למבט מלמעלה
        if (forceTopDown)
        {
            cam.transform.rotation = Quaternion.Euler(90f, maze.transform.eulerAngles.y, 0f);
        }

        // ממקמים למעלה, מעל המרכז
        cam.transform.position = worldCenter + Vector3.up * heightAbove;

        // מינימאפ הכי נקי: Orthographic
        cam.orthographic = true;

        // התאמת orthographicSize כך שיכסה את כל הגבולות בדיוק לפי aspect
        float aspect = Mathf.Max(0.0001f, cam.aspect);

        float halfH = (h * 0.5f) + paddingWorld;
        float halfW = (w * 0.5f) + paddingWorld;

        // OrthographicSize הוא חצי-גובה במסך; צריך לבחור את המקסימום בין:
        // halfH (כדי שהגובה ייכנס)
        // halfW / aspect (כדי שהרוחב ייכנס)
        cam.orthographicSize = Mathf.Max(halfH, halfW / aspect);

        // כדי שלא ייחתך לפי far clip:
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, heightAbove + 200f);
    }
}
