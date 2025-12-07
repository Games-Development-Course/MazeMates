public class NormalDoor : IDoor
{
    private bool opened = false;
    private DoorController controller;

    public NormalDoor(DoorController controller)
    {
        this.controller = controller;
    }

    public bool IsOpen() => opened;

    public void TryOpen()
    {
        if (opened)
            return;

        opened = true;

        controller.RequestOpenDoorRpc(); // ← גרסה מתוקנת ל־RPC
    }
}
