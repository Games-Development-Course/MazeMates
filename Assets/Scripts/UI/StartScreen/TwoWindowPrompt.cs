using System.Runtime.InteropServices;
using UnityEngine;

public class TwoWindowPrompt : MonoBehaviour
{
    [SerializeField] private GameObject thisPanel;         // הפאנל של השאלה
    [SerializeField] private GameObject instructionsPanel;  // הפאנל של ההוראות
    [SerializeField] private RelayUIController relayUi;     // גררי את האובייקט עם RelayUIController

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void OpenSecondWindowBlank();
    [DllImport("__Internal")] private static extern void NavigateSecondWindowWithJoinCode(string joinCode);
#endif

    public void OnNoClicked()
    {
        if (thisPanel != null) thisPanel.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(true);
    }

    public void OnYesClicked()
    {
        // 1) לפתוח חלון ריק מיד על קליק (כדי שספארי לא יחסום)
#if UNITY_WEBGL && !UNITY_EDITOR
        OpenSecondWindowBlank();
#endif

        // 2) להירשם לקבלת JoinCode כשהוא מוכן, ואז להפעיל Host
        if (relayUi != null)
        {
            relayUi.OnJoinCodeReady -= HandleJoinCodeReady;
            relayUi.OnJoinCodeReady += HandleJoinCodeReady;

            relayUi.OnHostClicked(); // זה קורא StartHostWithRelayAsync בפנים:contentReference[oaicite:2]{index=2}
        }

        // 3) להמשיך UI: לסגור שאלה ולהראות הוראות
        if (thisPanel != null) thisPanel.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(true);
    }

    private void HandleJoinCodeReady(string joinCode)
    {
        if (relayUi != null)
            relayUi.OnJoinCodeReady -= HandleJoinCodeReady;

#if UNITY_WEBGL && !UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(joinCode))
            NavigateSecondWindowWithJoinCode(joinCode.Trim());
#endif
    }
}
