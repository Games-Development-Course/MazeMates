using UnityEngine;
using UnityEngine.UI;

public sealed class WinWindowUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button goToLevelsButton;
    [SerializeField] private Button replayButton;

    [Header("Optional: replay container to hide")]
    [SerializeField] private GameObject replayRoot;

    private void OnEnable()
    {
        ApplyTutorialRules();
        WireButtons();
    }

    private void ApplyTutorialRules()
    {
        bool isTutorial = false;
        if (GameConfigNet.Instance != null)
            isTutorial = GameConfigNet.Instance.IsTutorial.Value;

        // Tutorial => hide Replay
        if (replayButton != null) replayButton.gameObject.SetActive(!isTutorial);
        if (replayRoot != null) replayRoot.SetActive(!isTutorial);
    }

    private void WireButtons()
    {
        if (goToLevelsButton != null)
        {
            goToLevelsButton.onClick.RemoveListener(OnGoToLevels);
            goToLevelsButton.onClick.AddListener(OnGoToLevels);
        }

        if (replayButton != null)
        {
            replayButton.onClick.RemoveListener(OnReplay);
            replayButton.onClick.AddListener(OnReplay);
        }
    }

    private void OnGoToLevels()
    {
        if (PauseConsensus.Instance != null)
            PauseConsensus.Instance.RequestGoToLevels();
    }

    private void OnReplay()
    {
        if (PauseConsensus.Instance != null)
            PauseConsensus.Instance.RequestReplay();
    }
}
