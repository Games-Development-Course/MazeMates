// File: Assets/Scripts/UI/RelayUIController.cs
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public sealed class RelayUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject connectionPanel;

    [Tooltip("TMP label that shows the join code (your RoomCode TMP)")]
    [SerializeField] private TMP_Text codeLabel;

    [SerializeField] private TMP_InputField codeInput;

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
    [Tooltip("Print join code to Console when created")]
    [SerializeField] private bool logJoinCodeToConsole = true;

    private LobbyState lobbyState;

    private bool hostInProgress;
    private int hostRequestVersion;

    // so we don't close the menu after opening it once
    private bool difficultyMenuOpened;

    public System.Action<string> OnJoinCodeReady;

    // expose current join code for same-scene consumers (optional)
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

        // server itself (usually 0)
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

        // IMPORTANT: this can hide your code visually. Code will still be logged if enabled.
        if (hideCodeLabelOnHostWhenReady && codeLabel != null)
            codeLabel.gameObject.SetActive(false);

        if (hideConnectionPanelOnHostWhenReady && connectionPanel != null)
            connectionPanel.SetActive(false);

        if (codeInput != null)
            codeInput.gameObject.SetActive(false);
    }

    private void ApplyUiState()
    {
        var nm = NetworkManager.Singleton;

        bool isListening = nm != null && nm.IsListening;
        bool isHost = nm != null && nm.IsHost;
        bool isClientOnly = nm != null && nm.IsClient && !nm.IsHost;

        // strong fallback: Host + more than 1 client id means a real client joined
        bool hasRealClient =
            isHost && nm != null && nm.ConnectedClientsIds != null && nm.ConnectedClientsIds.Count > 1;

        Debug.Log($"[RelayUI] ApplyUiState | listening={isListening} host={isHost} clientOnly={isClientOnly} hasRealClient={hasRealClient} opened={difficultyMenuOpened}");

        if (hostButtonsPanel != null && !difficultyMenuOpened)
            hostButtonsPanel.SetActive(false);

        if (!isListening)
        {
            difficultyMenuOpened = false;

            CurrentJoinCode = "";

            if (RoomCodeStore.Instance != null)
                RoomCodeStore.Instance.Clear();

            if (connectionPanel != null) connectionPanel.SetActive(true);
            ShowLobbyButtons(true);

            if (codeInput != null)
            {
                codeInput.gameObject.SetActive(true);
                codeInput.interactable = true;
            }

            // bring back code label when not listening
            if (roomCodeRoot != null) roomCodeRoot.SetActive(true);
            if (codeLabel != null) codeLabel.gameObject.SetActive(true);

            return;
        }

        if (!difficultyMenuOpened && hasRealClient)
        {
            OpenDifficultyMenuOnHost();
            return;
        }

        // Host waiting for client => show code
        if (isHost && !difficultyMenuOpened)
        {
            if (connectionPanel != null) connectionPanel.SetActive(true);
            ShowLobbyButtons(false);

            if (codeInput != null) codeInput.gameObject.SetActive(false);

            // Ensure RoomCode UI is actually visible (parent can be disabled)
            if (roomCodeRoot != null) roomCodeRoot.SetActive(true);
            if (codeLabel != null) codeLabel.gameObject.SetActive(true);

            return;
        }

        // Client: after join you can hide the panel
        if (isClientOnly && hideConnectionPanelOnClientWhenReady && connectionPanel != null)
        {
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

        // Make sure the code UI is visible while creating
        if (roomCodeRoot != null) roomCodeRoot.SetActive(true);
        if (codeLabel != null)
        {
            codeLabel.gameObject.SetActive(true);
            codeLabel.text = "Creating room...";
        }

        string joinCode = await RelayManager.Instance.StartHostWithRelayAsync();

        if (myVersion != hostRequestVersion) return;
        hostInProgress = false;

        if (!string.IsNullOrEmpty(joinCode))
        {
            CurrentJoinCode = joinCode;

            if (logJoinCodeToConsole)
                Debug.Log($"[RelayUI] JOIN CODE = {joinCode}");

            // cross-scene store (game scene)
            if (RoomCodeStore.Instance != null)
                RoomCodeStore.Instance.SetJoinCode(joinCode);

            // same-scene broadcast
            OnJoinCodeReady?.Invoke(joinCode);

            // show in StartScene UI
            if (roomCodeRoot != null) roomCodeRoot.SetActive(true);

            if (codeLabel != null)
            {
                codeLabel.gameObject.SetActive(true);
                codeLabel.text = joinCode;
            }

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
            ShowLobbyButtons(true);
        }
    }

    public async void OnJoinClicked()
    {
        if (codeInput == null) return;

        string joinCode = codeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(joinCode)) return;

        bool ok = await RelayManager.Instance.StartClientWithRelayAsync(joinCode);
        Debug.Log($"[RelayUI] JoinClicked -> ok={ok}");

        if (ok)
        {
            if (codeLabel != null) codeLabel.gameObject.SetActive(false);
            if (hideConnectionPanelOnClientWhenReady && connectionPanel != null) connectionPanel.SetActive(false);
        }
    }

    private void ShowLobbyButtons(bool show)
    {
        if (hostJoinButtonsRoot != null) hostJoinButtonsRoot.SetActive(show);
        if (joinAreaRoot != null) joinAreaRoot.SetActive(show);
    }
}
