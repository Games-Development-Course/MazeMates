// Assets/Scripts/Net/SessionSceneRouter.cs
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class SessionSceneRouter : NetworkBehaviour
{
    public static SessionSceneRouter Instance { get; private set; }

    [Header("Scenes (must exist in Build Settings)")]
    [SerializeField] private string menuSceneName = "StartScene"; // or "Menu"

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

    public void BackToMenu()
    {
        if (IsServer)
            LoadMenuAsHost();
        else
            BackToMenuServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void BackToMenuServerRpc()
    {
        LoadMenuAsHost();
    }

    private void LoadMenuAsHost()
    {
        if (!IsServer) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Reset any “in-game” toggles that would otherwise leave menu in a weird state.
        if (GameConfigNet.Instance != null && GameConfigNet.Instance.IsSpawned)
            GameConfigNet.Instance.SetSkinSelectOpenServerRpc(false);

        if (LobbyState.Instance != null && LobbyState.Instance.IsSpawned)
            LobbyState.Instance.ResetReadiesServerRpc();

        if (SceneManager.GetActiveScene().name == menuSceneName)
            return;

        nm.SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }
}
