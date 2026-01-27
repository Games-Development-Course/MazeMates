using Unity.Netcode;
using UnityEngine;

public sealed class HudBroadcastNet : NetworkBehaviour
{
    public static HudBroadcastNet Instance { get; private set; }

    [Header("Defaults")]
    [SerializeField] private float defaultDuration = 1.8f;
    [SerializeField] private Color32 defaultColor = new Color32(255, 255, 255, 255);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Call from SERVER only
    public void Broadcast(string msg) =>
        Broadcast(msg, defaultColor, defaultDuration);

    // Call from SERVER only
    public void Broadcast(string msg, Color32 color, float duration)
    {
        if (!IsServer) return;
        if (string.IsNullOrWhiteSpace(msg)) return;
        ShowForBothClientRpc(msg, color, duration);
    }

    [ClientRpc]
    private void ShowForBothClientRpc(string msg, Color32 color, float duration)
    {
        var hud = HUDManager.Instance;
        if (hud == null) return;

        hud.SetMessageAppearanceForBoth((Color)color, duration);
        hud.ShowMessageForBoth(msg);
    }
}
