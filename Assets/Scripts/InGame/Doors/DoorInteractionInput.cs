using UnityEngine;

public class DoorInteractionInput : MonoBehaviour
{
    private DoorController currentDoor;

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out DoorController door))
            currentDoor = door;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out DoorController door) && door == currentDoor)
            currentDoor = null;
    }

    void Update()
    {
        if (currentDoor == null)
            return;

        // לחץ רווח → בקשה לפתוח דלת
        if (Input.GetKeyDown(KeyCode.Space))
        {
            currentDoor.Interact();
        }
    }
}
