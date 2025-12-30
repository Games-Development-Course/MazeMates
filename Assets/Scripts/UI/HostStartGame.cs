// Assets/Scripts/UI/HostStartGame.cs
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HostStartGame : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string tutorialSceneName = "TutorialScene";
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Host-only UI")]
    [SerializeField] private GameObject hostButtonsPanel; // parent object holding 4 buttons
    [SerializeField] private LobbyState lobbyState;       // drag the LobbyState instance here

    private void Awake()
    {
        if (hostButtonsPanel != null)
            hostButtonsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        TryBind();
        ApplyVisibility();
    }

    private void OnDisable()
    {
        if (lobbyState != null)
            lobbyState.SessionFull.OnValueChanged -= OnSessionFullChanged;
    }

    private void TryBind()
    {
        if (lobbyState == null)
            lobbyState = FindFirstObjectByType<LobbyState>();

        if (lobbyState != null)
            lobbyState.SessionFull.OnValueChanged += OnSessionFullChanged;
    }

    private void OnSessionFullChanged(bool _, bool __) => ApplyVisibility();

    private void ApplyVisibility()
    {
        if (hostButtonsPanel == null) return;

        var nm = NetworkManager.Singleton;
        bool isHost = nm != null && nm.IsHost;
        bool full = lobbyState != null && lobbyState.IsSpawned && lobbyState.SessionFull.Value;

        hostButtonsPanel.SetActive(isHost && full);
    }

    public void StartTutorial()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        nm.SceneManager.LoadScene(tutorialSceneName, LoadSceneMode.Single);
    }

    public void StartGameEasy() => StartGame();
    public void StartGameMedium() => StartGame();
    public void StartGameHard() => StartGame();

    private void StartGame()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        if (nm.ConnectedClientsIds.Count < 2)
        {
            Debug.LogWarning("No clients connected yet!");
            return;
        }
        hostButtonsPanel.SetActive(false);

        nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}
