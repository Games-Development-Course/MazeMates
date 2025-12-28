using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostStartGame : MonoBehaviour
{
    [SerializeField] private string tutorialSceneName = "TutorialScene";
    [SerializeField] private string gameSceneName = "GameScene";

    public void StartTutorial()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
        NetworkManager.Singleton.SceneManager.LoadScene(tutorialSceneName, LoadSceneMode.Single);
    }

   public void StartGame()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (NetworkManager.Singleton.ConnectedClients.Count <= 1)
        {
            Debug.LogWarning("No clients connected yet!");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(
            gameSceneName,
            LoadSceneMode.Single
        );
    }


    public void click()
    {
        Debug.Log($"[SceneEvent] click");
    }
}
