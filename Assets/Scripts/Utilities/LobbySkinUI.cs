// Assets/Scripts/UI/LobbySkinUI.cs
using System.Collections;
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

    // prevents opening multiple times
    private bool openedOnce = false;

    private bool lastHasHebrew = false;
    private bool hadAnyChar = false;
    private Coroutine caretFixRoutine;


    // =========================================================
    // Unity lifecycle
    // =========================================================

    private void Start()
    {
        if (!lobbyState)
            lobbyState = FindFirstObjectByType<LobbyState>();

        if (panelRoot)
            panelRoot.SetActive(false);

        if (statusText)
        {
            statusText.text = "";
            statusText.gameObject.SetActive(false);
        }

        if (nameInput)
            nameInput.interactable = true;
    }

    private void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        if (!lobbyState)
            lobbyState = FindFirstObjectByType<LobbyState>();

        // 1) Open menu when server toggles SkinSelectOpen
        var cfg = GameConfigNet.Instance;
        if (!openedOnce && cfg != null && cfg.IsSpawned && cfg.SkinSelectOpen.Value)
        {
            openedOnce = true;
            OpenSkinMenu();
        }

        // 2) If panel isn't open, nothing else to do
        if (!panelRoot || !panelRoot.activeSelf) return;

        // 3) Status text
        if (statusText)
        {
            if (lobbyState == null || !lobbyState.IsSpawned)
                statusText.text = "מחכה לרשת...";
            else if (!lobbyState.SessionFull.Value)
                statusText.text = "מחכה ששחקן נוסף יתחבר...";
            else if (localReady && !lobbyState.BothReady)
                statusText.text = "מחכה לבחירת השחקן השני...";
            else
                statusText.text = "";
        }

        // 4) Server loads scene
        if (nm.IsServer && lobbyState != null && lobbyState.IsSpawned && lobbyState.BothReady)
        {
            if (cfg != null && cfg.IsSpawned && cfg.SkinSelectOpen.Value)
                cfg.SetSkinSelectOpenServerRpc(false);

            nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }

    // =========================================================
    // Name defaults & direction handling
    // =========================================================

    private string GetDefaultPlayerName()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
            return "שחקן";

        // Client = Traveller = שחקן 1
        // Host   = Navigator = שחקן 2
        return nm.IsServer ? "שחקן 2" : "שחקן 1";
    }

    private static bool ContainsHebrew(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '\u0590' && c <= '\u05FF')
                return true;
        }
        return false;
    }

    private void ApplyInputDirection()
    {
        if (!nameInput || !nameInput.textComponent) return;

        string t = nameInput.text ?? "";
        bool hasHebrew = ContainsHebrew(t);

        // האם כבר התחילו להקליד משהו?
        hadAnyChar = t.Length > 0;

        // עדכון כיוון + יישור
        nameInput.textComponent.isRightToLeftText = hasHebrew;
        nameInput.textComponent.alignment = hasHebrew
            ? TextAlignmentOptions.MidlineRight
            : TextAlignmentOptions.MidlineLeft;

        // אם הכיוון השתנה (למשל עברית->אנגלית או להפך), מתקנים caret בפריים הבא
        if (hadAnyChar && hasHebrew != lastHasHebrew)
        {
            if (caretFixRoutine != null) StopCoroutine(caretFixRoutine);
            caretFixRoutine = StartCoroutine(FixCaretNextFrame());
        }

        lastHasHebrew = hasHebrew;
    }
    private IEnumerator FixCaretNextFrame()
    {
        // מחכים פריים אחד ש-TMP יסיים bidi/layout
        yield return null;

        if (!nameInput) yield break;

        // מרענן את הטקסט הפנימי
        nameInput.ForceLabelUpdate();

        // מזיז את הסמן לקצה הטקסט בצורה בטוחה
        nameInput.MoveTextEnd(false);

        int end = nameInput.text != null ? nameInput.text.Length : 0;

        nameInput.caretPosition = end;
        nameInput.selectionAnchorPosition = end;
        nameInput.selectionFocusPosition = end;
    }


    // =========================================================
    // UI actions
    // =========================================================

    public void OpenSkinMenu()
    {
        localReady = false;

        if (panelRoot)
            panelRoot.SetActive(true);

        if (statusText)
        {
            statusText.text = "";
            statusText.gameObject.SetActive(true);
        }

        if (nameInput)
        {
            nameInput.interactable = true;

            if (string.IsNullOrWhiteSpace(nameInput.text))
                nameInput.text = GetDefaultPlayerName();

            ApplyInputDirection();
        }

        // Host resets readiness
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

    // 🔴 מחובר ל־On Value Changed (String)
    public void OnNameValueChanged(string _)
    {
        if (localReady) return;

        ApplyInputDirection();
        PushToServer();
    }

    public void PressStart()
    {
        localReady = true;
        PushToServer(readyOverride: true);

        if (nameInput)
            nameInput.interactable = false;
    }

    private void PushToServer(bool? readyOverride = null)
    {
        if (lobbyState == null || !lobbyState.IsSpawned)
            return;

        bool ready = readyOverride ?? localReady;

        string name = nameInput ? nameInput.text : "";
        if (string.IsNullOrWhiteSpace(name))
            name = GetDefaultPlayerName();

        lobbyState.SubmitLobbySelectionServerRpc(
            new FixedString32Bytes(name),
            selectedSkin,
            ready
        );
    }
}
