using Unity.Netcode;
using UnityEngine;

public class WinScreenTrigger : NetworkBehaviour
{
    private bool _fired;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (_fired) return;
        if (!other.CompareTag("Player")) return;

        _fired = true;

        // שולח לכולם
        ShowWinClientRpc();
    }

    [ClientRpc]
    private void ShowWinClientRpc()
    {
        // בכל קליינט נפתח את ה-UI המקומי הפעיל (Traveller או Navigator)
        foreach (var ui in FindObjectsOfType<CornerUIButtons>(true))
        {
            Debug.Log($"[WinRPC] localClient={NetworkManager.Singleton.LocalClientId} cornerUIs={FindObjectsOfType<CornerUIButtons>(true).Length}");

            // חשוב: לפתוח רק HUD פעיל אצל הקליינט הזה
            if (!ui.gameObject.activeInHierarchy) continue;
            if (!ui.enabled) continue;

            ui.SetWinScreen(true);
        }
    }
}
