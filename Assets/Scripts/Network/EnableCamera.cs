using Unity.Netcode;
using UnityEngine;

public class EnableCamera : NetworkBehaviour
{
    public Camera cam;
    public AudioListener listener;

    public override void OnNetworkSpawn()
    {
        bool active = IsOwner;
        cam.enabled = active;
        if (listener != null)
            listener.enabled = active;
    }
}
