using UnityEngine;
using UnityEngine.UI;

public class StartHintsUI : MonoBehaviour
{
    [SerializeField] private Toggle hintsToggle; // true = show hints

    private void Start()
    {
        if (!hintsToggle) return;

        bool defaultValue = true;

        if (GameConfigNet.Instance != null)
            defaultValue = GameConfigNet.Instance.ShowHints.Value;

        hintsToggle.SetIsOnWithoutNotify(defaultValue);
        hintsToggle.onValueChanged.AddListener(OnHintsChanged);
    }

    private void OnHintsChanged(bool showHints)
    {
        if (GameConfigNet.Instance == null) return;

        GameConfigNet.Instance.SetShowHintsServerRpc(showHints);
        // לא צריך פה להתעסק עם HUD בכלל — הוא יתעדכן לבד דרך OnValueChanged
    }
}
