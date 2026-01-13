using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    public static NetworkBootstrap Instance { get; private set; }

    private bool _handledUrl;

    private void Awake()
    {
        // אם כבר קיים Bootstrap, משמידים את הכפול כדי לא ליצור שני NetworkManager וכדומה
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // חשוב: חייב להיות Root GameObject (לא ילד של משהו אחר)
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // מומלץ: לוודא שגם ה-NetworkManager נשאר בין סצנות (אם הוא לא יושב על אותו GameObject)
        if (NetworkManager.Singleton != null)
            DontDestroyOnLoad(NetworkManager.Singleton.gameObject);

        // WebGL: אם הטאב נפתח עם joinCode ב-URL, ננסה להצטרף אוטומטית
#if UNITY_WEBGL && !UNITY_EDITOR
        TryAutoJoinFromUrl();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private void TryAutoJoinFromUrl()
    {
        if (_handledUrl) return;
        _handledUrl = true;

        string url = Application.absoluteURL;
        string joinCode = GetQueryParam(url, "joinCode");

        if (string.IsNullOrEmpty(joinCode))
            return;

        Debug.Log($"[Bootstrap] Found joinCode in URL: {joinCode}");

        if (RelayManager.Instance == null)
        {
            Debug.LogError("[Bootstrap] RelayManager.Instance is null. Make sure RelayManager exists in the first scene / bootstrap.");
            return;
        }

        // אם כבר יש חיבור (למקרה של רענון/שחזור), לא מתחברים שוב
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
        {
            Debug.Log("[Bootstrap] Network already started, skipping auto-join.");
            return;
        }

        RelayManager.Instance.JoinWithCode(joinCode);
    }

    private static string GetQueryParam(string url, string key)
    {
        if (string.IsNullOrEmpty(url)) return null;

        try
        {
            var uri = new Uri(url);
            var query = uri.Query; // מתחיל ב-?
            if (string.IsNullOrEmpty(query)) return null;

            foreach (var part in query.TrimStart('?').Split('&'))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && kv[0] == key)
                    return Uri.UnescapeDataString(kv[1]);
            }
        }
        catch { /* ignore */ }

        return null;
    }
#endif
}
