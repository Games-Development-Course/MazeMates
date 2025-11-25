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
            Debug.Log("WinTrigger: Player detected — loading scene: " + sceneName);
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.Log("WinTrigger: object is NOT the Player (tag = " + other.tag + ")");
        }
    }
}
