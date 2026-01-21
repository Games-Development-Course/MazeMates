using TMPro;
using UnityEngine;

public class HUDShared : MonoBehaviour
{
    [Header("Texts")]
    public TMP_Text livesText;
    public TMP_Text keysText;
    public TMP_Text lifebuoysText;
    public TMP_Text giftsText;
    public TMP_Text bombRemovalText;

    [Header("Hint Row (hide when ShowHints=false)")]
    [SerializeField] private GameObject hintRow; // גרור לכאן את RowHints

    private bool _bound;

    private void OnEnable()
    {
        BindToConfigIfPossible();
    }

    private void Start()
    {
        // גיבוי: לפעמים GameConfigNet נטען אחרי שה-HUD כבר OnEnable
        BindToConfigIfPossible();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void BindToConfigIfPossible()
    {
        if (_bound) return;
        if (GameConfigNet.Instance == null) return;

        // ערך התחלתי
        ApplyShowHints(GameConfigNet.Instance.ShowHints.Value);

        // האזנה לשינויים
        GameConfigNet.Instance.ShowHints.OnValueChanged += OnShowHintsChanged;

        _bound = true;
    }

    private void Unbind()
    {
        if (!_bound) return;
        if (GameConfigNet.Instance != null)
            GameConfigNet.Instance.ShowHints.OnValueChanged -= OnShowHintsChanged;

        _bound = false;
    }

    private void OnShowHintsChanged(bool previousValue, bool newValue)
    {
        ApplyShowHints(newValue);
    }

    private void ApplyShowHints(bool showHints)
    {
        if (hintRow)
            hintRow.SetActive(showHints);
    }

    public void UpdateValues(GameManager gm)
    {
        if (livesText)
            livesText.text = "x " + gm.lives;
        if (keysText)
            keysText.text = $"{gm.keys}/{gm.totalKeysToCollect}";
        if (lifebuoysText)
            lifebuoysText.text = "x " + gm.lifebuoys;
        if (giftsText)
            giftsText.text = "x " + gm.HeartPlacements;
        if (bombRemovalText)
            bombRemovalText.text = "x " + gm.BombRemovals;
    }
}
