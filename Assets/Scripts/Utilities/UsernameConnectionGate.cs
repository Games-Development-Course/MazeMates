using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

public class UsernameConnectionGate : MonoBehaviour
{
    private readonly HashSet<string> _activeUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ulong, string> _usernameByClientId = new Dictionary<ulong, string>();

    private void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.ConnectionApprovalCallback += ApprovalCheck;
        nm.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.ConnectionApprovalCallback -= ApprovalCheck;
        nm.OnClientDisconnectCallback -= OnClientDisconnect;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string username = DecodeUsername(request.Payload);

        // ברירת מחדל: דחייה עד שמוכיחים אחרת
        response.Approved = false;
        response.CreatePlayerObject = true;
        response.Pending = false;

        if (string.IsNullOrWhiteSpace(username))
        {
            response.Reason = "INVALID_USERNAME";
            return;
        }

        username = username.Trim();

        if (_activeUsernames.Contains(username))
        {
            response.Reason = "USERNAME_IN_USE";
            return;
        }

        // מאשרים ומסמנים את השם כתפוס
        _activeUsernames.Add(username);
        _usernameByClientId[request.ClientNetworkId] = username;

        response.Approved = true;
        response.Reason = "";
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (_usernameByClientId.TryGetValue(clientId, out var u))
        {
            _usernameByClientId.Remove(clientId);
            _activeUsernames.Remove(u);
        }
    }

    private static string DecodeUsername(byte[] payload)
    {
        if (payload == null || payload.Length == 0) return null;

        try
        {
            return Encoding.UTF8.GetString(payload);
        }
        catch
        {
            return null;
        }
    }
}
