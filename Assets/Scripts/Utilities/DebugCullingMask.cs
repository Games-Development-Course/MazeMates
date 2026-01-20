using UnityEngine;

[DisallowMultipleComponent]
public class DebugCameraCullingMask : MonoBehaviour
{
    private Camera cam;
    private int lastMask;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (!cam)
        {
            UnityEngine.Debug.LogError("[CULL DEBUG] No Camera component.", this);
            enabled = false;
            return;
        }

        lastMask = cam.cullingMask;

        UnityEngine.Debug.Log(
            $"[CULL DEBUG] Awake | Camera='{cam.name}' tag='{cam.tag}' mask={MaskToString(lastMask)} ({lastMask})",
            this
        );
    }

    private void LateUpdate()
    {
        if (cam.cullingMask == lastMask)
            return;

        int newMask = cam.cullingMask;

        UnityEngine.Debug.LogError(
            $"[CULL DEBUG] ❌ CULLING MASK CHANGED ❌\n" +
            $"Camera : {cam.name} (tag={cam.tag})\n" +
            $"Frame  : {Time.frameCount}\n" +
            $"FROM   : {MaskToString(lastMask)} ({lastMask})\n" +
            $"TO     : {MaskToString(newMask)} ({newMask})\n\n" +
            $"STACK TRACE:\n{new System.Diagnostics.StackTrace(2, true)}",
            this
        );

        lastMask = newMask;
    }

    private static string MaskToString(int mask)
    {
        string result = "";
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                    result += name + " | ";
            }
        }

        return string.IsNullOrEmpty(result)
            ? "(None)"
            : result.TrimEnd(' ', '|');
    }
}
