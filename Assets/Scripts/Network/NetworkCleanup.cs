using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public static class NetworkCleanup
{
    public static void Cleanup()
    {
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogWarning("[NetworkCleanup] No NetworkManager found.");
                return;
            }

            Debug.Log("[NetworkCleanup] Starting cleanup...");

            // 1) הסרת callbacks
            nm.OnClientConnectedCallback -= null;
            nm.OnClientDisconnectCallback -= null;
            nm.OnServerStarted -= null;

            // 2) Shutdown מסודר
            if (nm.IsListening)
            {
                Debug.Log("[NetworkCleanup] Calling Shutdown()");
                nm.Shutdown();
            }

            // 3) שחרור פורטים של ה־Transport
            var transport = nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport != null)
            {
                Debug.Log("[NetworkCleanup] Releasing transport socket...");
                transport.Shutdown();
            }

#if UNITY_EDITOR
            // 4) מחיקה בסצנה (Unity Editor בלבד)
            Debug.Log("[NetworkCleanup] Destroying NetworkManager object...");
            Object.DestroyImmediate(nm.gameObject);
#endif

            Debug.Log("[NetworkCleanup] Done.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[NetworkCleanup ERROR] " + ex.Message);
        }
    }
}
