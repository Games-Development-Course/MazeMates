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

    [Header("Layout")]
    [SerializeField] private RectTransform boardRect;
    [SerializeField] private LayoutDirection direction = LayoutDirection.VerticalUp;
    [SerializeField] private List<RectTransform> buttonsToLayout = new List<RectTransform>();
    [SerializeField] private float paddingRight = 16f;
    [SerializeField] private float paddingBottom = 16f;
    [SerializeField] private float spacing = 10f;

    [Header("Help Button")]
    [SerializeField] private Button helpButton;
    [SerializeField] private Transform instructionsPopup;

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
    [SerializeField] private Button toggleRoomCodeButton;
    [SerializeField] private GameObject roomCodeWindow;
    [SerializeField] private TMP_Text roomCodeText;

    [Tooltip("Optional: show ___ when empty")]
    [SerializeField] private string emptyPlaceholder = "___";
    // ===================================================

    // ==================== Pause ====================
    [Header("Pause Menu (Game only)")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private GameObject pauseWindow;
    [SerializeField] private Button pauseContinueButton; // "המשך"
    [SerializeField] private Button pauseReplayButton;   // "שחק מחדש"
    [SerializeField] private Button pauseLevelsButton;   // "מסך השלבים"

    [Header("Confirm Window (Local)")]
    [SerializeField] private GameObject confirmWindow;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("Peer Request Window (Other player)")]
    [SerializeField] private GameObject peerRequestWindow;
    [SerializeField] private TMP_Text peerRequestText;
    [SerializeField] private Button peerYesButton;
    [SerializeField] private Button peerNoButton;

    [Header("Toast / Message")]
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private float toastSeconds = 2.5f;

    private PauseConsensus.PauseAction _pendingLocalAction;
    private Coroutine _toastCo;
    // ===================================================

    private bool isMuted;
    private Image muteButtonImage;

    // Debug/probe
    private Coroutine _probeCo;

    // join code binding
    private bool _boundToStore;

    private void Awake()
    {
        Debug.Log($"[CornerUIButtons][Awake] scene={gameObject.scene.name} this={transform.name} id={GetInstanceID()}");

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (muteToggleButton != null)
            muteButtonImage = muteToggleButton.GetComponent<Image>();

        WireButtons();

        isMuted = startMuted;
        ApplyMuteState();

        TryResolveRoomCodeRefs(force: true);
        if (roomCodeWindow != null) roomCodeWindow.SetActive(false);

        SafeHidePauseUI();
        ApplyPauseVisibility();

        LayoutButtons();
    }

    private void OnEnable()
    {
        Debug.Log($"[CornerUIButtons][OnEnable] scene={gameObject.scene.name} this={transform.name} id={GetInstanceID()}");

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        TryResolveRoomCodeRefs(force: false);
        BindToRoomCodeStore();

        ApplyPauseVisibility();
        LayoutButtons();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindFromRoomCodeStore();

        if (_probeCo != null)
        {
            StopCoroutine(_probeCo);
            _probeCo = null;
        }
    }

    private void OnDestroy()
    {
        UnwireButtons();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindFromRoomCodeStore();

        if (_probeCo != null)
        {
            StopCoroutine(_probeCo);
            _probeCo = null;
        }

        if (_toastCo != null)
        {
            StopCoroutine(_toastCo);
            _toastCo = null;
        }
    }

    private void OnSceneLoaded(Scene loadedScene, LoadSceneMode mode)
    {
        if (loadedScene.name != targetScene) return;

        if (gameObject.scene.name != targetScene)
        {
            Debug.LogWarning(
                $"[CornerUIButtons] Target scene '{targetScene}' loaded, disabling old instance from scene '{gameObject.scene.name}'. this={transform.name} id={GetInstanceID()}"
            );

            UnwireButtons();

            if (destroyPersistentOnTargetSceneLoad)
            {
                Destroy(gameObject);
                return;
            }

            enabled = false;
        }
    }

    // -------------------- Room Code resolution --------------------
    private void TryResolveRoomCodeRefs(bool force)
    {
        Canvas myCanvas = GetComponentInParent<Canvas>(true);
        Transform canvasRoot = myCanvas ? myCanvas.transform : transform.root;

        if (roomCodeWindow != null && myCanvas != null)
        {
            Canvas winCanvas = roomCodeWindow.GetComponentInParent<Canvas>(true);
            if (winCanvas != myCanvas)
            {
                Debug.LogWarning(
                    $"[CornerUIButtons] roomCodeWindow points to DIFFERENT Canvas! " +
                    $"myCanvas='{myCanvas.name}' winCanvas='{(winCanvas ? winCanvas.name : "NULL")}'. Clearing ref and re-finding."
                );
                roomCodeWindow = null;
                roomCodeText = null;
            }
        }

        if (force || roomCodeWindow == null)
        {
            Transform t = FindDeepChild(canvasRoot, "RoomCodeScreen");
            if (t != null) roomCodeWindow = t.gameObject;
        }

        if ((force || roomCodeText == null) && roomCodeWindow != null)
        {
            Transform t = roomCodeWindow.transform.Find("Code");
            if (t == null) t = FindDeepChild(roomCodeWindow.transform, "Code");
            if (t != null) roomCodeText = t.GetComponent<TMP_Text>();
        }

        Debug.Log(
            $"[CornerUIButtons] Refs | myCanvas={(myCanvas ? myCanvas.name : "<NULL>")} " +
            $"roomCodeWindow={(roomCodeWindow ? GetFullPath(roomCodeWindow.transform) : "<NULL>")} " +
            $"roomCodeText={(roomCodeText ? GetFullPath(roomCodeText.transform) : "<NULL>")} " +
            $"scene={gameObject.scene.name}"
        );
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

    // -------------------- RoomCode Store binding --------------------
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

    // -------------------- Buttons wiring --------------------
    private void WireButtons()
    {
        if (helpButton != null) helpButton.onClick.AddListener(ToggleHelp);
        if (muteToggleButton != null) muteToggleButton.onClick.AddListener(ToggleMute);
        if (toggleRoomCodeButton != null) toggleRoomCodeButton.onClick.AddListener(ToggleRoomCode);

        // Pause
        if (pauseButton != null) pauseButton.onClick.AddListener(OpenPause);
        if (pauseContinueButton != null) pauseContinueButton.onClick.AddListener(ClosePause);
        if (pauseReplayButton != null) pauseReplayButton.onClick.AddListener(OnPauseReplayClicked);
        if (pauseLevelsButton != null) pauseLevelsButton.onClick.AddListener(OnPauseLevelsClicked);

        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (peerYesButton != null) peerYesButton.onClick.AddListener(OnPeerYesClicked);
        if (peerNoButton != null) peerNoButton.onClick.AddListener(OnPeerNoClicked);
        Debug.Log(
    $"[PauseUI] refs: confirmWindow={(confirmWindow ? "OK" : "NULL")} " +
    $"confirmYes={(confirmYesButton ? "OK" : "NULL")} confirmNo={(confirmNoButton ? "OK" : "NULL")} " +
    $"peerWin={(peerRequestWindow ? "OK" : "NULL")} peerYes={(peerYesButton ? "OK" : "NULL")} peerNo={(peerNoButton ? "OK" : "NULL")}"
);

    }

    private void UnwireButtons()
    {
        if (helpButton != null) helpButton.onClick.RemoveListener(ToggleHelp);
        if (muteToggleButton != null) muteToggleButton.onClick.RemoveListener(ToggleMute);
        if (toggleRoomCodeButton != null) toggleRoomCodeButton.onClick.RemoveListener(ToggleRoomCode);

        // Pause
        if (pauseButton != null) pauseButton.onClick.RemoveListener(OpenPause);
        if (pauseContinueButton != null) pauseContinueButton.onClick.RemoveListener(ClosePause);
        if (pauseReplayButton != null) pauseReplayButton.onClick.RemoveListener(OnPauseReplayClicked);
        if (pauseLevelsButton != null) pauseLevelsButton.onClick.RemoveListener(OnPauseLevelsClicked);

        if (confirmYesButton != null) confirmYesButton.onClick.RemoveListener(OnConfirmYes);
        if (confirmNoButton != null) confirmNoButton.onClick.RemoveListener(OnConfirmNo);

        if (peerYesButton != null) peerYesButton.onClick.RemoveListener(OnPeerYesClicked);
        if (peerNoButton != null) peerNoButton.onClick.RemoveListener(OnPeerNoClicked);
    }

    // -------------------- Help --------------------
    public void ToggleHelp()
    {
        if (instructionsPopup == null) return;

        bool nextState = !instructionsPopup.gameObject.activeSelf;
        instructionsPopup.gameObject.SetActive(nextState);
        if (!nextState) return;

        if (helpScreen1) helpScreen1.SetActive(true);
        if (helpScreen2) helpScreen2.SetActive(false);
        if (helpScreen3) helpScreen3.SetActive(false);
        if (helpScreen4) helpScreen4.SetActive(false);
    }

    // -------------------- Room Code --------------------
    public void ToggleRoomCode()
    {
        TryResolveRoomCodeRefs(force: false);

        if (roomCodeWindow == null)
        {
            Debug.LogError($"[CornerUIButtons] ToggleRoomCode: roomCodeWindow is NULL on {transform.name} scene={gameObject.scene.name}.");
            return;
        }

        bool nextState = !roomCodeWindow.activeSelf;
        roomCodeWindow.SetActive(nextState);

        ForceVisible(roomCodeWindow);
        roomCodeWindow.transform.SetAsLastSibling();

        if (_probeCo != null) StopCoroutine(_probeCo);
        _probeCo = StartCoroutine(PostToggleProbe(roomCodeWindow, nextState));

        if (nextState)
            RefreshRoomCodeText();
    }

    private static void ForceVisible(GameObject go)
    {
        if (!go) return;

        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = true;

        var rt = go.GetComponent<RectTransform>();
        if (rt != null && rt.localScale == Vector3.zero)
            rt.localScale = Vector3.one;
    }

    private IEnumerator PostToggleProbe(GameObject go, bool intended)
    {
        yield return new WaitForEndOfFrame();
        if (go == null) yield break;

        yield return new WaitForSeconds(0.15f);
    }

    private void RefreshRoomCodeText()
    {
        if (roomCodeText == null) return;

        if (RoomCodeStore.Instance != null)
            RoomCodeStore.Instance.TryLoadFromFile();

        string code = (RoomCodeStore.Instance != null) ? RoomCodeStore.Instance.JoinCode : "";
        roomCodeText.text = string.IsNullOrWhiteSpace(code) ? emptyPlaceholder : code;
    }

    // -------------------- Layout --------------------
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

    // -------------------- Mute --------------------
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

    // ==================== Pause Flow ====================
    // ==================== Pause Flow ====================
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
    }

    private void OpenPause()
    {
        Debug.Log("[PauseUI] OpenPause()");
        if (!IsInGameScene()) return;
        if (pauseWindow) pauseWindow.SetActive(true);
    }

    private void ClosePause()
    {
        Debug.Log("[PauseUI] ClosePause()");
        if (pauseWindow) pauseWindow.SetActive(false);
    }

    private void OnPauseReplayClicked()
    {
        Debug.Log("[PauseUI] Replay clicked");
        AskLocalConfirm(PauseConsensus.PauseAction.ReplayLevel);
    }

    private void OnPauseLevelsClicked()
    {
        Debug.Log("[PauseUI] Levels clicked");
        AskLocalConfirm(PauseConsensus.PauseAction.GoToLevels);
    }

    private void AskLocalConfirm(PauseConsensus.PauseAction action)
    {
        Debug.Log($"[PauseUI] AskLocalConfirm({action})");

        if (!IsInGameScene()) return;

        _pendingLocalAction = action;

        if (confirmText)
        {
            string what = (action == PauseConsensus.PauseAction.ReplayLevel)
                ? "שחק מחדש"
                : "לחזור למסך בחירת הרמות";
            confirmText.text = $"האם אתה בטוח שאתה רוצה {what}?";
        }
        else
        {
            Debug.LogWarning("[PauseUI] confirmText is NULL");
        }

        if (confirmWindow)
        {
            confirmWindow.SetActive(true);
            ForceUIInteractive(confirmWindow);
        }
        else
        {
            Debug.LogError("[PauseUI] confirmWindow is NULL (not assigned in Inspector)");
        }
    }

    private void OnConfirmNo()
    {
        Debug.Log("[PauseUI] Confirm NO");
        if (confirmWindow) confirmWindow.SetActive(false);
    }

    private void OnConfirmYes()
    {
        Debug.Log("[PauseUI] Confirm YES");
        if (confirmWindow) confirmWindow.SetActive(false);

        if (PauseConsensus.Instance != null)
        {
            Debug.Log($"[PauseUI] Sending request: {_pendingLocalAction}");
            PauseConsensus.Instance.RequestAction(_pendingLocalAction);
        }
        else
        {
            Debug.LogError("[PauseUI] PauseConsensus.Instance is NULL. (Did you add it on a NetworkObject, e.g. GameConfigNet?)");
        }
    }

    // נקרא ע"י PauseConsensus אצל השחקן השני
    public void ShowPeerRequest(PauseConsensus.PauseAction action)
    {
        Debug.Log($"[PauseUI] ShowPeerRequest({action})");
        _pendingLocalAction = action;

        if (peerRequestText)
        {
            string what = (action == PauseConsensus.PauseAction.ReplayLevel)
                ? "שחק מחדש"
                : "לחזור למסך בחירת הרמות";
            peerRequestText.text = $"חברך רוצה {what}. האם אתה מסכים?";
        }
        else
        {
            Debug.LogWarning("[PauseUI] peerRequestText is NULL");
        }

        if (peerRequestWindow)
        {
            peerRequestWindow.SetActive(true);
            ForceUIInteractive(peerRequestWindow);
        }
        else
        {
            Debug.LogError("[PauseUI] peerRequestWindow is NULL (not assigned in Inspector)");
        }
    }

    private void OnPeerYesClicked()
    {
        Debug.Log("[PauseUI] Peer YES");
        OnPeerAnswer(true);
    }

    private void OnPeerNoClicked()
    {
        Debug.Log("[PauseUI] Peer NO");
        OnPeerAnswer(false);
    }

    private void OnPeerAnswer(bool accept)
    {
        Debug.Log($"[PauseUI] Peer answer: {accept}");
        if (peerRequestWindow) peerRequestWindow.SetActive(false);

        if (PauseConsensus.Instance != null)
            PauseConsensus.Instance.RespondToPeerRequest(accept);
        else
            Debug.LogError("[PauseUI] PauseConsensus.Instance is NULL while responding.");
    }

    public void ShowDeniedMessage(PauseConsensus.PauseAction action)
    {
        Debug.Log($"[PauseUI] Denied: {action}");

        if (!toastText) return;

        string what = (action == PauseConsensus.PauseAction.ReplayLevel)
            ? "שחק מחדש"
            : "לחזור למסך בחירת הרמות";

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

    private static void ForceUIInteractive(GameObject go)
    {
        if (!go) return;

        go.transform.SetAsLastSibling();

        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }
    // ===================================================

    // ===================================================


   

}
