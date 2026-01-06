// ==========================================
// File: Assets/Scripts/UI/EndGamePopupUI.cs
// Local UI controller. Put this on a Canvas in GameScene (and TutorialScene if you want).
// Assign panel + text + buttons in Inspector.
// ==========================================
using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EndGamePopupUI : MonoBehaviour
{
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button backToMenuButton;

    private SessionStateNet state;

    private void Awake()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);

        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(OnPlayAgainClicked);

        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
    }

    private void OnEnable()
    {
        Bind();
        Refresh();
    }

    private void OnDisable()
    {
        if (state != null)
            state.End.OnValueChanged -= OnEndChanged;
    }

    private void Bind()
    {
        if (state == null)
            state = SessionStateNet.Instance;

        if (state != null)
            state.End.OnValueChanged += OnEndChanged;
    }

    private void OnEndChanged(EndState _, EndState __) => Refresh();

    private void Refresh()
    {
        if (rootPanel == null)
            return;

        state = state != null ? state : SessionStateNet.Instance;

        bool ended = state != null && state.IsSpawned && state.End.Value != EndState.None;
        rootPanel.SetActive(ended);

        if (!ended)
            return;

        if (titleLabel != null)
        {
            titleLabel.text = state.End.Value == EndState.Win
                ? "You escaped! 🎉"
                : "Game Over 💥";
        }

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        // Host is authoritative for scene actions.
        if (playAgainButton != null)
            playAgainButton.interactable = isHost;

        if (backToMenuButton != null)
            backToMenuButton.interactable = isHost;
    }

    private void OnPlayAgainClicked()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsHost)
            return;

        SessionStateNet.Instance.RestartSameGameServerRpc();
    }

    private void OnBackToMenuClicked()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsHost)
            return;

        SessionStateNet.Instance.BackToMenuServerRpc();
    }
}
