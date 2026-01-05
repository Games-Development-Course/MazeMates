// Assets/Scripts/Net/LobbyState.cs
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class LobbyState : NetworkBehaviour
{
    // Total players INCLUDING the host.
    public NetworkVariable<int> MaxPlayers { get; } = new(
        2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> ConnectedPlayers { get; } = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> SessionFull { get; } = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // RelayManager.MaxConnections is "total players" (host + clients) in your project.
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

    private void OnClientChangedServer(ulong clientId) => RecomputeServer();

    private void RecomputeServer()
    {
        // IMPORTANT:
        // ConnectedClientsIds.Count can look like "1" immediately (host),
        // and you were comparing it to MaxPlayers that was effectively "clients allowed" elsewhere.
        // We standardize: MaxPlayers = total players INCLUDING host.
        int connectedTotal = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ConnectedClientsList.Count // includes host
            : 0;

        ConnectedPlayers.Value = connectedTotal;

        bool full = connectedTotal >= MaxPlayers.Value;

        if (SessionFull.Value != full)
        {
            SessionFull.Value = full;
            Debug.Log($"[LobbyState] SessionFull changed -> {SessionFull.Value} (connectedTotal={connectedTotal}/{MaxPlayers.Value})");
        }
        else
        {
            Debug.Log($"[LobbyState] Recompute (no change) (connectedTotal={connectedTotal}/{MaxPlayers.Value}) full={SessionFull.Value}");
        }
    }
}
