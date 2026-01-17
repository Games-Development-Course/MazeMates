// Assets/Scripts/UI/LobbySkinUI.cs
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LobbySkinUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_Text statusText;

    [Header("Net")]
    [SerializeField] private LobbyState lobbyState;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "GameScene";

    private int selectedSkin = 0;
    private bool localReady = false;

    // ✅ prevents opening multiple times
    private bool openedOnce = false;

    private void Start()
    {
        if (!lobbyState) lobbyState = FindFirstObjectByType<LobbyState>();

        if (panelRoot) panelRoot.SetActive(false);

        if (statusText)
        {
            statusText.text = "";
            statusText.gameObject.SetActive(false); // ✅ off by default
        }

        if (nameInput) nameInput.interactable = true;
    }

    private void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        if (!lobbyState) lobbyState = FindFirstObjectByType<LobbyState>();

        // ✅ 1) Open menu when server toggles SkinSelectOpen
        var cfg = GameConfigNet.Instance;
        if (!openedOnce && cfg != null && cfg.IsSpawned && cfg.SkinSelectOpen.Value)
        {
            openedOnce = true;
            OpenSkinMenu();
        }

        // ✅ 2) If panel isn't open, nothing else to do
        if (!panelRoot || !panelRoot.activeSelf) return;

        // ✅ 3) Update status text only when panel is open
        if (statusText)
        {
            if (lobbyState == null || !lobbyState.IsSpawned)
            {
                statusText.text = "מחכה לרשת...";
            }
            else if (!lobbyState.SessionFull.Value)
            {
                statusText.text = "מחכה ששחקן נוסף יתחבר...";
            }
            else if (localReady && !lobbyState.BothReady)
            {
                statusText.text = "מחכה לבחירת השחקן השני...";
            }
            else
            {
                statusText.text = "";
            }
        }

        // ✅ 4) Only server loads scene when both ready
        if (nm.IsServer && lobbyState != null && lobbyState.IsSpawned && lobbyState.BothReady)
        {
            // close flag so it won't re-open on refresh
            if (cfg != null && cfg.IsSpawned && cfg.SkinSelectOpen.Value)
                cfg.SetSkinSelectOpenServerRpc(false);

            nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }

    public void OpenSkinMenu()
    {
        localReady = false;

        if (panelRoot) panelRoot.SetActive(true);

        if (statusText)
        {
            statusText.text = "";
            statusText.gameObject.SetActive(true);
        }

        if (nameInput)
        {
            nameInput.interactable = true;
            if (string.IsNullOrWhiteSpace(nameInput.text))
                nameInput.text = "Player";
        }

        // ✅ host resets readiness when opening menu (new difficulty selection)
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer &&
            lobbyState != null &&
            lobbyState.IsSpawned)
        {
            lobbyState.ResetReadiesServerRpc();
        }

        PushToServer();
    }

    public void SelectSkin(int index)
    {
        if (localReady) return;
        selectedSkin = Mathf.Clamp(index, 0, 3);
        PushToServer();
    }

    public void OnNameEdited()
    {
        if (localReady) return;
        PushToServer();
    }

    public void PressStart()
    {
        localReady = true;
        PushToServer(readyOverride: true);

        if (nameInput) nameInput.interactable = false;
    }

    private void PushToServer(bool? readyOverride = null)
    {
        if (lobbyState == null || !lobbyState.IsSpawned) return;

        bool ready = readyOverride ?? localReady;

        string n = nameInput ? nameInput.text : "Player";
        if (string.IsNullOrWhiteSpace(n)) n = "Player";

        lobbyState.SubmitLobbySelectionServerRpc(new FixedString32Bytes(n), selectedSkin, ready);
    }
}
