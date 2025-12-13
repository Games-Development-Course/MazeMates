using UnityEngine;
using TMPro;

public class TravellerRelayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text codeText;  // ?? ?-"Code Text" ???

    public async void OnStartHostClicked()
    {
        Debug.Log("[TravellerRelayUI] >>> OnStartHostClicked pressed");

        // ???? Host ??? Relay ?????? Join Code
        string joinCode = await RelayManager.Instance.StartHostWithRelayAsync();

        if (codeText != null)
        {
            codeText.text = "Code: " + joinCode;
        }
    }
}
