// Assets/Scripts/Utilities/BombTrigger.cs
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class BombTrigger : NetworkBehaviour
{
    private bool travellerInside = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Player")) return;
        if (!IsTravellerPlayerCollider(other)) return;

        travellerInside = true;
        SetBombSpotlightTargetClientRpc(true, MakeAllNonServerClientsTargetParams());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Player")) return;
        if (!IsTravellerPlayerCollider(other)) return;

        travellerInside = false;
        SetBombSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
    }

    private void OnDisable()
    {
        // ✅ אם הפצצה נעלמת בזמן שהמטייל בתוך הטריגר – נכבה זרקור
        if (!IsServer) return;
        if (!travellerInside) return;

        travellerInside = false;
        SetBombSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
    }

    private void OnDestroy()
    {
        // גיבוי נוסף (לפעמים OnDisable מספיק, אבל זה בטוח)
        if (!IsServer) return;
        if (!travellerInside) return;

        travellerInside = false;
        SetBombSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
    }

    private bool IsTravellerPlayerCollider(Collider other)
    {
        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null || !no.IsSpawned || !no.IsPlayerObject)
            return false;

        // אם אצלכם traveller יכול לא להיות host, אפשר להחליף לזיהוי כמו ב-PickupObject.
        return no.OwnerClientId == NetworkManager.ServerClientId;
    }

    private static ClientRpcParams MakeAllNonServerClientsTargetParams()
    {
        var nm = NetworkManager.Singleton;
        var ids = nm.ConnectedClientsIds;

        var list = new List<ulong>(ids.Count);
        foreach (var id in ids)
            if (id != NetworkManager.ServerClientId)
                list.Add(id);

        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = list.ToArray() }
        };
    }
    public void ForceOff_Server()
    {
        if (!IsServer) return;

        // גם אם לא קיבלנו Exit (כי despawn), נכבה את הזרקור
        SetBombSpotlightTargetClientRpc(false, MakeAllNonServerClientsTargetParams());
    }


    [ClientRpc]
    private void SetBombSpotlightTargetClientRpc(bool on, ClientRpcParams rpcParams = default)
    {
        NavigatorSpotlights.I?.SetNearBomb(on);
    }
}
