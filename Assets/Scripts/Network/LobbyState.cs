//LobbyState (NetworkBehaviour): server tracks ConnectedPlayers / MaxPlayers / SessionFull via NetworkVariables.
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class LobbyState : NetworkBehaviour
{
    public NetworkVariable<int> MaxPlayers { get; } = new(
        2,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> ConnectedPlayers { get; } = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> SessionFull { get; } = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (RelayManager.Instance != null)
            MaxPlayers.Value = Mathf.Max(1, RelayManager.Instance.MaxConnections);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientChangedServer;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientChangedServer;
        }

        RecomputeServer();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientChangedServer;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientChangedServer;
        }
    }

    private void OnClientChangedServer(ulong _)
    {
        RecomputeServer();
    }

    private void RecomputeServer()
    {
        int connected = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;
        ConnectedPlayers.Value = connected;
        SessionFull.Value = connected >= MaxPlayers.Value;
    }
}
