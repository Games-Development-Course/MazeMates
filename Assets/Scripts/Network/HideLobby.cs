//using Unity.Netcode;
//using UnityEngine;

//public class HideLobbyWhenBothConnected : NetworkBehaviour
//{
//    [SerializeField] private RelayUITest relayUI;

//    public override void OnNetworkSpawn()
//    {
//        if (IsServer && NetworkManager.Singleton != null)
//        {
//            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
//        }
//    }

//    private void OnDestroy()
//    {
//        if (IsServer && NetworkManager.Singleton != null)
//        {
//            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
//        }
//    }

//    private void OnClientConnected(ulong clientId)
//    {
//        if (!IsServer)
//            return;

//        // ברגע שיש 2 לקוחות מחוברים (Host + Navigator)
//        if (NetworkManager.Singleton.ConnectedClientsList.Count >= 2)
//        {
//            HideLobbyClientRpc();
//        }
//    }

//    [ClientRpc]
//    private void HideLobbyClientRpc()
//    {
//        if (relayUI == null)
//            relayUI = FindFirstObjectByType<RelayUITest>();

//        if (relayUI != null)
//            relayUI.HidePanel();
//    }
//}
