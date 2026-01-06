using UnityEngine;

public class GuestButtonHandler : MonoBehaviour
{
    [SerializeField] private GameObject authRoot;
    [SerializeField] private GameObject lobbyRoot;

    public void OnGuestClicked()
    {
        authRoot.SetActive(false);
        lobbyRoot.SetActive(true);
    }
}
