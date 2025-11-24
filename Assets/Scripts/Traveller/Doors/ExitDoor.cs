public class ExitDoor : IDoor
{
    private bool opened = false;
    private DoorController controller;

    public ExitDoor(DoorController controller)
    {
        this.controller = controller;
    }

    public bool IsOpen() => opened;

    public void TryOpen()
    {
        if (opened) return;

        if (GameManager.Instance.AllKeysCollected())
        {
            opened = true;
            controller.StartOpeningDoor(controller.openAngle);
        }
        else
        {
            // הודעה לשני המסכים
            HUDManager.Instance.ShowMessageForBoth("חסר לך מפתח!");
        }
    }
}
