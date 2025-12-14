using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Canvas))]
public class AttachCanvasToMainCamera : MonoBehaviour
{
    [SerializeField] private string targetCameraName; // "TravellerCamera" או "NavigatorCamera"

    private void Start()
    {
        StartCoroutine(AttachWhenReady());
    }

    private IEnumerator AttachWhenReady()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
            yield break;

        Camera cam = null;

        // מחכים עד שהמצלמה עם השם המבוקש תיווצר (Traveller(Clone)/Navigator(Clone))
        while (cam == null)
        {
            var cameras = Resources.FindObjectsOfTypeAll<Camera>();
            foreach (var c in cameras)
            {
                if (c.name == targetCameraName)
                {
                    cam = c;
                    break;
                }
            }

            if (cam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                Debug.Log($"[AttachCanvasToMainCamera] '{name}' attached to camera '{cam.name}'");
                yield break;
            }

            // המצלמות נוצרות אחרי הטעינה → נחכה פריים וננסה שוב
            yield return null;
        }
    }
}
