// Assets/Scripts/Utilities/EditorQuickStart.cs
// Attach ONCE in StartScene (the scene with NetworkManager + GameConfigNet + HostStartGame flow).
// No relay, no join codes, no clients.
// Play -> StartHost (local) -> set GameConfigNet criteria -> load GameScene.
// Your existing spawn system should spawn Traveller normally (same as real flow).

using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public sealed class EditorQuickStart : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Run")]
    [SerializeField] private bool runOnPlay = true;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Criteria (like StartGameEasy)")]
    [SerializeField] private int mazeW = 11;
    [SerializeField] private int mazeH = 11;
    [SerializeField] private int hearts = 3;
    [SerializeField] private int bombs = 3;
    [SerializeField] private int keys = 3;
    [SerializeField] private int normalDoors = 3;
    [SerializeField] private int puzzleDoors = 2;
    [SerializeField] private int difficulty = 0; // 0 easy, 1 med, 2 hard

    private void Awake()
    {
        if (!runOnPlay) { enabled = false; return; }
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Run();
    }

    private void Run()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // 1) Start Host locally (NO relay / NO codes)
        if (!nm.IsListening)
            nm.StartHost();

        // 2) Set config exactly like HostStartGame does
        var cfg = GameConfigNet.Instance;
        if (cfg != null)
        {
            int seed = Random.Range(1, int.MaxValue);
            cfg.SetConfigServerRpc(mazeW, mazeH, hearts, bombs, keys, normalDoors, puzzleDoors, difficulty, seed);
        }

        // 3) Load GameScene (same as your menu flow)
        if (nm.SceneManager != null)
            nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
#endif
}
