using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public enum LevelEndState
{
    Win = 0,
    Lose = 1
}

public class LevelEndUI : MonoBehaviour
{
    public static LevelEndUI Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    [Header("Buttons")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        root.SetActive(false);

        restartButton.onClick.AddListener(OnRestartClicked);
        mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    public void Show(LevelEndState state)
    {
        if (root == null) return;

        // If we're hidden because parent HUD is disabled, move to a global canvas
        if (!root.activeInHierarchy)
        {
            var globalCanvas = GameObject.Find("GlobalOverlayCanvas");
            if (globalCanvas != null)
                root.transform.SetParent(globalCanvas.transform, worldPositionStays: false);
        }

        root.SetActive(true);
        root.transform.SetAsLastSibling();
        Debug.Log($"[LevelEndUI] root activeSelf={root.activeSelf} activeInHierarchy={root.activeInHierarchy}");
        Debug.Log($"[LevelEndUI] parent GO activeInHierarchy={root.transform.parent.gameObject.activeInHierarchy}");

        // Bring to front
        root.transform.SetAsLastSibling();

        // Force canvas to be on top
        var canvas = root.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            Debug.Log($"[LevelEndUI] Canvas='{canvas.name}' mode={canvas.renderMode} order={canvas.sortingOrder}");
        }
        else
        {
            Debug.LogWarning("[LevelEndUI] No parent Canvas found.");
        }

        // If you use CanvasGroup, force it visible
        var cg = root.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        // Cursor/UI friendliness
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        // Only host can actually change scenes (recommended)
        restartButton.interactable = isHost;
        mainMenuButton.interactable = isHost;

        switch (state)
        {
            case LevelEndState.Win:
                titleText.text = "Level Completed!";
                bodyText.text = "You both reached the control room.";
                break;

            case LevelEndState.Lose:
                titleText.text = "Game Over";
                bodyText.text = "No lives left.";
                break;
        }
    }

    public void Hide()
    {
        root.SetActive(false);
    }

    private void OnRestartClicked()
    {
        if (LevelFlowManager.Instance != null)
            LevelFlowManager.Instance.RequestRestart();
    }

    private void OnMainMenuClicked()
    {
        if (LevelFlowManager.Instance != null)
            LevelFlowManager.Instance.RequestMainMenu();
    }
}
