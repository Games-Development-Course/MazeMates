using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyUIAutoHide : MonoBehaviour
{
    [SerializeField] private GameObject lobbyRoot; // כל ה-UI של StartScene
    [SerializeField] private string lobbySceneName = "StartScene";

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (lobbyRoot == null) return;

        bool isLobby = scene.name == lobbySceneName;
        lobbyRoot.SetActive(isLobby);
    }
}
