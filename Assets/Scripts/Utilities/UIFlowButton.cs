using UnityEngine;

public class UIFlowButton : MonoBehaviour
{
    [Header("Optional targets")]
    public Transform previousObject;
    public Transform nextObject;

    [Header("Object to close (default = this)")]
    public GameObject objectToClose;

    private void Awake()
    {
        // If not set, close the object this script is on
        if (objectToClose == null)
            objectToClose = gameObject;
    }

    // ------------------------
    // Button Actions
    // ------------------------

    public void GoPrevious()
    {
        if (previousObject != null)
            previousObject.gameObject.SetActive(true);

        Close();
    }

    public void GoNext()
    {
        if (nextObject != null)
            nextObject.gameObject.SetActive(true);

        Close();
    }

    public void Close()
    {
        if (objectToClose != null)
            objectToClose.SetActive(false);
    }
}
