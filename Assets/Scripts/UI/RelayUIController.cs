// Assets/Scripts/UI/RelayUIController.cs
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public sealed class RelayUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private TMP_Text codeLabel;
    [SerializeField] private TMP_InputField codeInput;

    [Header("Optional UI Groups (recommended)")]
    [SerializeField] private GameObject hostJoinButtonsRoot;
    [SerializeField] private GameObject joinAreaRoot;

    [Header("Behavior")]
    [SerializeField] private bool hideCodeLabelOnHostWhenFull = true;
    [SerializeField] private bool hideConnectionPanelOnClientWhenFull = true;
    [SerializeField] private bool hideConnectionPanelOnHostWhenFull = false;

    private LobbyState lobbyState;

    private bool hostInProgress;
    private int hostRequestVersion;

    private void OnEnable() => StartCoroutine(BindLobbyStateWhenReady());

    private void OnDisable()
    {
        if (lobbyState != null)
            lobbyState.SessionFull.OnValueChanged -= OnSessionFullChanged;

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

    private void OnSessionFullChanged(bool _, bool __) => ApplyUiState();

    private void ApplyUiState()
    {
        var nm = NetworkManager.Singleton;
        bool isListening = nm != null && nm.IsListening;
        bool isHost = nm != null && nm.IsHost;
        bool isClientOnly = nm != null && nm.IsClient && !nm.IsHost;

        bool full = lobbyState != null && lobbyState.SessionFull.Value;

        Debug.Log($"[RelayUI] ApplyUiState | listening={isListening} host={isHost} clientOnly={isClientOnly} full={full}");

        if (!isListening)
        {
            if (connectionPanel != null) connectionPanel.SetActive(true);
            ShowLobbyButtons(true);

            if (codeInput != null)
            {
                codeInput.gameObject.SetActive(true);
                codeInput.interactable = true;
            }

            if (codeLabel != null) codeLabel.gameObject.SetActive(true);
            return;
        }

        if (full)
        {
            if (isHost)
            {
                if (hideCodeLabelOnHostWhenFull && codeLabel != null)
                    codeLabel.gameObject.SetActive(false);

                if (hideConnectionPanelOnHostWhenFull && connectionPanel != null)
                    connectionPanel.SetActive(false);
            }
            else if (isClientOnly)
            {
                if (hideConnectionPanelOnClientWhenFull && connectionPanel != null)
                    connectionPanel.SetActive(false);
            }

            return;
        }

        // Not full yet
        if (connectionPanel != null) connectionPanel.SetActive(true);

        if (isHost)
        {
            ShowLobbyButtons(false);
            if (codeInput != null) codeInput.gameObject.SetActive(false);
            if (codeLabel != null) codeLabel.gameObject.SetActive(true);
        }
        else
        {
            ShowLobbyButtons(true);
            if (codeInput != null) codeInput.gameObject.SetActive(true);
            if (codeLabel != null) codeLabel.gameObject.SetActive(true);
        }
    }

    public async void OnHostClicked()
    {
        if (hostInProgress) return;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;

        hostInProgress = true;
        int myVersion = ++hostRequestVersion;

        ShowHostCodeOnlyUI(creating: true, code: "");

        string joinCode = await RelayManager.Instance.StartHostWithRelayAsync();

        if (myVersion != hostRequestVersion) return;
        hostInProgress = false;

        if (!string.IsNullOrEmpty(joinCode))
        {
            ShowHostCodeOnlyUI(creating: false, code: joinCode);

            if (codeLabel != null)
            {
                codeLabel.text = joinCode;
                codeLabel.gameObject.SetActive(true);
            }

            if (codeInput != null)
            {
                codeInput.text = joinCode;
                codeInput.interactable = false;
            }
        }
        else
        {
            ShowLobbyButtons(true);

            if (codeLabel != null) codeLabel.text = "";
            if (codeInput != null)
            {
                codeInput.interactable = true;
                codeInput.text = "";
            }
        }
    }

    public async void OnJoinClicked()
    {
        string joinCode = codeInput != null ? codeInput.text.Trim().ToUpper() : "";
        if (string.IsNullOrEmpty(joinCode)) return;

        bool ok = await RelayManager.Instance.StartClientWithRelayAsync(joinCode);
        Debug.Log($"[RelayUI] JoinClicked -> StartClient ok={ok}");

        // Client-side UX only; host will react to SessionFull when connection is real
        if (codeLabel != null) codeLabel.gameObject.SetActive(false);
        if (ok && connectionPanel != null) connectionPanel.SetActive(false);
    }

    private void ShowLobbyButtons(bool show)
    {
        if (hostJoinButtonsRoot != null) hostJoinButtonsRoot.SetActive(show);
        if (joinAreaRoot != null) joinAreaRoot.SetActive(show);
    }

    private void ShowHostCodeOnlyUI(bool creating, string code)
    {
        if (connectionPanel != null) connectionPanel.SetActive(true);

        ShowLobbyButtons(false);

        if (codeInput != null) codeInput.gameObject.SetActive(false);

        if (codeLabel != null)
        {
            codeLabel.gameObject.SetActive(true);
            codeLabel.text = creating ? "Creating room..." : code;
        }
    }
}
