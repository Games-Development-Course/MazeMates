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

        LayoutButtons();
    }

    private void OnEnable()
    {
        Debug.Log($"[CornerUIButtons][OnEnable] scene={gameObject.scene.name} this={transform.name} id={GetInstanceID()}");

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        TryResolveRoomCodeRefs(force: false);
        BindToRoomCodeStore();
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

        // אם חיווט ידני מצביע לקנבס אחר -> ננקה ונאתר מחדש
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

        // Deep Find בתוך אותו קנבס
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

        // ייתכן שה-Store נוצר רגע אחרי -> ננסה כמה פריימים
        StartCoroutine(BindStoreWhenReady());
    }

    private IEnumerator BindStoreWhenReady()
    {
        // עד 2 שניות
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

            // ✅ נסה גם טעינה מהקובץ במקרה שהקוד הגיע מתהליך אחר
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

    private void OnJoinCodeChanged(string _)
    {
        RefreshRoomCodeText();
    }

    // -------------------- Buttons wiring --------------------
    private void WireButtons()
    {
        if (helpButton != null) helpButton.onClick.AddListener(ToggleHelp);
        if (muteToggleButton != null) muteToggleButton.onClick.AddListener(ToggleMute);
        if (toggleRoomCodeButton != null) toggleRoomCodeButton.onClick.AddListener(ToggleRoomCode);
    }

    private void UnwireButtons()
    {
        if (helpButton != null) helpButton.onClick.RemoveListener(ToggleHelp);
        if (muteToggleButton != null) muteToggleButton.onClick.RemoveListener(ToggleMute);
        if (toggleRoomCodeButton != null) toggleRoomCodeButton.onClick.RemoveListener(ToggleRoomCode);
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
        Debug.Log($"[CornerUIButtons] ToggleRoomCode clicked (this={transform.name}) -> nextState={nextState} window={GetFullPath(roomCodeWindow.transform)}");

        roomCodeWindow.SetActive(nextState);

        // ✅ make sure it is visually visible (CanvasGroup can hide even when active)
        ForceVisible(roomCodeWindow);

        // ✅ bring to top of UI
        roomCodeWindow.transform.SetAsLastSibling();

        // PROBE
        if (_probeCo != null) StopCoroutine(_probeCo);
        _probeCo = StartCoroutine(PostToggleProbe(roomCodeWindow, nextState));

        Debug.Log(
            $"[CornerUIButtons] After SetActive({nextState}): " +
            $"activeSelf={roomCodeWindow.activeSelf} activeInHierarchy={roomCodeWindow.activeInHierarchy} " +
            $"parentActive={(roomCodeWindow.transform.parent ? roomCodeWindow.transform.parent.gameObject.activeInHierarchy : true)}"
        );

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
        if (rt != null)
        {
            if (rt.localScale == Vector3.zero) rt.localScale = Vector3.one;
        }
    }

    private IEnumerator PostToggleProbe(GameObject go, bool intended)
    {
        int id = go ? go.GetInstanceID() : -1;

        yield return new WaitForEndOfFrame();
        if (go == null)
        {
            Debug.LogWarning($"[RoomCode PROBE][EndOfFrame] intended={intended} go=NULL (was id={id})");
            yield break;
        }
        Debug.Log($"[RoomCode PROBE][EndOfFrame] intended={intended} go={GetFullPath(go.transform)} id={go.GetInstanceID()} activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy}");

        yield return new WaitForSeconds(0.15f);
        if (go == null)
        {
            Debug.LogWarning($"[RoomCode PROBE][+0.15s] intended={intended} go=NULL (was id={id})");
            yield break;
        }
        Debug.Log($"[RoomCode PROBE][+0.15s] intended={intended} go={GetFullPath(go.transform)} id={go.GetInstanceID()} activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy}");
    }

    private void RefreshRoomCodeText()
    {
        if (roomCodeText == null) return;

        // ✅ if store exists, try load file once (client process)
        if (RoomCodeStore.Instance != null)
            RoomCodeStore.Instance.TryLoadFromFile();

        string code = (RoomCodeStore.Instance != null) ? RoomCodeStore.Instance.JoinCode : "";
        roomCodeText.text = string.IsNullOrWhiteSpace(code) ? emptyPlaceholder : code;

        Debug.Log($"[CornerUIButtons] RefreshRoomCodeText -> '{roomCodeText.text}' (store='{code}')");
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
}
