using UnityEngine;

public class DoorPlayerActivator : MonoBehaviour
{
    private DoorController door;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out DoorController d))
            door = d;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out DoorController d) && d == door)
            door = null;
    }

    private void Update()
    {
        if (door == null)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            door.Interact();
            Debug.Log("SPACE pressed, door = " + (door != null));
        }
    }
}
