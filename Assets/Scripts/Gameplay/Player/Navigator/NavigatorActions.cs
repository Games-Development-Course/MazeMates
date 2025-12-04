using UnityEngine;

public class NavigatorActions : MonoBehaviour
{
    private GameWorldController world;

    private void Start()
    {
        world = GameWorldController.Instance;
    }

    public void OpenDoor()
    {
        var gm = GameManager.Instance;

        // אם המטייל עומד על פד של דלת יציאה → נפתח דלת יציאה
        var exitDoor = GameWorldController.Instance.FindNearestDoorOnPad(DoorType.Exit);
        if (exitDoor != null)
        {
            world.OpenExitDoor();
            return;
        }

        // אחרת, אם הוא עומד על דלת רגילה → נפתח דלת רגילה
        var normalDoor = GameWorldController.Instance.FindNearestDoorOnPad(DoorType.Normal);
        if (normalDoor != null)
        {
            world.OpenNormalDoor();
            return;
        }

        // אם לא על שום דלת
        HUDManager.Instance.ShowMessageToNavigator("המטייל לא עומד על דלת");
    }

    public void ShowPuzzle() => world.ShowPuzzle();

    public void RemoveBomb() => world.RemoveBomb();

    public void PlaceHeart() => world.PlaceHeart();

    public void UseLifebuoy() => world.UseLifebuoy();
}
