using System;
using UnityEngine;

public sealed class RoomCodeWindowWatcher : MonoBehaviour
{
    private void OnEnable()
    {
        Debug.Log($"[RoomCodeWindowWatcher][OnEnable] {Path()} activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy}");
    }

    private void OnDisable()
    {
        Debug.Log($"[RoomCodeWindowWatcher][OnDisable] {Path()} activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy}\nSTACK:\n{Environment.StackTrace}");
    }

    private string Path()
    {
        var t = transform;
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }
}
