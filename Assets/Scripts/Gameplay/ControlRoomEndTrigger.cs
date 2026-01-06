// ==========================================
// File: Assets/Scripts/Gameplay/ControlRoomEndTrigger.cs
// Put this on a trigger collider in the CONTROL ROOM.
// When Traveller enters AND keys collected => set EndState=Win.
// ==========================================
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class ControlRoomEndTrigger : NetworkBehaviour
{
    private void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        if (other == null || !other.CompareTag("Player"))
            return;

        var gm = GameManager.Instance;
        if (gm == null || !gm.AllKeysCollected())
            return;

        // Ensure this is Traveller (your own helper if you have one; this is a simple check)
        // If you already have IsTravellerPlayer(NetworkObject) in PickupObject, you can reuse that pattern here.
        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null)
            return;

        // If your Traveller is host-owner, this will typically be true:
        bool isTraveller = (NetworkManager.Singleton != null && no.OwnerClientId == NetworkManager.Singleton.LocalClientId && NetworkManager.Singleton.IsHost)
                          || (gm.traveller != null && no.gameObject == gm.traveller);

        if (!isTraveller)
            return;

        var ss = SessionStateNet.Instance;
        if (ss == null || !ss.IsSpawned)
            return;

        ss.SetEndServerRpc(EndState.Win);
    }
}
