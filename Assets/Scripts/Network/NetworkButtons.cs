using Unity.Netcode;
using UnityEngine;

public class NetworkButtons : MonoBehaviour
{
    public void StartHost()
    {
        Debug.Log("HOST STARTING");
        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        Debug.Log("CLIENT STARTING");
        NetworkManager.Singleton.StartClient();
    }
}
