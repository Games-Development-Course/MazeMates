using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class PlayerInfoHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text infoText;

    private const char LTR = '\u202A';
    private const char POP = '\u202C';

    private void Start()
    {
        Refresh();
        Bind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Bind()
    {
        var cfg = GameConfigNet.Instance;
        if (cfg == null || !cfg.IsSpawned) return;

        cfg.HostName.OnValueChanged += OnChanged;
        cfg.ClientName.OnValueChanged += OnChanged;
    }

    private void Unbind()
    {
        var cfg = GameConfigNet.Instance;
        if (cfg == null || !cfg.IsSpawned) return;

        cfg.HostName.OnValueChanged -= OnChanged;
        cfg.ClientName.OnValueChanged -= OnChanged;
    }

    private void OnChanged(FixedString32Bytes _, FixedString32Bytes __) => Refresh();

    private void Refresh()
    {
        var nm = NetworkManager.Singleton;
        var cfg = GameConfigNet.Instance;
        if (nm == null || cfg == null || infoText == null) return;

        // ✅ אצלך: Host=מטייל, Client=נווט
        bool iAmHost = nm.IsServer;

        string rawName = iAmHost
            ? cfg.HostName.Value.ToString()
            : cfg.ClientName.Value.ToString();

        if (string.IsNullOrWhiteSpace(rawName))
            rawName = "שחקן";

        string role = iAmHost ? "מטייל" : "נווט";

        string nameForUI = ContainsHebrew(rawName)
            ? rawName
            : $"{LTR}{rawName}{POP}";

        infoText.text =
            $"שחקן: {nameForUI}\n" +
            $"תפקיד: {role}";
    }

    private static bool ContainsHebrew(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '\u0590' && c <= '\u05FF')
                return true;
        }
        return false;
    }
}
