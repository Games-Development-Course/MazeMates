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
    [SerializeField] private GameObject hostButtonsPanel;
    [SerializeField] private LobbyState lobbyState;

    [Header("Easy Config")]
    [SerializeField] private int easyMazeW = 13;
    [SerializeField] private int easyMazeH = 13;
    [SerializeField] private int easyHearts = 3;
    [SerializeField] private int easyBombs = 3;
    [SerializeField] private int easyKeys = 3;
    [SerializeField] private int easyNormalDoors = 3;
    [SerializeField] private int easyPuzzleDoors = 2;

    [Header("Medium Config")]
    [SerializeField] private int medMazeW = 25;
    [SerializeField] private int medMazeH = 25;
    [SerializeField] private int medHearts = 4;
    [SerializeField] private int medBombs = 2;
    [SerializeField] private int medKeys = 2;
    [SerializeField] private int medNormalDoors = 4;
    [SerializeField] private int medPuzzleDoors = 3;

    [Header("Hard Config")]
    [SerializeField] private int hardMazeW = 31;
    [SerializeField] private int hardMazeH = 31;
    [SerializeField] private int hardHearts = 3;
    [SerializeField] private int hardBombs = 3;
    [SerializeField] private int hardKeys = 1;
    [SerializeField] private int hardNormalDoors = 5;
    [SerializeField] private int hardPuzzleDoors = 4;

    // Lives אם אתה רוצה גם כאן—שאירתי כמו קודם (אפשר לשנות ב Inspector)
    [SerializeField] private int easyLives = 3;
    [SerializeField] private int medLives = 2;
    [SerializeField] private int hardLives = 1;

    // NEW: Hints fixed by difficulty (easy=1, med=2, hard=4)
    private const int EASY_HINTS = 1;
    private const int MED_HINTS = 2;
    private const int HARD_HINTS = 4;

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

    public void StartGameEasy() => StartGameWithDifficulty(0);
    public void StartGameMedium() => StartGameWithDifficulty(1);
    public void StartGameHard() => StartGameWithDifficulty(2);

    private void StartGameWithDifficulty(int diff)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        if (nm.ConnectedClientsIds.Count < 2)
        {
            Debug.LogWarning("No clients connected yet!");
            return;
        }

        var cfg = GameConfigNet.Instance;
        if (cfg == null)
        {
            Debug.LogError("GameConfigNet is missing in the Start/Menu scene (NetworkManager scene).");
            return;
        }

        int seed = Random.Range(1, int.MaxValue);

        if (diff == 0)
        {
            // easy: BombRemovals == total bombs (known now)
            cfg.SetConfigServerRpc(
                easyMazeW, easyMazeH,
                easyHearts, easyBombs, easyKeys,
                easyNormalDoors, easyPuzzleDoors,
                0, seed,
                easyLives,
                easyBombs,       // BombRemovals == bombs
                EASY_HINTS       // hints
            );
        }
        else if (diff == 1)
        {
            // medium BombRemovals depends on maze => set placeholder, compute in GameScene after generation
            cfg.SetConfigServerRpc(
                medMazeW, medMazeH,
                medHearts, medBombs, medKeys,
                medNormalDoors, medPuzzleDoors,
                1, seed,
                medLives,
                0,              // placeholder, will be computed
                MED_HINTS
            );
        }
        else
        {
            // hard BombRemovals depends on maze => set placeholder, compute in GameScene after generation
            cfg.SetConfigServerRpc(
                hardMazeW, hardMazeH,
                hardHearts, hardBombs, hardKeys,
                hardNormalDoors, hardPuzzleDoors,
                2, seed,
                hardLives,
                0,              // placeholder, will be computed
                HARD_HINTS
            );
        }

        hostButtonsPanel?.SetActive(false);
        nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}
