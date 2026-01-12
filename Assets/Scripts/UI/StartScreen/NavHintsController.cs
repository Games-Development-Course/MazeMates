using UnityEngine;

public class NavHintsController : MonoBehaviour
{
    [SerializeField] private GameObject showHint; // זה האובייקט ShowHint ב-NavEnvironment

    private void Start()
    {
        Apply();

        // אם הערך יכול להשתנות בזמן ריצה:
        if (GameConfigNet.Instance != null)
            GameConfigNet.Instance.ShowHints.OnValueChanged += OnHintsChanged;
    }

    private void OnDestroy()
    {
        if (GameConfigNet.Instance != null)
            GameConfigNet.Instance.ShowHints.OnValueChanged -= OnHintsChanged;
    }

    private void OnHintsChanged(bool oldValue, bool newValue) => Apply();

    private void Apply()
    {
        if (!showHint) return;

        bool show = true;
        if (GameConfigNet.Instance != null)
            show = GameConfigNet.Instance.ShowHints.Value;

        showHint.SetActive(show);
    }
}
