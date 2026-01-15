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

    [Header("Host UI (Difficulty Menu)")]
    [Tooltip("גרור לכאן את HostButtonsPanel (תפריט רמות קושי/StartGame)")]
    [SerializeField] private GameObject hostButtonsPanel;

    [Header("Behavior")]
    [SerializeField] private bool hideCodeLabelOnHostWhenReady = true;
    [SerializeField] private bool hideConnectionPanelOnHostWhenReady = true;
    [SerializeField] private bool hideConnectionPanelOnClientWhenReady = true;

    private LobbyState lobbyState;

    private bool hostInProgress;
    private int hostRequestVersion;

    // כדי שלא נכבה תפריט אחרי שפתחנו
    private bool difficultyMenuOpened;
    public System.Action<string> OnJoinCodeReady;


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
        // מחכים שה-NetworkManager באמת קיים (בפרויקט שלך הוא ב-opening/persistent)
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

        // השרת עצמו (בדרך כלל 0)
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
    }

    private void ApplyUiState()
    {
        var nm = NetworkManager.Singleton;

        bool isListening = nm != null && nm.IsListening;
        bool isHost = nm != null && nm.IsHost;
        bool isClientOnly = nm != null && nm.IsClient && !nm.IsHost;

        // Fallback חזק: אם אנחנו Host ורואים יותר מלקוח אחד – סימן שמישהו הצטרף.
        bool hasRealClient =
            isHost && nm != null && nm.ConnectedClientsIds != null && nm.ConnectedClientsIds.Count > 1;

        Debug.Log($"[RelayUI] ApplyUiState | listening={isListening} host={isHost} clientOnly={isClientOnly} hasRealClient={hasRealClient} opened={difficultyMenuOpened}");

        // ברירת מחדל: התפריט סגור עד שמישהו באמת הצטרף
        if (hostButtonsPanel != null && !difficultyMenuOpened)
            hostButtonsPanel.SetActive(false);

        if (!isListening)
        {
            difficultyMenuOpened = false;

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

        // אם כבר יש לקוח אמיתי – נפתח (גם אם פספסנו callback)
        if (!difficultyMenuOpened && hasRealClient)
        {
            OpenDifficultyMenuOnHost();
            return;
        }

        // מצב ביניים: Host מחכה ללקוח => רק קוד
        if (isHost && !difficultyMenuOpened)
        {
            if (connectionPanel != null) connectionPanel.SetActive(true);
            ShowLobbyButtons(false);

            if (codeInput != null) codeInput.gameObject.SetActive(false);
            if (codeLabel != null) codeLabel.gameObject.SetActive(true);
            return;
        }

        // לקוח: אחרי join אפשר להסתיר את הפאנל
        if (isClientOnly && hideConnectionPanelOnClientWhenReady && connectionPanel != null)
        {
            // אם כבר התחבר – לרוב רוצים להסתיר
            // (אם תרצה להשאיר עד סצנה הבאה, תגיד)
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

        // UI: רק קוד
        if (connectionPanel != null) connectionPanel.SetActive(true);
        ShowLobbyButtons(false);
        if (codeInput != null) codeInput.gameObject.SetActive(false);

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
            if (codeLabel != null) {
                codeLabel.text = joinCode;
                OnJoinCodeReady?.Invoke(joinCode);
            }

            // אופציונלי: לשים את הקוד גם בשדה כדי שיהיה קל להעתיק
            if (codeInput != null)
            {
                codeInput.text = joinCode;
                codeInput.interactable = false;
            }

            // תן ApplyUiState לוודא שאנחנו במצב "מחכה ללקוח"
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
