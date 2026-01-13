using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Threading.Tasks;

public class GuestButtonHandler : MonoBehaviour
{
    [SerializeField] private GameObject authRoot;
    [SerializeField] private GameObject lobbyRoot;

    public async void OnGuestClicked()
    {
        await SignInAsGuest();
    }

    private async Task SignInAsGuest()
    {
        await UnityServices.InitializeAsync();

        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Guest signed in successfully. PlayerID: {AuthenticationService.Instance.PlayerId}");

            authRoot.SetActive(false);
            lobbyRoot.SetActive(true);
        }
        catch (AuthenticationException ex)
        {
            Debug.LogError($"Authentication failed: {ex.Message}");
        }
        catch (RequestFailedException ex)
        {
            Debug.LogError($"Request failed: {ex.Message}");
        }
    }
}
