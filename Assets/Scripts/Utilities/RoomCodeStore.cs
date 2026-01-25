using UnityEngine;

public sealed class RoomCodeStore : MonoBehaviour
{
    public static RoomCodeStore Instance { get; private set; }

    public string JoinCode { get; private set; } = "";
    public event System.Action<string> OnJoinCodeChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetJoinCode(string code)
    {
        code = code?.Trim() ?? "";
        if (JoinCode == code) return;

        JoinCode = code;
        OnJoinCodeChanged?.Invoke(JoinCode);
    }

    public void Clear()
    {
        SetJoinCode("");
    }
}
