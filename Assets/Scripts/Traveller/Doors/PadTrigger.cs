using UnityEngine;

public class PadTrigger : MonoBehaviour
{
    public DoorController door;
    private bool playerOnPad = false;

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("ENTER: " + other.name);

        if (other.CompareTag("Player"))
            playerOnPad = true;
    }


    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerOnPad = false;
    }

    public bool IsPlayerOnPad()
    {
        return playerOnPad;
    }

}
