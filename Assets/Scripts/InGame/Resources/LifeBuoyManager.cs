using UnityEngine;

public class LifeBuoyManager : MonoBehaviour
{
    public static LifeBuoyManager Instance;
    public GameManager gameManager;

    [Header("UI Overlay shown during hint")]
    public GameObject overlayObj;

    [Header("How long hint stays visible (seconds)")]
    public float displayTime = 2.5f;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Activates the lifebuoy overlay, but ONLY if puzzle is currently open.
    /// </summary>
    public void TryUseLifeBuoy()
    {
        if (gameManager.lifebuoys == 0)
        {
            HUDManager.Instance.ShowMessageToNavigator("��� �� ����� ���� ������.");
            Debug.Log("Lifebuoy denied � none available.");
            return;
        }
        // ���� �� ���� ����
        if (!GameManager.Instance.inPuzzle)
        {
            Debug.Log("Lifebuoy denied � puzzle is not open.");
            return;
        }

        Debug.Log("Lifebuoy used � showing hint overlay.");
        HUDManager.Instance.ShowMessageToNavigator("���� ���� �����, ��� ����� �� ��� �� ����");
        gameManager.lifebuoys--;

        // ����� �� ����
        overlayObj.SetActive(true);

        // ���� �� ����� ����
        CancelInvoke(nameof(HideOverlay));

        // ������ ����� ���
        Invoke(nameof(HideOverlay), displayTime);
    }

    /// <summary>
    /// Hides the hint overlay.
    /// </summary>
    private void HideOverlay()
    {
        overlayObj.SetActive(false);
    }
}
