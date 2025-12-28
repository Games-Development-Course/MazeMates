using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using FixedString = Unity.Collections.FixedString64Bytes;
using System;
public class StartSceneManager : NetworkBehaviour
{
    [Header("UI")]
    public GameObject hostPanel;
    public GameObject clientPanel;
    public GameObject connectionPanel;

    public TMP_InputField hostNameInput;
    public TMP_InputField clientNameInput;

    public TMP_Text selectedModeText;

    [Header("Scenes")]
    public string gameSceneName = "GameScene";
    public string tutorialSceneName = "TutorialScene";


    // ─────────────────────────────
    // Networked state
    // ─────────────────────────────

    private NetworkVariable<string> hostName =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<string> clientName =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<GameMode> selectedMode =
        new(GameMode.Easy, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        // Hide connection UI for everyone
        connectionPanel.SetActive(false);

        if (IsHost)
        {
            hostPanel.SetActive(true);
            clientPanel.SetActive(false);
        }
        else
        {
            hostPanel.SetActive(false);
            clientPanel.SetActive(true);
        }
    }


    // ─────────────────────────────
    // UI → Network
    // ─────────────────────────────

    public void OnHostNameChanged(string value)
    {
        if (IsOwner)
            hostName.Value = value;
    }

    public void OnClientNameChanged(string value)
    {
        if (IsOwner)
            clientName.Value = value;
    }

    public void SelectEasy()    => SetModeServerRpc(GameMode.Easy);
    public void SelectMedium()  => SetModeServerRpc(GameMode.Medium);
    public void SelectHard()    => SetModeServerRpc(GameMode.Hard);
    public void SelectTutorial()=> SetModeServerRpc(GameMode.Tutorial);

    [ServerRpc(RequireOwnership = false)]
    void SetModeServerRpc(GameMode mode)
    {
        selectedMode.Value = mode;
    }

    void OnModeChanged(GameMode oldMode, GameMode newMode)
    {
        selectedModeText.text = $"Mode: {newMode}";
    }

    // ─────────────────────────────
    // START GAME
    // ─────────────────────────────

    public void OnStartGamePressed()
    {
        if (!IsHost)
            return;

        // Store for next scene
        GameSessionData.HostName = hostName.Value.ToString();
        GameSessionData.ClientName = clientName.Value.ToString();
        GameSessionData.SelectedMode = selectedMode.Value;

        string sceneToLoad =
            selectedMode.Value == GameMode.Tutorial
            ? tutorialSceneName
            : gameSceneName;

        NetworkManager.SceneManager.LoadScene(
            sceneToLoad,
            LoadSceneMode.Single
        );
    }
    void Start()
    {
        connectionPanel.SetActive(true);
        hostPanel.SetActive(false);
        clientPanel.SetActive(false);
    }

}
