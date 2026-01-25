// File: Assets/Scripts/UI/RelayUIController.cs
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public sealed class RelayUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject connectionPanel;

    [Header("Code UI (separate)")]
    [Tooltip("TMP label that shows ONLY the join code")]
    [SerializeField] private TMP_Text codeLabel;

    [Tooltip("Input used for joining (client types code here). Host can optionally fill it too.")]
    [SerializeField] private TMP_InputField codeInput;

    [Header("Status UI (separate)")]
    [Tooltip("TMP label that shows status like 'יוצר חדר...' / 'מצטרף...' / 'מחכה לבחירת רמת קושי...'")]
    [SerializeField] private TMP_Text statusLabel;

    [Header("Client Status Texts")]
    [SerializeField] private string waitingForDifficultyText = "מחכה לבחירת רמת קושי...";

    [Header("Optional: RoomCode Root (recommended)")]
    [Tooltip("Drag Canvas/LobbyRoot/RoomCode here. If this parent is disabled, the TMP won't show even if we set the text.")]
    [SerializeField] private GameObject roomCodeRoot;

    [Header("Optional UI Groups (recommended)")]
    [SerializeField] private GameObject hostJoinButtonsRoot;
    [SerializeField] private GameObject joinAreaRoot;

    [Header("Host UI (Difficulty Menu)")]
    [Tooltip("Drag HostButtonsPanel (difficulty menu / StartGame) here")]
    [SerializeField] private GameObject hostButtonsPanel;

    [Header("Behavior")]
    [SerializeField] private bool hideCodeLabelOnHostWhenReady = true;
    [SerializeField] private bool hideConnectionPanelOnHostWhenReady = true;
    [SerializeField] private bool hideConnectionPanelOnClientWhenReady = true;

    [Header("Debug")]
    [SerializeField] private bool logJoinCodeToConsole = true;

    private LobbyState lobbyState;

    private bool hostInProgress;
    private int hostRequestVersion;

    private bool difficultyMenuOpened;

    // ✅ Client-only flag: keep showing "waiting for difficulty" until cleared
    private bool clientWaitingForDifficulty;

    public System.Action<string> OnJoinCodeReady;

    public string CurrentJoinCode { get; private set; } = "";

    private void OnEnable()
    {
        StartCoroutine(BindLobbyStateWhenReady());
        StartCoroutine(BindNetworkManagerCallbacksWhenReady());
    }

    private void OnDisable()
    {
        if (lobbyState != null)
            lobbyState.SessionFull.OnValueChanged -= OnSessionFullChanged;

        var nm = NetworkManager.Singleton;
        if (nm != null)
            nm.OnClientConnectedCallback -= HandleClientConnected;

        lobbyState = null;
    }

    private IEnumerator BindLobbyStateWhenReady()
    {
        while (true)
        {
            lobbyState = FindFirstObjectByType<LobbyState>();
            if (lobbyState != null && lobbyState.IsSpawned)
                break;

            lobbyState = null;
            yield return null;
        }

        lobbyState.SessionFull.OnValueChanged += OnSessionFullChanged;
        ApplyUiState();
    }

    private IEnumerator BindNetworkManagerCallbacksWhenReady()
    {
        while (NetworkManager.Singleton == null)
            yield return null;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
    }

    private void OnSessionFullChanged(bool _, bool __) => ApplyUiState();

    private void HandleClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (!nm.IsHost) return;
        if (clientId == NetworkManager.ServerClientId) return;

        Debug.Log($"[RelayUI] Real client joined! clientId={clientId}");
        OpenDifficultyMenuOnHost();
    }

    private void OpenDifficultyMenuOnHost()
    {
        difficultyMenuOpened = true;

        if (hostButtonsPanel != null)
            hostButtonsPanel.SetActive(true);

        ShowLobbyButtons(false);

        if (hideCodeLabelOnHostWhenReady && codeLabel != null)
            codeLabel.gameObject.SetActive(false);

        if (hideConnectionPanelOnHostWhenReady && connectionPanel != null)
            connectionPanel.SetActive(false);

        if (codeInput != null)
            codeInput.gameObject.SetActive(false);

        SetStatus("");
    }

    private void ApplyUiState()
    {
        var nm = NetworkManager.Singleton;

        bool isListening = nm != null && nm.IsListening;
        bool isHost = nm != null && nm.IsHost;
        bool isClientOnly = nm != null && nm.IsClient && !nm.IsHost;

        bool hasRealClient =
            isHost && nm != null && nm.ConnectedClientsIds != null && nm.ConnectedClientsIds.Count > 1;

        Debug.Log($"[RelayUI] ApplyUiState | listening={isListening} host={isHost} clientOnly={isClientOnly} hasRealClient={hasRealClient} opened={difficultyMenuOpened} waitingDiff={clientWaitingForDifficulty}");

        if (hostButtonsPanel != null && !difficultyMenuOpened)
            hostButtonsPanel.SetActive(false);

        if (!isListening)
        {
            difficultyMenuOpened = false;

            // ✅ נקה JoinCode רק אצל HOST (Client לא נוגע בזה)
            if (nm != null && nm.IsHost)
            {
                CurrentJoinCode = "";
                RoomCodeStore.Instance?.Clear();
            }

            // ✅ reset waiting flag when leaving session
            clientWaitingForDifficulty = false;

            if (connectionPanel != null) connectionPanel.SetActive(true);
            ShowLobbyButtons(true);

            if (codeInput != null)
            {
                codeInput.gameObject.SetActive(true);
                codeInput.interactable = true;
            }

            if (roomCodeRoot != null) roomCodeRoot.SetActive(true);
            if (codeLabel != null)
            {
                codeLabel.gameObject.SetActive(true);
                codeLabel.text = "";
            }

            SetStatus("");
            return;
        }

        if (!difficultyMenuOpened && hasRealClient)
        {
            OpenDifficultyMenuOnHost();
            return;
        }

        if (isHost && !difficultyMenuOpened)
        {
            if (connectionPanel != null) connectionPanel.SetActive(true);
            ShowLobbyButtons(false);

            if (codeInput != null) codeInput.gameObject.SetActive(false);

            if (roomCodeRoot != null) roomCodeRoot.SetActive(true);
            if (codeLabel != null) codeLabel.gameObject.SetActive(true);

            return;
        }

        // ✅ Client: if we're waiting for difficulty, keep panel visible so the status text stays on screen
        if (isClientOnly && clientWaitingForDifficulty)
        {
            if (connectionPanel != null) connectionPanel.SetActive(true);
            ShowLobbyButtons(false);

            if (codeInput != null) codeInput.gameObject.SetActive(false);
            if (codeLabel != null) codeLabel.gameObject.SetActive(false);

            SetStatus(waitingForDifficultyText);
            return;
        }

        // Client: after join you can hide the panel
        if (isClientOnly && hideConnectionPanelOnClientWhenReady && connectionPanel != null)
        {
            if (!connectionPanel.name.Contains("HUD"))
                connectionPanel.SetActive(false);
        }
    }

    public async void OnHostClicked()
    {
        if (hostInProgress) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        hostInProgress = true;
        int myVersion = ++hostRequestVersion;

        difficultyMenuOpened = false;
        if (hostButtonsPanel != null) hostButtonsPanel.SetActive(false);

        if (connectionPanel != null) connectionPanel.SetActive(true);
        ShowLobbyButtons(false);

        if (codeInput != null) codeInput.gameObject.SetActive(false);

        if (roomCodeRoot != null) roomCodeRoot.SetActive(true);

        SetStatus("יוצר חדר...");
        SetCode("");

        string joinCode = await RelayManager.Instance.StartHostWithRelayAsync();

        if (myVersion != hostRequestVersion) return;
        hostInProgress = false;

        if (!string.IsNullOrEmpty(joinCode))
        {
            CurrentJoinCode = joinCode;

            if (logJoinCodeToConsole)
                Debug.Log($"[RelayUI] JOIN CODE = {joinCode}");

            RoomCodeStore.Instance?.SetJoinCode(joinCode);
            OnJoinCodeReady?.Invoke(joinCode);

            if (roomCodeRoot != null) roomCodeRoot.SetActive(true);

            SetCode(joinCode);
            SetStatus("");

            if (codeInput != null)
            {
                codeInput.text = joinCode;
                codeInput.interactable = false;
            }

            ApplyUiState();
        }
        else
        {
            Debug.LogError("[RelayUI] Failed to create host");
            SetStatus("נכשל ליצור חדר");
            ShowLobbyButtons(true);
        }
    }

    public async void OnJoinClicked()
    {
        if (codeInput == null) return;

        string joinCode = codeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(joinCode)) return;

        SetStatus("מצטרף...");

        bool ok = await RelayManager.Instance.StartClientWithRelayAsync(joinCode);
        Debug.Log($"[RelayUI] JoinClicked -> ok={ok}");

        if (ok)
        {
            RoomCodeStore.Instance?.SetJoinCode(joinCode);

            // hide code label on client
            if (codeLabel != null) codeLabel.gameObject.SetActive(false);

            // ✅ show waiting message until difficulty is chosen
            clientWaitingForDifficulty = true;
            SetStatus(waitingForDifficultyText);

            // keep connection panel visible while waiting (so status is visible)
            if (connectionPanel != null) connectionPanel.SetActive(true);
            ShowLobbyButtons(false);

            if (codeInput != null) codeInput.gameObject.SetActive(false);

            ApplyUiState();
        }
        else
        {
            clientWaitingForDifficulty = false;
            SetStatus("התחברות נכשלה");
        }
    }

    // ✅ CALL THIS when difficulty is chosen (from your difficulty screen / button)
    // On HOST you probably call it directly.
    // To clear it on CLIENT too, have the host send a ClientRpc somewhere that calls this on each client.
    public void NotifyDifficultyChosen()
    {
        clientWaitingForDifficulty = false;
        SetStatus("");

        var nm = NetworkManager.Singleton;
        bool isClientOnly = nm != null && nm.IsClient && !nm.IsHost;

        // now we may hide the panel like before
        if (isClientOnly && hideConnectionPanelOnClientWhenReady && connectionPanel != null)
        {
            if (!connectionPanel.name.Contains("HUD"))
                connectionPanel.SetActive(false);
        }
    }

    private void ShowLobbyButtons(bool show)
    {
        if (hostJoinButtonsRoot != null) hostJoinButtonsRoot.SetActive(show);
        if (joinAreaRoot != null) joinAreaRoot.SetActive(show);
    }

    private void SetCode(string code)
    {
        if (codeLabel == null) return;
        codeLabel.gameObject.SetActive(true);
        codeLabel.text = code ?? "";
    }

    private void SetStatus(string status)
    {
        if (statusLabel == null) return;

        if (string.IsNullOrWhiteSpace(status))
        {
            statusLabel.text = "";
            statusLabel.gameObject.SetActive(false);
            return;
        }

        statusLabel.gameObject.SetActive(true);
        statusLabel.text = status;
    }
}
