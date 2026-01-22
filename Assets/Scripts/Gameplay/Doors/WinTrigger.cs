using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WinTrigger : MonoBehaviour
{
    public string sceneName = "WinGame";

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("WinTrigger: ENTER detected with object = " + other.name);

        if (other.CompareTag("Player"))
        {
            Debug.Log("WinTrigger: Player detected — ending level (invoking GameManager.EndLevel)");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndLevel();
            }
            else
            {
                Debug.Log("WinTrigger: GameManager.Instance is null — falling back to loading scene " + sceneName);
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
        }
        else
        {
            Debug.Log("WinTrigger: object is NOT the Player (tag = " + other.tag + ")");
        }
    }
}
