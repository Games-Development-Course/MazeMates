// Assets/Scripts/UI/CloseWindow.cs
using UnityEngine;

public class CloseWindow : MonoBehaviour
{
    [Header("What to close (optional)")]
    [Tooltip("If empty, closes THIS gameObject.")]
    [SerializeField] private GameObject windowRoot;

    [Header("ESC behavior")]
    [SerializeField] private bool closeOnEsc = true;

    private void Awake()
    {
        if (windowRoot == null)
            windowRoot = gameObject;
    }

    private void Update()
    {
        if (!closeOnEsc) return;
        if (windowRoot == null) return;
        if (!windowRoot.activeInHierarchy) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    // Drag this into Button -> OnClick()
    public void Close()
    {
        if (windowRoot != null)
            windowRoot.SetActive(false);
    }

    // Optional if you want an "open" too
    public void Open()
    {
        if (windowRoot != null)
            windowRoot.SetActive(true);
    }

    public void Toggle()
    {
        if (windowRoot != null)
            windowRoot.SetActive(!windowRoot.activeSelf);
    }
}
