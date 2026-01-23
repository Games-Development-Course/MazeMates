// Assets/Scripts/UI/HostOnlyUiGate.cs
using Unity.Netcode;
using UnityEngine;

public sealed class HostOnlyUiGate : MonoBehaviour
{
    [SerializeField] private GameObject root;

    private void OnEnable() => Apply();
    private void Update() => Apply();

    private void Apply()
    {
        var nm = NetworkManager.Singleton;
        bool isHost = nm != null && nm.IsListening && nm.IsHost;

        if (root != null)
            root.SetActive(isHost);
        else
            gameObject.SetActive(isHost);
    }
}
