// File: Assets/Scripts/UI/CornerUIButtons.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CornerUIButtons : MonoBehaviour
{
    public enum LayoutDirection
    {
        VerticalUp,    // stack upward from bottom-right
        HorizontalLeft // stack leftward from bottom-right
    }

    [Header("Layout")]
    [SerializeField] private RectTransform boardRect; // the "мез" / panel you want to place buttons inside
    [SerializeField] private LayoutDirection direction = LayoutDirection.VerticalUp;

    [Tooltip("Order matters: first = closest to bottom-right, then stacked by direction")]
    [SerializeField] private List<RectTransform> buttonsToLayout = new List<RectTransform>();

    [SerializeField] private float paddingRight = 16f;
    [SerializeField] private float paddingBottom = 16f;
    [SerializeField] private float spacing = 10f;

    [Header("Help Button")]
    [SerializeField] private Button helpButton;
    [SerializeField] private Transform instructionsPopup; // will be SetActive(true)

    [Header("Mute Button (toggle)")]
    [SerializeField] private Button muteToggleButton;
    [SerializeField] private Sprite volumeOnSprite;
    [SerializeField] private Sprite muteSprite;

    [Tooltip("These GameObjects will be enabled/disabled when muting/unmuting")]
    [SerializeField] private List<GameObject> audioObjects = new List<GameObject>();

    [Header("Initial State")]
    [SerializeField] private bool startMuted = false;

    private bool isMuted;
    private Image muteButtonImage;

    private void Awake()
    {
        if (muteToggleButton != null)
            muteButtonImage = muteToggleButton.GetComponent<Image>();

        WireButtons();
        isMuted = startMuted;
        ApplyMuteState();
        LayoutButtons();
    }

    private void OnEnable()
    {
        LayoutButtons();
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    // If the board changes size (different resolutions), keep it positioned.
    private void OnRectTransformDimensionsChange()
    {
        LayoutButtons();
    }

    private void WireButtons()
    {
        if (helpButton != null)
            helpButton.onClick.AddListener(OpenHelp);

        if (muteToggleButton != null)
            muteToggleButton.onClick.AddListener(ToggleMute);
    }

    private void UnwireButtons()
    {
        if (helpButton != null)
            helpButton.onClick.RemoveListener(OpenHelp);

        if (muteToggleButton != null)
            muteToggleButton.onClick.RemoveListener(ToggleMute);
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

            // Ensure it lives under the board
            if (rt.parent != boardRect)
                rt.SetParent(boardRect, false);

            // Anchor to bottom-right of the board
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);

            // Place it
            if (direction == LayoutDirection.VerticalUp)
            {
                rt.anchoredPosition = new Vector2(-paddingRight, y);
                y += rt.rect.height + spacing;
            }
            else // HorizontalLeft
            {
                rt.anchoredPosition = new Vector2(-x, paddingBottom);
                x += rt.rect.width + spacing;
            }
        }
    }

    // -------------------- Help --------------------
    public void OpenHelp()
    {
        if (instructionsPopup != null)
            instructionsPopup.gameObject.SetActive(true);
    }

    // -------------------- Mute --------------------
    public void ToggleMute()
    {
        isMuted = !isMuted;
        ApplyMuteState();
    }

    // Exposed if you ever want separate UI hooks:
    public void MuteOn()
    {
        isMuted = true;
        ApplyMuteState();
    }

    public void MuteOff()
    {
        isMuted = false;
        ApplyMuteState();
    }

    private void ApplyMuteState()
    {
        // Enable/disable all provided audio roots
        bool shouldBeActive = !isMuted;
        for (int i = 0; i < audioObjects.Count; i++)
        {
            if (audioObjects[i] != null)
                audioObjects[i].SetActive(shouldBeActive);
        }

        // Swap sprite
        if (muteButtonImage != null)
        {
            if (isMuted && muteSprite != null) muteButtonImage.sprite = muteSprite;
            else if (!isMuted && volumeOnSprite != null) muteButtonImage.sprite = volumeOnSprite;
        }
    }
}
