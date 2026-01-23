// Assets/Scripts/UI/PanelToggleOnP.cs
using UnityEngine;

public sealed class PanelToggleOnP : MonoBehaviour
{
    [Header("Assign the panel GameObject to show/hide")]
    [SerializeField] private GameObject panel;

    [Header("If true, panel is forced hidden on Start")]
    [SerializeField] private bool hideOnStart = true;

    private void Start()
    {
        if (panel == null)
        {
            Debug.LogError($"{nameof(PanelToggleOnP)}: Panel reference is not set.", this);
            enabled = false;
            return;
        }

        if (hideOnStart)
            panel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            // Toggle behavior:
            panel.SetActive(!panel.activeSelf);

            // If you ONLY want to show (never hide), replace with:
            // panel.SetActive(true);
        }
    }
}
