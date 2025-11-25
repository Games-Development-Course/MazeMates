using UnityEngine;

public class CameraCollision : MonoBehaviour
{
    public Transform cameraTransform;   // MainCamera
    public float headHeight = 0.7f;     // גובה עיניים
    public float minDistance = 0.1f;
    public float maxDistance = 0.1f;    // FPS ? המצלמה לא באמת זזה קדימה/אחורה
    public float smooth = 10f;

    private void LateUpdate()
    {
        // מציבים את הראש בגובה הנכון תמיד
        transform.localPosition = new Vector3(0f, headHeight, 0f);

        // יוצרים Ray קדימה כדי לבדוק אם ראש נוגע בקיר
        RaycastHit hit;
        Vector3 origin = transform.position;
        Vector3 direction = cameraTransform.forward;

        if (Physics.Raycast(origin, direction, out hit, 0.15f))
        {
            // נוגעים בקיר ? לא מאפשרים למצלמה להיכנס פנימה
            float adjustY = Mathf.Lerp(cameraTransform.localPosition.y, -0.05f, Time.deltaTime * smooth);
            cameraTransform.localPosition = new Vector3(0f, adjustY, 0f);
        }
        else
        {
            // עומד רגיל
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                Vector3.zero,
                Time.deltaTime * smooth
            );
        }
    }
}
