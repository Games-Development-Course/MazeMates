using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NavigatorHUD : MonoBehaviour
{
    [Header("Shared Bar (manual reference)")]
    public HUDShared sharedBar;
    public RectTransform barParent;

    [Header("Buttons to Lock Before Ready")]
    public Button[] actionButtons;

    [Header("Navigator UI")]
    [SerializeField] public TMP_Text messageText;
    [SerializeField] private GameObject messageRoot;
    [SerializeField] private Image bubbleImage;  
    [SerializeField] private bool hideOnStartButKeepActive = true;


    private Color defaultColor;

    private void Awake()
    {
        WireIfNeeded();
        if (messageText != null) defaultColor = messageText.color;
        if (hideOnStartButKeepActive) SetVisible(false);
    }

    private void WireIfNeeded()
    {
        if (messageText == null)
            messageText = GetComponentInChildren<TMP_Text>(true);

        if (messageRoot == null && messageText != null)
        {
            var img = messageText.GetComponentInParent<Image>(true);
            messageRoot = (img != null) ? img.gameObject : messageText.gameObject;
        }

        if (bubbleImage == null && messageRoot != null)
            bubbleImage = messageRoot.GetComponent<Image>();
    }

    private void SetVisible(bool visible)
    {
        WireIfNeeded();
        if (messageRoot == null) return;

        var cg = messageRoot.GetComponent<CanvasGroup>();
        if (cg == null) cg = messageRoot.AddComponent<CanvasGroup>();

        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;

        if (bubbleImage != null) bubbleImage.enabled = true;
        if (messageText != null) messageText.enabled = true;
    }

    private IEnumerator Start()
    {
        if (!sharedBar)
            sharedBar = GetComponentInChildren<HUDShared>(true);

        foreach (var b in actionButtons)
            b.interactable = false;

        yield return StartCoroutine(WaitForNavigator());

        Debug.Log("NavigatorHUD: Navigator is ready — buttons unlocked.");
    }

    private IEnumerator WaitForNavigator()
    {
        while (NavigatorActions.Instance == null)
            yield return null;

        var nav = NavigatorActions.Instance;

        while (!nav.IsSpawned)
            yield return null;

        while (!nav.IsOwner)
            yield return null;

        foreach (var b in actionButtons)
            b.interactable = true;
    }

    // ============================================
    // HUD API
    // ============================================

    public void UpdateShared(GameManager gm)
    {
        sharedBar?.UpdateValues(gm);
    }

    public void SetMessageColor(Color c)
    {
        if (messageText)
            messageText.color = c;
    }
    public void ShowMessage(string msg)
    {
        WireIfNeeded();
        if (messageText == null || messageRoot == null) return;

        messageText.text = msg ?? "";
        messageRoot.SetActive(true);
        SetVisible(true);
    }

    public void Clear()
    {
        if (messageText == null) return;

        messageText.text = string.Empty;
        messageText.color = defaultColor;

        if (hideOnStartButKeepActive) SetVisible(false);
        else if (messageRoot != null) messageRoot.SetActive(false);
    }


}
