// File: Assets/Scripts/Utilities/CornerUIButtons.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CornerUIButtons : MonoBehaviour
{
    public enum LayoutDirection { VerticalUp, HorizontalLeft }

    [Header("Scene Rules")]
    [SerializeField] private string targetScene = "GameScene";
    [SerializeField] private bool destroyPersistentOnTargetSceneLoad = false;

    [Header("Layout (optional)")]
    [SerializeField] private RectTransform boardRect;
    [SerializeField] private LayoutDirection direction = LayoutDirection.VerticalUp;
    [SerializeField] private List<RectTransform> buttonsToLayout = new List<RectTransform>();
    [SerializeField] private float paddingRight = 16f;
    [SerializeField] private float paddingBottom = 16f;
    [SerializeField] private float spacing = 10f;

    [Header("Help Button")]
    [SerializeField] private Button helpButton;
    [SerializeField] private Transform instructionsPopup; // PopupWindows/HelpWindow

    [Header("Mute Button (toggle)")]
    [SerializeField] private Button muteToggleButton;
    [SerializeField] private Sprite volumeOnSprite;
    [SerializeField] private Sprite muteSprite;
    [SerializeField] private List<GameObject> audioObjects = new List<GameObject>();

    [Header("Initial State")]
    [SerializeField] private bool startMuted = false;

    [Header("Help Screens")]
    [SerializeField] private GameObject helpScreen1;
    [SerializeField] private GameObject helpScreen2;
    [SerializeField] private GameObject helpScreen3;
    [SerializeField] private GameObject helpScreen4;

    // ==================== Room Code ====================
    [Header("Room Code (toggle)")]
    [SerializeField] private Button toggleRoomCodeButton; // CornerUI/RoomCode
    [SerializeField] private GameObject roomCodeWindow;   // HUD/RoomCodeScreen
    [SerializeField] private TMP_Text roomCodeText;       // RoomCodeScreen/Code

    [Tooltip("Optional: show ___ when empty")]
    [SerializeField] private string emptyPlaceholder = "___";
    // ===================================================

    // ==================== Pause ====================
    [Header("Pause Menu (Game only)")]
    [SerializeField] private Button pauseButton;          // CornerUI/Pause
    [SerializeField] private GameObject pauseWindow;      // PopupWindows/PauseWindow
    [SerializeField] private Button pauseContinueButton;  // PauseWindow/ExitButton (אצלך)
    [SerializeField] private Button pauseReplayButton;    // PauseWindow/PlayAgain
    [SerializeField] private Button pauseLevelsButton;    // PauseWindow/ReturnToLevels

    [Header("Confirm Window (Local)")]
    [SerializeField] private GameObject confirmWindow;    // PopupWindows/ConfirmWindow
    [SerializeField] private TMP_Text confirmText;        // ConfirmWindow/Text (TMP)
    [SerializeField] private Button confirmYesButton;     // ConfirmWindow/Yes
    [SerializeField] private Button confirmNoButton;      // ConfirmWindow/No

    [Header("Peer Request Window (Other player)")]
    [SerializeField] private GameObject peerRequestWindow; // PopupWindows/PeerRequestWindow
    [SerializeField] private TMP_Text peerRequestText;     // PeerRequestWindow/Text (TMP)
    [SerializeField] private Button peerYesButton;         // PeerRequestWindow/Yes
    [SerializeField] private Button peerNoButton;          // PeerRequestWindow/No

    [Header("Toast / Message")]
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private float toastSeconds = 2.5f;

   [Header("Win Screen")]
    [SerializeField] private GameObject WinWindow;     // PopupWindows/WinWindow (או השם אצלך)
    [SerializeField] private Button ReturnToLevels;   // WinWindow/PlayAgain (או השם אצלך)

    [Header("Lose Screen")]
    [SerializeField] private GameObject loseWindow;     // PopupWindows/LooseWindow  (או LoseWindow)
    [SerializeField] private Button loseReplayButton;   // אופציונלי: LooseWindow/PlayAgain
    [SerializeField] private Button loseLevelsButton;   // אופציונלי: LooseWindow/ReturnToLevels

    private PauseConsensus.PauseAction _pendingLocalAction;
    private Coroutine _toastCo;

    // --- Confirm flow state (NEW) ---
    private bool _waitingPeerDecision;
    private Coroutine _closeConfirmCo;

    // ===================================================

    private bool isMuted;
    private Image muteButtonImage;

    private Coroutine _probeCo;
    private bool _boundToStore;

    // Roots יחסית ל-CornerUI
    private Transform _cornerRoot; // CornerUI
    private Transform _hudRoot;    // TravellerHUD / NavigatorHUD
    private Transform _popupRoot;  // HUD/PopupWindows

    // --------------------------------------------------

    private void Awake()
    {
        Debug.Log($"[CornerUIButtons][Awake] scene={gameObject.scene.name} this={transform.name} id={GetInstanceID()}");

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        ResolveAllRefs(force: true);

        if (muteToggleButton != null)
            muteButtonImage = muteToggleButton.GetComponent<Image>();

        UnwireButtons();
        WireButtons();

        isMuted = startMuted;
        ApplyMuteState();

        if (roomCodeWindow != null) roomCodeWindow.SetActive(false);

        SafeHidePauseUI();
        ApplyPauseVisibility();
    }

    private void OnEnable()
    {
        Debug.Log($"[CornerUIButtons][OnEnable] scene={gameObject.scene.name} this={transform.name} id={GetInstanceID()}");

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        ResolveAllRefs(force: false);

        UnwireButtons();
        WireButtons();

        BindToRoomCodeStore();

        ApplyPauseVisibility();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindFromRoomCodeStore();

        if (_probeCo != null) { StopCoroutine(_probeCo); _probeCo = null; }
        if (_closeConfirmCo != null) { StopCoroutine(_closeConfirmCo); _closeConfirmCo = null; }
    }

    private void OnDestroy()
    {
        UnwireButtons();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindFromRoomCodeStore();

        if (_probeCo != null) { StopCoroutine(_probeCo); _probeCo = null; }
        if (_toastCo != null) { StopCoroutine(_toastCo); _toastCo = null; }
        if (_closeConfirmCo != null) { StopCoroutine(_closeConfirmCo); _closeConfirmCo = null; }
    }

    private void OnSceneLoaded(Scene loadedScene, LoadSceneMode mode)
    {
        if (loadedScene.name != targetScene) return;

        if (gameObject.scene.name != targetScene)
        {
            Debug.LogWarning($"[CornerUIButtons] Target scene '{targetScene}' loaded, disabling old instance from scene '{gameObject.scene.name}'. this={transform.name} id={GetInstanceID()}");

            UnwireButtons();

            if (destroyPersistentOnTargetSceneLoad)
            {
                Destroy(gameObject);
                return;
            }

            enabled = false;
        }
    }

    // ============================================================
    // ✅ AUTO RESOLVE - יחסית ל-CornerUI בהיררכיה שלך
    // ============================================================

    [ContextMenu("Resolve UI References (FORCE)")]
    private void ResolveRefsForceMenu() => ResolveAllRefs(force: true);

    private void ResolveAllRefs(bool force)
    {
        CacheRoots();

        if (_cornerRoot == null)
        {
            Debug.LogWarning($"[CornerUIButtons] ResolveAllRefs: CornerUI root not found. this={GetFullPath(transform)}");
            return;
        }

        // Layout base (לא חובה לשימוש)
        if (force || boardRect == null)
            boardRect = _cornerRoot.GetComponent<RectTransform>();

        // Corner buttons
        if (force || helpButton == null) helpButton = FindButton(_cornerRoot, "Help");
        if (force || muteToggleButton == null) muteToggleButton = FindButton(_cornerRoot, "Mute");
        if (force || pauseButton == null) pauseButton = FindButton(_cornerRoot, "Pause");
        if (force || toggleRoomCodeButton == null) toggleRoomCodeButton = FindButton(_cornerRoot, "RoomCode");

        // Auto buttons list for layout (לא מזיזים בפועל אם אתה לא קורא LayoutButtons)
        if (force || buttonsToLayout == null || buttonsToLayout.Count == 0)
        {
            buttonsToLayout ??= new List<RectTransform>();
            buttonsToLayout.Clear();

            AddButtonRect(helpButton);
            AddButtonRect(muteToggleButton);
            AddButtonRect(pauseButton);
            AddButtonRect(toggleRoomCodeButton);
        }

        // PopupWindows root
        if (_popupRoot != null)
        {
            if (force || instructionsPopup == null)
                instructionsPopup = FindChild(_popupRoot, "HelpWindow");

            if (instructionsPopup != null)
            {
                if (force || helpScreen1 == null) helpScreen1 = FindChildGO(instructionsPopup, "HelpScreen1");
                if (force || helpScreen2 == null) helpScreen2 = FindChildGO(instructionsPopup, "HelpScreen2");
                if (force || helpScreen3 == null) helpScreen3 = FindChildGO(instructionsPopup, "HelpScreen3");
                if (force || helpScreen4 == null) helpScreen4 = FindChildGO(instructionsPopup, "HelpScreen4");
            }

            if (force || pauseWindow == null) pauseWindow = FindChildGO(_popupRoot, "PauseWindow");
            if (pauseWindow != null)
            {
                // אצלך: PauseWindow/PlayAgain + ReturnToLevels + ExitButton
                if (force || pauseReplayButton == null) pauseReplayButton = FindButton(pauseWindow.transform, "PlayAgain");
                if (force || pauseLevelsButton == null) pauseLevelsButton = FindButton(pauseWindow.transform, "ReturnToLevels");
                if (force || pauseContinueButton == null) pauseContinueButton = FindButton(pauseWindow.transform, "ExitButton"); // ה"המשך" אצלך
            }

            if (force || confirmWindow == null) confirmWindow = FindChildGO(_popupRoot, "ConfirmWindow");
            if (confirmWindow != null)
            {
                if (force || confirmYesButton == null) confirmYesButton = FindButton(confirmWindow.transform, "Yes");
                if (force || confirmNoButton == null) confirmNoButton = FindButton(confirmWindow.transform, "No");
                if (force || confirmText == null) confirmText = FindTMPByContains(confirmWindow.transform, "Text");
            }

            if (force || peerRequestWindow == null) peerRequestWindow = FindChildGO(_popupRoot, "PeerRequestWindow");
            if (peerRequestWindow != null)
            {
                if (force || peerYesButton == null) peerYesButton = FindButton(peerRequestWindow.transform, "Yes");
                if (force || peerNoButton == null) peerNoButton = FindButton(peerRequestWindow.transform, "No");
                if (force || peerRequestText == null) peerRequestText = FindTMPByContains(peerRequestWindow.transform, "Text");
            }
            if (force || WinWindow == null)
                WinWindow = FindChildGO(_popupRoot, "WinWindow"); // שנה אם השם אחר

            if (WinWindow != null)
            {
                if (force || ReturnToLevels == null)
                    ReturnToLevels = FindButton(WinWindow.transform, "ReturnToLevels"); // שנה אם השם אחר
            }
            // ---- LoseWindow ----
            if (force || loseWindow == null)
            {
                // תומך בשני שמות כדי שלא תיתקע על טעות כתיב
                loseWindow = FindChildGO(_popupRoot, "LooseWindow");
                if (loseWindow == null)
                    loseWindow = FindChildGO(_popupRoot, "LoseWindow");
            }

            if (loseWindow != null)
            {
                if (force || loseReplayButton == null)
                    loseReplayButton = FindButton(loseWindow.transform, "PlayAgain");

                if (force || loseLevelsButton == null)
                    loseLevelsButton = FindButton(loseWindow.transform, "ReturnToLevels");
            }




        }

        // RoomCodeScreen (לפי ההיררכיה שלך הוא תחת ה-HUD, לא תחת PopupWindows)
        if (force || roomCodeWindow == null)
            roomCodeWindow = (_hudRoot != null) ? FindChildGO(_hudRoot, "RoomCodeScreen") : null;

        if ((force || roomCodeText == null) && roomCodeWindow != null)
            roomCodeText = FindTMP(roomCodeWindow.transform, "Code");

        if (muteToggleButton != null)
            muteButtonImage = muteToggleButton.GetComponent<Image>();

        Debug.Log(
            $"[CornerUIButtons][Resolve] hud={(_hudRoot ? _hudRoot.name : "NULL")} " +
            $"corner={GetFullPath(_cornerRoot)} popup={(_popupRoot ? GetFullPath(_popupRoot) : "NULL")} " +
            $"pauseBtn={(pauseButton ? "OK" : "NULL")} pauseWin={(pauseWindow ? "OK" : "NULL")} roomCodeWin={(roomCodeWindow ? "OK" : "NULL")}"
        );

        void AddButtonRect(Button b)
        {
            if (!b) return;
            var rt = b.GetComponent<RectTransform>();
            if (rt) buttonsToLayout.Add(rt);
        }
    }

    private void CacheRoots()
    {
        // CornerUI root
        _cornerRoot = transform;
        var t = transform;
        while (t != null && t.name != "CornerUI")
            t = t.parent;
        if (t != null) _cornerRoot = t;

        // HUD root: TravellerHUD / NavigatorHUD
        _hudRoot = _cornerRoot;
        while (_hudRoot != null && _hudRoot.name != "TravellerHUD" && _hudRoot.name != "NavigatorHUD")
            _hudRoot = _hudRoot.parent;

        // PopupWindows
        _popupRoot = null;
        if (_hudRoot != null)
        {
            _popupRoot = _hudRoot.Find("PopupWindows");
            if (_popupRoot == null)
                _popupRoot = FindDeepChild(_hudRoot, "PopupWindows");
        }
    }

    // ============================================================
    // RoomCode Store binding
    // ============================================================
    private void BindToRoomCodeStore()
    {
        if (_boundToStore) return;
        _boundToStore = true;
        StartCoroutine(BindStoreWhenReady());
    }

    private IEnumerator BindStoreWhenReady()
    {
        float t = 0f;
        while (RoomCodeStore.Instance == null && t < 2f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (RoomCodeStore.Instance != null)
        {
            RoomCodeStore.Instance.OnJoinCodeChanged -= OnJoinCodeChanged;
            RoomCodeStore.Instance.OnJoinCodeChanged += OnJoinCodeChanged;
            RoomCodeStore.Instance.TryLoadFromFile();
        }

        RefreshRoomCodeText();
    }

    private void UnbindFromRoomCodeStore()
    {
        if (!_boundToStore) return;
        _boundToStore = false;

        if (RoomCodeStore.Instance != null)
            RoomCodeStore.Instance.OnJoinCodeChanged -= OnJoinCodeChanged;
    }

    private void OnJoinCodeChanged(string _) => RefreshRoomCodeText();

    private void RefreshRoomCodeText()
    {
        if (roomCodeText == null) return;

        if (RoomCodeStore.Instance != null)
            RoomCodeStore.Instance.TryLoadFromFile();

        string code = (RoomCodeStore.Instance != null) ? RoomCodeStore.Instance.JoinCode : "";
        roomCodeText.text = string.IsNullOrWhiteSpace(code) ? emptyPlaceholder : code;
    }

    // ============================================================
    // Wiring
    // ============================================================
    private void WireButtons()
    {
        if (helpButton != null) helpButton.onClick.AddListener(ToggleHelp);
        if (muteToggleButton != null) muteToggleButton.onClick.AddListener(ToggleMute);
        if (toggleRoomCodeButton != null) toggleRoomCodeButton.onClick.AddListener(ToggleRoomCode);

        if (pauseButton != null) pauseButton.onClick.AddListener(OpenPause);
        if (pauseContinueButton != null) pauseContinueButton.onClick.AddListener(ClosePause);
        if (pauseReplayButton != null) pauseReplayButton.onClick.AddListener(OnPauseReplayClicked);
        if (pauseLevelsButton != null) pauseLevelsButton.onClick.AddListener(OnPauseLevelsClicked);

        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (peerYesButton != null) peerYesButton.onClick.AddListener(OnPeerYesClicked);
        if (peerNoButton != null) peerNoButton.onClick.AddListener(OnPeerNoClicked);

        if (loseReplayButton != null) loseReplayButton.onClick.AddListener(OnPauseReplayClicked);
        if (loseLevelsButton != null) loseLevelsButton.onClick.AddListener(OnPauseLevelsClicked);

    }

    private void UnwireButtons()
    {
        if (helpButton != null) helpButton.onClick.RemoveListener(ToggleHelp);
        if (muteToggleButton != null) muteToggleButton.onClick.RemoveListener(ToggleMute);
        if (toggleRoomCodeButton != null) toggleRoomCodeButton.onClick.RemoveListener(ToggleRoomCode);

        if (pauseButton != null) pauseButton.onClick.RemoveListener(OpenPause);
        if (pauseContinueButton != null) pauseContinueButton.onClick.RemoveListener(ClosePause);
        if (pauseReplayButton != null) pauseReplayButton.onClick.RemoveListener(OnPauseReplayClicked);
        if (pauseLevelsButton != null) pauseLevelsButton.onClick.RemoveListener(OnPauseLevelsClicked);

        if (confirmYesButton != null) confirmYesButton.onClick.RemoveListener(OnConfirmYes);
        if (confirmNoButton != null) confirmNoButton.onClick.RemoveListener(OnConfirmNo);

        if (peerYesButton != null) peerYesButton.onClick.RemoveListener(OnPeerYesClicked);
        if (peerNoButton != null) peerNoButton.onClick.RemoveListener(OnPeerNoClicked);
        if (loseReplayButton != null) loseReplayButton.onClick.RemoveListener(OnPauseReplayClicked);
        if (loseLevelsButton != null) loseLevelsButton.onClick.RemoveListener(OnPauseLevelsClicked);

    }

    // ============================================================
    // Help
    // ============================================================
    public void ToggleHelp()
    {
        ResolveAllRefs(force: false);

        if (instructionsPopup == null) return;

        bool nextState = !instructionsPopup.gameObject.activeSelf;
        instructionsPopup.gameObject.SetActive(nextState);
        if (!nextState) return;

        if (helpScreen1) helpScreen1.SetActive(true);
        if (helpScreen2) helpScreen2.SetActive(false);
        if (helpScreen3) helpScreen3.SetActive(false);
        if (helpScreen4) helpScreen4.SetActive(false);
    }

    // ============================================================
    // Room Code
    // ============================================================
    public void ToggleRoomCode()
    {
        ResolveAllRefs(force: false);

        if (roomCodeWindow == null)
        {
            Debug.LogError($"[CornerUIButtons] ToggleRoomCode: roomCodeWindow is NULL on {transform.name} scene={gameObject.scene.name}.");
            return;
        }

        bool nextState = !roomCodeWindow.activeSelf;
        roomCodeWindow.SetActive(nextState);

        ForceUIInteractive(roomCodeWindow);

        if (_probeCo != null) StopCoroutine(_probeCo);
        _probeCo = StartCoroutine(PostToggleProbe(roomCodeWindow, nextState));

        if (nextState)
            RefreshRoomCodeText();
    }

    private IEnumerator PostToggleProbe(GameObject go, bool intended)
    {
        yield return new WaitForEndOfFrame();
        if (go == null) yield break;
        yield return new WaitForSeconds(0.15f);
    }

    // ============================================================
    // Layout (optional - אתה לא חייב להשתמש)
    // ============================================================
    public void LayoutButtons()
    {
        if (boardRect == null || buttonsToLayout == null || buttonsToLayout.Count == 0)
            return;

        float y = paddingBottom;
        float x = paddingRight;

        for (int i = 0; i < buttonsToLayout.Count; i++)
        {
            RectTransform rt = buttonsToLayout[i];
            if (rt == null) continue;

            if (rt.parent != boardRect)
                rt.SetParent(boardRect, false);

            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);

            if (direction == LayoutDirection.VerticalUp)
            {
                rt.anchoredPosition = new Vector2(-paddingRight, y);
                y += rt.rect.height + spacing;
            }
            else
            {
                rt.anchoredPosition = new Vector2(-x, paddingBottom);
                x += rt.rect.width + spacing;
            }
        }
    }

    // ============================================================
    // Mute
    // ============================================================
    public void ToggleMute()
    {
        isMuted = !isMuted;
        ApplyMuteState();
    }

    private void ApplyMuteState()
    {
        bool shouldBeActive = !isMuted;

        for (int i = 0; i < audioObjects.Count; i++)
            if (audioObjects[i] != null)
                audioObjects[i].SetActive(shouldBeActive);

        if (muteButtonImage != null)
        {
            if (isMuted && muteSprite != null) muteButtonImage.sprite = muteSprite;
            else if (!isMuted && volumeOnSprite != null) muteButtonImage.sprite = volumeOnSprite;
        }
    }

    // ============================================================
    // Pause Flow
    // ============================================================
    private bool IsInGameScene() => gameObject.scene.name == targetScene;

    private void ApplyPauseVisibility()
    {
        bool inGame = IsInGameScene();
        if (pauseButton != null) pauseButton.gameObject.SetActive(inGame);
        if (!inGame) SafeHidePauseUI();
    }

    private void SafeHidePauseUI()
    {
        if (pauseWindow) pauseWindow.SetActive(false);
        if (confirmWindow) confirmWindow.SetActive(false);
        if (peerRequestWindow) peerRequestWindow.SetActive(false);
        if (toastText) toastText.gameObject.SetActive(false);

        _waitingPeerDecision = false;
        SetConfirmButtonsInteractable(true);

        if (_closeConfirmCo != null) { StopCoroutine(_closeConfirmCo); _closeConfirmCo = null; }
    }

    private void OpenPause()
    {
        if (IsPauseLocked()) return;

        ResolveAllRefs(force: false);

        if (!IsInGameScene()) return;
        if (!pauseWindow) return;

        bool next = !pauseWindow.activeSelf;
        pauseWindow.SetActive(next);

        if (next)
            ForceUIInteractive(pauseWindow);
    }


    private void ClosePause()
    {
        if (pauseWindow) pauseWindow.SetActive(false);
    }

    private void OnPauseReplayClicked()
    {
        if (IsPauseLocked()) return;

        AskLocalConfirm(PauseConsensus.PauseAction.ReplayLevel);
    }
    private void OnPauseLevelsClicked()
    {
        if (IsPauseLocked()) return;

        AskLocalConfirm(PauseConsensus.PauseAction.GoToLevels);
    }
    private void AskLocalConfirm(PauseConsensus.PauseAction action)
    {
        if (IsPauseLocked()) return;

        _waitingPeerDecision = false;
        if (_closeConfirmCo != null) { StopCoroutine(_closeConfirmCo); _closeConfirmCo = null; }
        if (confirmYesButton) confirmYesButton.interactable = true;
        if (confirmNoButton) confirmNoButton.interactable = true;
        if (!IsInGameScene()) return;

        // RESET confirm state בכל פתיחה
        _waitingPeerDecision = false;
        SetConfirmButtonsInteractable(true);

        if (_closeConfirmCo != null)
        {
            StopCoroutine(_closeConfirmCo);
            _closeConfirmCo = null;
        }

        _pendingLocalAction = action;

        if (confirmText)
        {
            string what = (action == PauseConsensus.PauseAction.ReplayLevel) ? "שחק מחדש" : "לחזור למסך בחירת הרמות";
            confirmText.text = $"האם אתה בטוח שאתה רוצה {what}?";
        }

        if (confirmWindow)
        {
            confirmWindow.SetActive(true);
            ForceUIInteractive(confirmWindow);
        }
        else
        {
            Debug.LogError("[PauseUI] confirmWindow is NULL");
        }
    }

    private void OnConfirmNo()
    {
        if (IsPauseLocked()) return;
        _waitingPeerDecision = false;
        SetConfirmButtonsInteractable(true);

        if (_closeConfirmCo != null)
        {
            StopCoroutine(_closeConfirmCo);
            _closeConfirmCo = null;
        }

        if (confirmWindow) confirmWindow.SetActive(false);
    }

    // ✅ שינוי: לא סוגרים - כותבים "מחכה..." ונועלים כפתורים
    private void OnConfirmYes()
    {
        if (_waitingPeerDecision) return;

        _waitingPeerDecision = true;

        if (pauseWindow) pauseWindow.SetActive(false); // ✅ נועל את הזרימה

        if (confirmText)
            confirmText.text = "מחכה לאישור מהשחקן השני...";

        if (confirmYesButton) confirmYesButton.interactable = false;
        if (confirmNoButton) confirmNoButton.interactable = false;

        if (PauseConsensus.Instance != null)
            PauseConsensus.Instance.RequestAction(_pendingLocalAction);
        else
            Debug.LogError("[PauseUI] PauseConsensus.Instance is NULL.");
    }

    public void OnLocalRequestDenied(PauseConsensus.PauseAction action)
    {
        if (confirmWindow == null) return;

        // show the message in confirm window
        confirmWindow.SetActive(true);
        ForceUIInteractive(confirmWindow);

        _waitingPeerDecision = false;

        if (confirmText)
            confirmText.text = "חברך לא מאשר";

        if (_closeConfirmCo != null) StopCoroutine(_closeConfirmCo);
        _closeConfirmCo = StartCoroutine(CloseConfirmAfterSeconds(3f));
    }

    private IEnumerator CloseConfirmAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _waitingPeerDecision = false;


        if (confirmWindow) confirmWindow.SetActive(false);

        if (confirmYesButton) confirmYesButton.interactable = true;
        if (confirmNoButton) confirmNoButton.interactable = true;

        _closeConfirmCo = null;
    }


    // נקרא ע"י PauseConsensus אצל השחקן השני
    public void ShowPeerRequest(PauseConsensus.PauseAction action)
    {
        if (IsPauseLocked()) return;

        _pendingLocalAction = action;

        if (peerRequestText)
        {
            string what = (action == PauseConsensus.PauseAction.ReplayLevel) ? "שחק מחדש" : "לחזור למסך בחירת הרמות";
            peerRequestText.text = $"חברך רוצה {what}. האם אתה מסכים?";
        }

        if (peerRequestWindow)
        {
            peerRequestWindow.SetActive(true);
            ForceUIInteractive(peerRequestWindow);
        }
        else
        {
            Debug.LogError("[PauseUI] peerRequestWindow is NULL");
        }
    }

    private void OnPeerYesClicked() => OnPeerAnswer(true);
    private void OnPeerNoClicked() => OnPeerAnswer(false);

    private void OnPeerAnswer(bool accept)
    {
        if (peerRequestWindow) peerRequestWindow.SetActive(false);

        if (PauseConsensus.Instance != null)
            PauseConsensus.Instance.RespondToPeerRequest(accept);
        else
            Debug.LogError("[PauseUI] PauseConsensus.Instance is NULL while responding.");
    }

    // ✅ שינוי: אם אנחנו מחכים ב-ConfirmWindow -> להראות שם "חברך לא מאשר" ולסגור אחרי 3 שניות
    public void ShowDeniedMessage(PauseConsensus.PauseAction action)
    {
        // אם הלקוח הזה הוא מי שלחץ "כן" ומחכה לתשובה
        if (_waitingPeerDecision && confirmWindow != null && confirmWindow.activeInHierarchy)
        {
            _waitingPeerDecision = false;

            if (confirmText)
                confirmText.text = "חברך לא מאשר";

            SetConfirmButtonsInteractable(false);
            ForceUIInteractive(confirmWindow);

            if (_closeConfirmCo != null) StopCoroutine(_closeConfirmCo);
            _closeConfirmCo = StartCoroutine(CloseConfirmAfterSeconds(3f));
            return;
        }

        // fallback: טוסט
        if (!toastText) return;

        string what = (action == PauseConsensus.PauseAction.ReplayLevel) ? "שחק מחדש" : "לחזור למסך בחירת הרמות";
        toastText.text = $"חברך לא מאשר {what}";
        toastText.gameObject.SetActive(true);

        if (_toastCo != null) StopCoroutine(_toastCo);
        _toastCo = StartCoroutine(HideToastAfterSeconds());
    }

    private IEnumerator HideToastAfterSeconds()
    {
        yield return new WaitForSeconds(toastSeconds);
        if (toastText) toastText.gameObject.SetActive(false);
        _toastCo = null;
    }

    private void SetConfirmButtonsInteractable(bool on)
    {
        if (confirmYesButton) confirmYesButton.interactable = on;
        if (confirmNoButton) confirmNoButton.interactable = on;
    }

    private bool IsPauseLocked() => _waitingPeerDecision;



    private static void ForceUIInteractive(GameObject go)
    {
        if (!go) return;

        go.transform.SetAsLastSibling();

        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            if (cg.alpha <= 0.01f) cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    // ============================================================
    // Helpers
    // ============================================================
    private static Button FindButton(Transform root, string childName)
    {
        if (root == null) return null;
        var t = root.Find(childName);
        if (t == null) t = FindDeepChild(root, childName);
        return t ? t.GetComponent<Button>() : null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null) return null;
        var t = root.Find(childName);
        if (t == null) t = FindDeepChild(root, childName);
        return t;
    }

    private static GameObject FindChildGO(Transform root, string childName)
    {
        var t = FindChild(root, childName);
        return t ? t.gameObject : null;
    }

    private static TMP_Text FindTMP(Transform root, string childName)
    {
        var t = FindChild(root, childName);
        return t ? t.GetComponent<TMP_Text>() : null;
    }

    // עוזר כי אצלך זה "Text (TMP)" ולא שם נקי
    private static TMP_Text FindTMPByContains(Transform root, string contains)
    {
        if (root == null) return null;
        foreach (var tx in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tx != null && tx.name != null && tx.name.Contains(contains))
                return tx;
        }
        return root.GetComponentInChildren<TMP_Text>(true);
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        var q = new Queue<Transform>();
        q.Enqueue(root);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur.name == childName) return cur;

            for (int i = 0; i < cur.childCount; i++)
                q.Enqueue(cur.GetChild(i));
        }
        return null;
    }

    private static string GetFullPath(Transform t)
    {
        if (!t) return "<NULL>";
        var sb = new StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }
    public void ToggleWinScreen()
    {
        ResolveAllRefs(force: false);

        if (WinWindow == null)
        {
            Debug.LogError($"[CornerUIButtons] ToggleWinScreen: winWindow is NULL. popupRoot={(_popupRoot ? GetFullPath(_popupRoot) : "NULL")}");
            return;
        }

        bool next = !WinWindow.activeSelf;
        WinWindow.SetActive(next);

        if (next)
            ForceUIInteractive(WinWindow);
        Debug.Log($"[CornerUIButtons][ToggleWinScreen] winWindow={GetFullPath(WinWindow.transform)} next={next}");
    }
    // === Win Screen API (instance) ===
    public void SetWinScreen(bool open)
    {
        ResolveAllRefs(force: false);

        if (WinWindow == null)
        {
            Debug.LogError($"[CornerUIButtons] SetWinScreen: winWindow is NULL on {GetFullPath(transform)}");
            return;
        }

        WinWindow.SetActive(open);

        if (open)
            ForceUIInteractive(WinWindow);
    }
    // === Broadcast to BOTH HUDs (Traveller + Navigator) ===
    public static void ResolveAllRefsForBothPlayers(bool force)
    {
    #if UNITY_6000_0_OR_NEWER
        var uis = FindObjectsByType<CornerUIButtons>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    #else
        var uis = Object.FindObjectsOfType<CornerUIButtons>(true);
    #endif
        foreach (var ui in uis)
            if (ui != null)
                ui.ResolveAllRefs(force);
    }

    public static void SetWinScreenForBothPlayers(bool open)
    {
    #if UNITY_6000_0_OR_NEWER
        var uis = FindObjectsByType<CornerUIButtons>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    #else
        var uis = Object.FindObjectsOfType<CornerUIButtons>(true);
    #endif
        foreach (var ui in uis)
            if (ui != null)
                ui.SetWinScreen(open);
    }
    public void SetLoseScreen(bool open)
    {
        ResolveAllRefs(force: false);

        if (loseWindow == null)
        {
            Debug.LogError($"[CornerUIButtons] SetLoseScreen: loseWindow is NULL on {transform.name}");
            return;
        }

        loseWindow.SetActive(open);

        if (open)
            ForceUIInteractive(loseWindow);
    }
    public static void SetLoseScreenForBothPlayers(bool open)
    {
    #if UNITY_6000_0_OR_NEWER
        var uis = FindObjectsByType<CornerUIButtons>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    #else
        var uis = Object.FindObjectsOfType<CornerUIButtons>(true);
    #endif

        foreach (var ui in uis)
        {
            if (ui == null) continue;
            if (!ui.gameObject.activeInHierarchy) continue;
            if (!ui.enabled) continue;

            ui.SetLoseScreen(open);
        }
    }


}