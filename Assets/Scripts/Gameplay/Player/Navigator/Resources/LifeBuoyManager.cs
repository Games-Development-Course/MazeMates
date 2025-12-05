using UnityEngine;

public class LifeBuoyManager : MonoBehaviour
{
    public static LifeBuoyManager Instance;
    public GameManager gameManager;

    [Header("How long hint stays visible (seconds)")]
    public float displayTime = 2.5f;

    private void Awake()
    {
        Instance = this;
    }

    public void TryUseLifeBuoy()
    {
        if (gameManager.lifebuoys == 0)
        {
            HUDManager.Instance.ShowMessageToNavigator("אין לך גלגלי הצלה נוספים.");
            return;
        }

        if (!GameManager.Instance.inPuzzle)
        {
            Debug.Log("Cannot use lifebuoy — puzzle not active.");
            return;
        }

        Debug.Log("Lifebuoy used — choosing random hint");
        gameManager.lifebuoys--;

        DoorController activeDoor = GameManager.Instance.activePuzzleDoor;
        if (
            activeDoor == null
            || activeDoor.spawnedHints == null
            || activeDoor.spawnedHints.Count == 0
        )
        {
            Debug.LogError("No hints found on active puzzle door!");
            return;
        }

        // רמז רנדומלי
        int index = Random.Range(0, activeDoor.spawnedHints.Count);
        GameObject chosenHint = activeDoor.spawnedHints[index];

        HUDManager.Instance.ShowMessageToNavigator("רמז הופעל!");

        chosenHint.SetActive(true);

        // כיבוי ישן
        CancelInvoke(nameof(HideAllHints));

        // הדלקה ל-2.5 שניות
        Invoke(nameof(HideAllHints), displayTime);
    }

    private void HideAllHints()
    {
        DoorController activeDoor = GameManager.Instance.activePuzzleDoor;
        if (activeDoor == null || activeDoor.spawnedHints == null)
            return;

        foreach (var h in activeDoor.spawnedHints)
            h.SetActive(false);
    }
}
