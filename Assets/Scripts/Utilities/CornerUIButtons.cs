// File: Assets/Scripts/Utilities/CornerUIButtons.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CornerUIButtons : MonoBehaviour
{
    public enum LayoutDirection { VerticalUp, HorizontalLeft }

    [Header("Scene Rules")]
    [Tooltip("When this scene loads, any CornerUIButtons instance NOT belonging to it will disable itself.")]
    [SerializeField] private string targetScene = "GameScene";

    [Tooltip("If true and this object survived via DontDestroyOnLoad, destroy it when GameScene loads.")]
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

    private void Awake()
    {
        Debug.Log($"[CornerUIButtons][Awake] scene={gameObject.scene.name} this={transform.name} id={GetInstanceID()}");

        // חשוב: נרשמים כאן כדי לתפוס את המעבר מ-StartScene ל-GameScene
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (muteToggleButton != null)
            muteButtonImage = muteToggleButton.GetComponent<Image>();

        WireButtons();

        isMuted = startMuted;
        ApplyMuteState();

        TryResolveRoomCodeRefs();
        if (roomCodeWindow != null) roomCodeWindow.SetActive(false);

        LayoutButtons();
    }

    private void OnEnable()
    {
        Debug.Log($"[CornerUIButtons][OnEnable] scene={gameObject.scene.name} this={transform.name} id={GetInstanceID()}");

        // להבטיח שלא נרשם פעמיים
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        TryResolveRoomCodeRefs();
        LayoutButtons();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        UnwireButtons();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene loadedScene, LoadSceneMode mode)
    {
        // ברגע ש-GameScene נטענת:
        if (loadedScene.name != targetScene) return;

        // אם האינסטנס הנוכחי *לא* יושב בתוך GameScene => זה ה-UI מה-StartScene (או DontDestroy) שמפריע.
        if (gameObject.scene.name != targetScene)
        {
            Debug.LogWarning(
                $"[CornerUIButtons] Target scene '{targetScene}' loaded, disabling old instance from scene '{gameObject.scene.name}'. this={transform.name} id={GetInstanceID()}"
            );

            // קודם ננתק listeners כדי שלא ישארו “קליקים” תלויים
            UnwireButtons();

            // אם הוא הגיע דרך DontDestroyOnLoad ורוצים למחוק לגמרי:
            if (destroyPersistentOnTargetSceneLoad)
            {
                Destroy(gameObject);
                return;
            }

            // אחרת רק לכבות את הקומפוננטה
            enabled = false;
        }
    }

    private void TryResolveRoomCodeRefs()
    {
        // אם שכחת לשייך, ננסה למצוא אוטומטית בתוך ההיררכיה המקומית
        if (roomCodeWindow == null)
        {
            var t = transform.Find("RoomCodeScreen");
            if (t != null) roomCodeWindow = t.gameObject;
        }

        if (roomCodeText == null && roomCodeWindow != null)
        {
            var t = roomCodeWindow.transform.Find("Code");
            if (t != null) roomCodeText = t.GetComponent<TMP_Text>();
        }

        Debug.Log($"[CornerUIButtons] Refs | roomCodeWindow={(roomCodeWindow ? roomCodeWindow.name : "<NULL>")} roomCodeText={(roomCodeText ? roomCodeText.name : "<NULL>")} scene={gameObject.scene.name}");
    }

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
        TryResolveRoomCodeRefs();

        if (roomCodeWindow == null)
        {
            Debug.LogError($"[CornerUIButtons] ToggleRoomCode: roomCodeWindow is NULL on {transform.name} scene={gameObject.scene.name}.");
            return;
        }

        bool nextState = !roomCodeWindow.activeSelf;
        Debug.Log($"[CornerUIButtons] ToggleRoomCode clicked (this={transform.name}) -> nextState={nextState} window={roomCodeWindow.name}");

        roomCodeWindow.SetActive(nextState);

        // לוג כדי לזהות אם משהו מכבה אותו מיד אחרי
        Debug.Log($"[CornerUIButtons] After SetActive({nextState}): activeSelf={roomCodeWindow.activeSelf} activeInHierarchy={roomCodeWindow.activeInHierarchy}");

        if (nextState)
            RefreshRoomCodeText();
    }

    private void RefreshRoomCodeText()
    {
        if (roomCodeText == null) return;

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
