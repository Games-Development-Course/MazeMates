// Assets/Scripts/Net/LobbyState.cs
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class LobbyState : NetworkBehaviour
{
    public static LobbyState Instance { get; private set; }

    // Total players INCLUDING the host.
    public NetworkVariable<int> MaxPlayers { get; } = new(
        2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> ConnectedPlayers { get; } = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> SessionFull { get; } = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ----------------------------
    // Lobby selections
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

    public bool BothReady => HostReady.Value && ClientReady.Value && SessionFull.Value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

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

    private void OnClientChangedServer(ulong _) => RecomputeServer();

    private void RecomputeServer()
    {
        int connectedTotal = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ConnectedClientsList.Count
            : 0;

        ConnectedPlayers.Value = connectedTotal;
        SessionFull.Value = connectedTotal >= MaxPlayers.Value;
    }
    // קוראים לזה רק מהשרת/הוסט ברגע שנבחרה רמת קושי
    public void NotifyDifficultyChosen_Server()
    {
        if (!IsServer) return;
        DifficultyChosenClientRpc();
    }

    [ClientRpc]
    private void DifficultyChosenClientRpc()
    {
        // ירוץ על כל ה-Clients (וגם על ה-Host)
        var ui = FindFirstObjectByType<RelayUIController>();
        if (ui != null)
            ui.NotifyDifficultyChosen();
    }
    private bool IsHostClient(ulong clientId) => clientId == NetworkManager.ServerClientId;

    [ServerRpc(RequireOwnership = false)]
    public void SubmitLobbySelectionServerRpc(
        FixedString32Bytes playerName,
        int skinIndex,
        bool ready,
        ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        playerName = playerName.Length == 0
            ? new FixedString32Bytes("Player")
            : playerName;

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

        // Keep also in GameConfigNet (persists across scenes)
        var cfg = GameConfigNet.Instance;
        if (cfg != null && cfg.IsSpawned)
        {
            if (isHostSender)
            {
                cfg.HostName.Value = playerName;
                cfg.HostSkin.Value = skinIndex;
            }
            else
            {
                cfg.ClientName.Value = playerName;
                cfg.ClientSkin.Value = skinIndex;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetReadiesServerRpc()
    {
        HostReady.Value = false;
        ClientReady.Value = false;
    }
}
