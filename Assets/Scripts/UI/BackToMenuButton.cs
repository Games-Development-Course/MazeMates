// Assets/Scripts/UI/BackToMenuButton.cs
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BackToMenuButton : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MenuScene";

    public void OnBackToMenuClicked()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || !nm.IsHost) return; // host only

        var cfg = GameConfigNet.Instance;
        if (cfg != null && cfg.IsSpawned)
            cfg.SkinSelectOpen.Value = false;

        var lobby = FindFirstObjectByType<LobbyState>();
        if (lobby != null && lobby.IsSpawned)
        {
            lobby.HostReady.Value = false;
            lobby.ClientReady.Value = false;
        }

        if (SceneManager.GetActiveScene().name == menuSceneName) return;
        nm.SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }
}
