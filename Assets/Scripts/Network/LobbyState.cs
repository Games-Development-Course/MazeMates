// Assets/Scripts/Net/LobbyState.cs
using System.Linq;
using Unity.Collections;
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

    // ----------------------------
    // NEW: Lobby selections (2 players)
    // index 0 = Host, index 1 = Client
    // ----------------------------
    public NetworkVariable<FixedString32Bytes> HostName { get; } = new(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString32Bytes> ClientName { get; } = new(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> HostSkin { get; } = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> ClientSkin { get; } = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> HostReady { get; } = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> ClientReady { get; } = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool BothReady =>
        HostReady.Value && ClientReady.Value && SessionFull.Value;

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

    private void OnClientChangedServer(ulong clientId) => RecomputeServer();

    private void RecomputeServer()
    {
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
    }

    private bool IsHostClient(ulong clientId) => clientId == NetworkManager.ServerClientId;

    private bool IsKnownNonHostClient(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return false;
        // "Client" in your 2-player game = first non-host client
        return NetworkManager.Singleton.ConnectedClientsIds.Any(id => id != NetworkManager.ServerClientId && id == clientId);
    }
    [ServerRpc(RequireOwnership = false)]
    public void SubmitLobbySelectionServerRpc(
        FixedString32Bytes playerName,
        int skinIndex,
        bool ready,
        ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        playerName = playerName.Length == 0 ? new FixedString32Bytes("Player") : playerName;
        skinIndex = Mathf.Clamp(skinIndex, 0, 3);

        bool isHostSender = IsHostClient(sender);

        if (isHostSender)
        {
            HostName.Value = playerName;
            HostSkin.Value = skinIndex;
            HostReady.Value = ready;
        }
        else
        {
            ClientName.Value = playerName;
            ClientSkin.Value = skinIndex;
            ClientReady.Value = ready;
        }

        // ✅ NEW: גם לשמור את הסקינים ב-GameConfigNet כדי שישרדו מעבר סצנה
        var cfg = GameConfigNet.Instance;
        if (cfg != null && cfg.IsSpawned)
        {
            if (isHostSender) cfg.HostSkin.Value = skinIndex;
            else cfg.ClientSkin.Value = skinIndex;
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void ResetReadiesServerRpc()
    {
        HostReady.Value = false;
        ClientReady.Value = false;
    }
}
