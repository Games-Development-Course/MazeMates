using UnityEngine;
using TMPro;
using Unity.Netcode;

public class RelayUITest : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject connectionPanel;   // הפאנל עם Host/Join/קוד
    [SerializeField] private TMP_Text codeLabel;           // הטקסט שמציג את הקוד (Code: QKTKF7)
    [SerializeField] private TMP_InputField codeInput;     // השדה שבו הנווט מזין קוד

    private bool lobbyHidden = false;

    // ===========================
    // NETCODE LIFECYCLE
    // ===========================
    public override void OnNetworkSpawn()
    {
        Debug.Log($"[RelayTestUI] OnNetworkSpawn | IsServer={IsServer} IsClient={IsClient} IsOwner={IsOwner}");

        if (IsServer && NetworkManager.Singleton != null)
        {
            Debug.Log("[RelayTestUI] Subscribing to OnClientConnectedCallback (SERVER)");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedServer;
        }

        if (connectionPanel == null)
        {
            Debug.LogWarning("[RelayTestUI] connectionPanel is NULL in inspector!");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            Debug.Log("[RelayTestUI] Unsubscribing from OnClientConnectedCallback (SERVER)");
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedServer;
        }
    }

    private void Start()
    {
        Debug.Log("[RelayTestUI] Ready (Start).");
    }

    // ===========================
    // SERVER: כשקליינט מתחבר
    // ===========================
    private void OnClientConnectedServer(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[RelayTestUI] OnClientConnectedServer called but NetworkManager is NULL");
            return;
        }

        var ids = NetworkManager.Singleton.ConnectedClientsIds;
        int count = ids != null ? ids.Count : 0;

        Debug.Log($"[RelayTestUI] OnClientConnectedServer (clientId={clientId}), total={count}");

        // כשיש 2 שחקנים או יותר – להסתיר לובי אצל כולם
        if (!lobbyHidden && count >= 2)
        {
            Debug.Log("[RelayTestUI] Two players connected → calling HideLobbyPanelClientRpc");
            lobbyHidden = true;
            HideLobbyPanelClientRpc();
        }
    }

    // ===========================
    // CLIENT RPC – רץ אצל כולם
    // ===========================
    [ClientRpc]
    private void HideLobbyPanelClientRpc()
    {
        Debug.Log($"[RelayTestUI] HideLobbyPanelClientRpc on client | IsServer={IsServer} IsClient={IsClient}");

        if (connectionPanel != null)
        {
            Debug.Log("[RelayTestUI] Hiding connectionPanel on this client");
            connectionPanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[RelayTestUI] connectionPanel is NULL on this client – cannot hide UI");
        }
    }

    // ===========================
    // HOST BUTTON
    // ===========================
    public async void OnHostClicked()
    {
        Debug.Log("[RelayTestUI] Host button clicked");

        string joinCode = await RelayManager.Instance.StartHostWithRelayAsync();

        if (!string.IsNullOrEmpty(joinCode))
        {
            // מציגים את הקוד אצל ההוסט
            if (codeLabel != null)
                codeLabel.text = $"Code:\n{joinCode}";

            // ממלאים גם את ה-Input שיהיה נוח להעתיק/להדביק
            if (codeInput != null)
                codeInput.text = joinCode;

            Debug.Log($"[RelayTestUI] Host got joinCode = {joinCode}");
        }
        else
        {
            Debug.LogWarning("[RelayTestUI] Host failed, no join code.");
        }
    }

    // ===========================
    // JOIN BUTTON (נווט)
    // ===========================
    public async void OnJoinClicked()
    {
        Debug.Log("[RelayTestUI] Join button clicked");

        string joinCode = codeInput != null ? codeInput.text.Trim().ToUpper() : "";

        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogWarning("[RelayTestUI] Join code is empty.");
            return;
        }

        Debug.Log($"[RelayTestUI] Trying to join with code = {joinCode}");

        bool ok = await RelayManager.Instance.StartClientWithRelayAsync(joinCode);
        Debug.Log("[RelayTestUI] StartClient returned = " + ok);
    }
}
