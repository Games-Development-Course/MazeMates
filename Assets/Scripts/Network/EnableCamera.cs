// EnableCamera.cs  (Fusion 2)
using Fusion;
using UnityEngine;

public class EnableCamera : NetworkBehaviour
{
    public Camera cam;
    public AudioListener listener;

    public override void Spawned()
    {
        base.Spawned();

        bool active = Object.HasInputAuthority;

        if (cam != null)
            cam.enabled = active;

        if (listener != null)
            listener.enabled = active;
    }
}
