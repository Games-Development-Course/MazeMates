using System.Linq;
using UnityEngine;

public class GameWorldController : MonoBehaviour
{
    public static GameWorldController Instance;

    private void Awake()
    {
        Instance = this;
    }

    private DoorController FindNearestDoorOnPad(DoorType type)
    {
        var allDoors = FindObjectsByType<DoorController>(FindObjectsSortMode.None);

        foreach (var d in allDoors)
        {
            if (d.doorType != type)
                continue;

            if (d.TravellerIsOnPad())
                return d;
        }

        return null;
    }

    public void OpenNormalDoor()
    {
        var door = FindNearestDoorOnPad(DoorType.Normal);

        if (door == null)
        {
            HUDManager.Instance.ShowMessageToNavigator("המטייל לא עומד על פד של דלת רגילה");
            return;
        }

        door.Interact();
    }

    public void OpenExitDoor()
    {
        var door = FindNearestDoorOnPad(DoorType.Exit);

        if (door == null)
        {
            HUDManager.Instance.ShowMessageToNavigator("המטייל לא עומד על פד של דלת יציאה");
            return;
        }

        if (!GameManager.Instance.AllKeysCollected())
        {
            HUDManager.Instance.ShowMessageToNavigator("המטייל לא אסף את כל המפתחות");
            return;
        }

        door.Interact();
    }

    public void ShowPuzzle()
    {
        var door = FindNearestDoorOnPad(DoorType.Puzzle);

        if (door == null)
        {
            HUDManager.Instance.ShowMessageToNavigator("המטייל לא עומד על פד של דלת חידה");
            return;
        }

        PuzzleDoor puzzle = door.GetPuzzle();

        if (puzzle == null)
        {
            HUDManager.Instance.ShowMessageToNavigator("הדלת אינה דלת חידה");
            return;
        }


        puzzle.TryOpen();   // פותח את החידה על הדלת, ומפעיל גם נווט/מטייל לפי ההגדרות החדשות

        HUDManager.Instance.ShowMessageToNavigator("החידה הוצגה למטייל");
    }

    public void UseLifebuoy()
    {
        var door = FindNearestDoorOnPad(DoorType.Puzzle);

        if (door == null)
        {
            HUDManager.Instance.ShowMessageToNavigator("המטייל לא עומד על פד של דלת חידה");
            return;
        }

        PuzzleDoor puzzle = door.GetPuzzle();

        if (puzzle == null)
        {
            HUDManager.Instance.ShowMessageToNavigator("הדלת אינה דלת חידה");
            return;
        }

        puzzle.ForceSolveAndOpen();
        HUDManager.Instance.ShowMessageToNavigator("הדלת נפתחה באמצעות גלגל הצלה!");
    }

    public void PlaceHeart()
    {
        Transform traveller = GameObject.FindWithTag("Player").transform;

        Vector3 pos = traveller.position + traveller.forward * 1f;
        Instantiate(Resources.Load<GameObject>("HeartModel"), pos, Quaternion.identity);

        HUDManager.Instance.ShowMessageToNavigator("לב הונח!");
    }

    public void RemoveBomb()
    {
        Transform traveller = GameObject.FindWithTag("Player").transform;

        var bombs = FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(x => x.name.ToLower().Contains("bomb"))
            .OrderBy(x => Vector3.Distance(traveller.position, x.position));

        var bomb = bombs.FirstOrDefault();

        if (bomb == null)
        {
            HUDManager.Instance.ShowMessageToNavigator("לא נמצאה פצצה");
            return;
        }

        Destroy(bomb.gameObject);
        HUDManager.Instance.ShowMessageToNavigator("פצצה הוסרה!");
    }
}
