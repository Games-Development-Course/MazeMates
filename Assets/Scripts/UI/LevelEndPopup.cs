using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

public class LevelEndPopup : MonoBehaviour
{
    [Header("Popup")]
    public GameObject popupRoot; // panel to enable/disable
    public TMP_Text titleText;

    [Header("Buttons")]
    public Button restartButton;
    public Button backToMenuButton;

    [Header("Scenes")]
    [Tooltip("Name of the menu/start scene to load when returning to menu")]
    public string menuSceneName = "StartScene";

    private void Awake()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelEnded += ShowPopup;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelEnded -= ShowPopup;
    }

    private void ShowPopup()
    {
        if (popupRoot == null)
            return;

        popupRoot.SetActive(true);
    }

    private void HidePopup()
    {
        if (popupRoot == null)
            return;

        popupRoot.SetActive(false);
    }

    private void OnRestartClicked()
    {
        HidePopup();
        var current = SceneManager.GetActiveScene().name;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(current, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(current, LoadSceneMode.Single);
        }
    }

    private void OnBackToMenuClicked()
    {
        HidePopup();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
        }
    }
}
