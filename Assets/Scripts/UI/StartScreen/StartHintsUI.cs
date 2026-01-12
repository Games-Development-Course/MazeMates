using UnityEngine;
using UnityEngine.UI;

public class StartHintsUI : MonoBehaviour
{
    [SerializeField] private Toggle hintsToggle; // true = show hints

    private void Start()
    {
        if (!hintsToggle) return;

        // ברירת מחדל
        bool defaultValue = true;

        // אם כבר יש GameConfigNet
        if (GameConfigNet.Instance != null)
            defaultValue = GameConfigNet.Instance.ShowHints.Value;

        hintsToggle.isOn = defaultValue;
        hintsToggle.onValueChanged.AddListener(OnHintsChanged);
    }

    private void OnHintsChanged(bool showHints)
    {
        if (GameConfigNet.Instance == null) return;

        // אם זה רץ על Host/Server – אפשר ישירות
        // אם זה Client – עדיף ServerRpc (ראה סעיף 2)
        GameConfigNet.Instance.SetShowHintsServerRpc(showHints);
    }
}
