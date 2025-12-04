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
        if (opened)
            return;

        if (!GameManager.Instance.AllKeysCollected())
        {
            return;
        }

        opened = true;

        controller.GetComponentInChildren<DoorDissolver>()?.TriggerDissolve();
    }
}
