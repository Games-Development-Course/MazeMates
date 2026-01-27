using UnityEngine;

public class NavHintsController : MonoBehaviour
{
    [SerializeField] private GameObject showHint;        // כפתור/אובייקט ShowHint ב-NavEnvironment
    [SerializeField] private GameObject hintRoomSprite;  // הספרייט בחדר הנווט (או ההורה שלו)

    private void Start()
    {
        Apply();

        var cfg = GameConfigNet.Instance;
        if (cfg != null)
            cfg.ShowHints.OnValueChanged += OnHintsChanged;
    }

    private void OnDestroy()
    {
        var cfg = GameConfigNet.Instance;
        if (cfg != null)
            cfg.ShowHints.OnValueChanged -= OnHintsChanged;
    }

    private void OnHintsChanged(bool oldValue, bool newValue) => Apply();

    private void Apply()
    {
        bool show = true;
        var cfg = GameConfigNet.Instance;
        if (cfg != null)
            show = cfg.ShowHints.Value;

        if (showHint) showHint.SetActive(show);
        if (hintRoomSprite) hintRoomSprite.SetActive(show);
    }
}
