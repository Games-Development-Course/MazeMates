using UnityEngine;

public class TravellerDoorActivator : MonoBehaviour
{
    private DoorController currentDoor;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out DoorController door))
        {
            currentDoor = door;
            Debug.Log("Traveller ENTER door " + door.name);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out DoorController door) && door == currentDoor)
        {
            Debug.Log("Traveller EXIT door " + door.name);
            currentDoor = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Traveller pressed SPACE");

            if (currentDoor == null)
            {
                Debug.Log("Traveller has NO DOOR to interact with");
                return;
            }

            currentDoor.Interact();
        }
    }
}
