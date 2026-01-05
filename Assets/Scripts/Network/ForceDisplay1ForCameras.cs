using UnityEngine;
using UnityEngine.SceneManagement;

public class ForceDisplay1ForCameras : MonoBehaviour
{
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
#if UNITY_2023_1_OR_NEWER
        var cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
#else
        var cameras = Object.FindObjectsOfType<Camera>(true);
#endif

        foreach (var cam in cameras)
        {
            cam.targetDisplay = 0; // Display 1
        }
    }
}
