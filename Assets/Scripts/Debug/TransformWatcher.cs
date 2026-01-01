// Assets/Scripts/Debug/TransformWatcher.cs
using UnityEngine;

public sealed class TransformWatcher : MonoBehaviour
{
    private Vector3 lastPos;
    private Quaternion lastRot;
    private Vector3 lastScale;

    private void Awake()
    {
        lastPos = transform.position;
        lastRot = transform.rotation;
        lastScale = transform.lossyScale;
        Debug.Log($"[TransformWatcher] START {name} pos={lastPos} rot={lastRot.eulerAngles} scale={lastScale}");
    }

    private void LateUpdate()
    {
        if (transform.position != lastPos || transform.rotation != lastRot || transform.lossyScale != lastScale)
        {
            Debug.LogWarning($"[TransformWatcher] CHANGED {name} pos {lastPos}->{transform.position} rot {lastRot.eulerAngles}->{transform.rotation.eulerAngles} scale {lastScale}->{transform.lossyScale}");
            lastPos = transform.position;
            lastRot = transform.rotation;
            lastScale = transform.lossyScale;
        }
    }
}
