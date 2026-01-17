using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerLobbyData : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> PlayerName =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> SkinIndex =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsReady =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [ServerRpc]
    public void SubmitSelectionServerRpc(FixedString32Bytes name, int skinIndex, bool ready,
        ServerRpcParams rpcParams = default)
    {
        // אופציונלי: לאשר שהשולח הוא הבעלים
        if (OwnerClientId != rpcParams.Receive.SenderClientId) return;

        PlayerName.Value = name;
        SkinIndex.Value = Mathf.Clamp(skinIndex, 0, 3);
        IsReady.Value = ready;
    }
}
