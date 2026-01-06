using UnityEngine;


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
            return;

        opened = true;

        Vector3 openerPos = controller.transform.position;
        var gm = GameManager.Instance;
        if (gm != null && gm.traveller != null)
            openerPos = gm.traveller.transform.position;

        controller.RequestOpenDoorServerRpc(openerPos);
    }
}
