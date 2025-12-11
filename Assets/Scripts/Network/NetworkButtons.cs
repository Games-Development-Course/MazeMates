using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class NetworkButtons : MonoBehaviour
{
    public enum NetworkModeChoice
    {
        Host,
        Client,
        HostClient,   // משמש את GameManager כדי לזהות מצב Host+Client
        Shared = HostClient
    }

    [Header("Runner & Scene")]
    [SerializeField] private NetworkRunner runner;
    [SerializeField] private NetworkSceneManagerDefault sceneManager;

    [Header("UI")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private GameObject enterRoomPanel;
    [SerializeField] private GameObject inRoomPanel;

    [Header("Config")]
    public NetworkModeChoice modeChoice = NetworkModeChoice.HostClient;
    public string defaultSessionName = "MazeMatesRoom";

    private bool _isStarting;

    // ============================================================
    // PUBLIC API — GameManager משתמש בזה
    // ============================================================

    public async void StartHost()
    {
        if (_isStarting) return;
        _isStarting = true;

        var mode = GameMode.Shared;
        if (modeChoice == NetworkModeChoice.Host)
            mode = GameMode.Host;
        else if (modeChoice == NetworkModeChoice.Client)
            mode = GameMode.Client;
        else // HostClient / Shared
            mode = GameMode.Shared;

        await StartGameInternal(mode);
        _isStarting = false;
    }

    public async void StartClient()
    {
        if (_isStarting) return;
        _isStarting = true;

        // ב־Join תמיד Client (גם אם המשחק רץ במצב Shared אצל ה־Host)
        await StartGameInternal(GameMode.Client);
        _isStarting = false;
    }

    // כפתור Join במסך ה־UI יכול לקרוא לזה אם את רוצה "Shared" בשם
    public void StartClientShared()
    {
        StartClient();
    }

    // ============================================================
    // INTERNAL
    // ============================================================

    private NetworkSceneInfo BuildSceneInfo()
    {
        var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var info = new NetworkSceneInfo();

        if (sceneRef.IsValid)
        {
            // מריצים את הרץ על הסצנה הנוכחית (הטוטוריאל)
            info.AddSceneRef(sceneRef, LoadSceneMode.Single);
        }

        return info;
    }

    private async Task StartGameInternal(GameMode mode)
    {
        if (runner == null)
            runner = FindObjectOfType<NetworkRunner>();

        if (runner == null)
        {
            runner = gameObject.AddComponent<NetworkRunner>();
            runner.ProvideInput = true;
        }

        if (runner.IsRunning)
        {
            Debug.LogWarning("[NetworkButtons] Runner already running, ignoring StartGame.");
            return;
        }

        if (sceneManager == null)
        {
            sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null)
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        var sceneInfo = BuildSceneInfo();

        string sessionName = defaultSessionName;
        if (roomCodeInput != null && !string.IsNullOrEmpty(roomCodeInput.text))
            sessionName = roomCodeInput.text;

        try
        {
            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                Scene = sceneInfo,
                SceneManager = sceneManager
            });

            if (result.Ok)
            {
                Debug.Log("[NetworkButtons] StartGame OK");

                if (enterRoomPanel != null)
                    enterRoomPanel.SetActive(false);

                if (inRoomPanel != null)
                    inRoomPanel.SetActive(true);
            }
            else
            {
                Debug.LogError($"[NetworkButtons] StartGame failed: {result.ShutdownReason}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkButtons] StartGame exception: {e}");
        }
    }
}
