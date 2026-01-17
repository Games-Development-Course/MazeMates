// Assets/Scripts/Player/SkinApplierFromLobby.cs
using Unity.Netcode;
using UnityEngine;

public sealed class SkinApplier : NetworkBehaviour
{
    [SerializeField] private GameObject[] skins; // 4 skins, only 1 active

    private GameConfigNet cfg;

    public override void OnNetworkSpawn()
    {
        cfg = GameConfigNet.Instance;

        if (cfg == null)
        {
            Debug.LogWarning($"[SkinApplier] GameConfigNet.Instance is NULL on {name}");
            return;
        }

        Apply();

        // להאזין לשינויים
        cfg.HostSkin.OnValueChanged += OnSkinChanged;
        cfg.ClientSkin.OnValueChanged += OnSkinChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (cfg != null)
        {
            cfg.HostSkin.OnValueChanged -= OnSkinChanged;
            cfg.ClientSkin.OnValueChanged -= OnSkinChanged;
        }
    }

    private void OnSkinChanged(int _, int __) => Apply();

    private void Apply()
    {
        if (skins == null || skins.Length == 0)
        {
            Debug.LogWarning($"[SkinApplier] skins array not set on {name}");
            return;
        }

        if (cfg == null || !cfg.IsSpawned)
        {
            Debug.LogWarning($"[SkinApplier] cfg missing/not spawned on {name}");
            return;
        }

        bool isHostPlayer = OwnerClientId == NetworkManager.ServerClientId;
        int index = isHostPlayer ? cfg.HostSkin.Value : cfg.ClientSkin.Value;
        index = Mathf.Clamp(index, 0, skins.Length - 1);

        for (int i = 0; i < skins.Length; i++)
            if (skins[i] != null)
                skins[i].SetActive(i == index);

        Debug.Log($"[SkinApplier] obj={name} owner={OwnerClientId} hostId={NetworkManager.ServerClientId} index={index}");
    }
}
