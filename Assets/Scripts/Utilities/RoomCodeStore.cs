using System;
using System.IO;
using UnityEngine;

public sealed class RoomCodeStore : MonoBehaviour
{
    public static RoomCodeStore Instance { get; private set; }

    public string JoinCode { get; private set; } = "";
    public event Action<string> OnJoinCodeChanged;

    // Shared Join Code file (same as RelayAutoFlow)
    private static string SharedDir => Path.Combine(Path.GetTempPath(), "MazeMates");
    private static string JoinCodeFile => Path.Combine(SharedDir, "relay_joincode.txt");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ✅ Client may start without having the code in memory yet -> load from file.
        TryLoadFromFile();
    }

    public void SetJoinCode(string code)
    {
        code = (code ?? "").Trim().ToUpperInvariant();

        if (JoinCode == code) return;

        JoinCode = code;
        TrySaveToFile(code);
        OnJoinCodeChanged?.Invoke(JoinCode);
    }

    public void Clear()
    {
        SetJoinCode("");
    }

    public bool TryLoadFromFile()
    {
        try
        {
            if (!File.Exists(JoinCodeFile)) return false;

            string text = File.ReadAllText(JoinCodeFile);
            if (string.IsNullOrWhiteSpace(text)) return false;

            int sep = text.IndexOf('|');
            if (sep <= 0 || sep >= text.Length - 1) return false;

            string code = text.Substring(sep + 1).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) return false;

            if (JoinCode != code)
            {
                JoinCode = code;
                OnJoinCodeChanged?.Invoke(JoinCode);
            }

            return true;
        }
        catch { return false; }
    }

    private void TrySaveToFile(string code)
    {
        try
        {
            Directory.CreateDirectory(SharedDir);
            long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            File.WriteAllText(JoinCodeFile, $"{unix}|{code}");
        }
        catch { }
    }
}
