using Unity.Netcode;
using UnityEngine;

public class NetworkButtons : MonoBehaviour
{
    public void StartHost()
    {
        if (!NetworkManager.Singleton.IsListening)
        {
            Debug.Log("HOST STARTING");
            NetworkManager.Singleton.StartHost();
        }
        else
        {
            Debug.LogWarning("Network already running!");
        }
    }

    public void StartClient()
    {
        if (!NetworkManager.Singleton.IsListening)
        {
            Debug.Log("CLIENT STARTING");
            NetworkManager.Singleton.StartClient();
        }
        else
        {
            Debug.LogWarning("Network already running!");
        }
    }
}
