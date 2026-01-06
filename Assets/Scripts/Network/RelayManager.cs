using System;
using System.Threading.Tasks;
using UnityEngine;

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

using Unity.Networking.Transport.Relay;  // RelayServerData

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    [Header("Relay Settings")]
    [SerializeField] private int maxConnections = 2;

    // ל-WebGL חייבים WSS (במקום dtls / udp)
    private const string ConnectionType = "wss";

    private bool _servicesInitialized;

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

    private async void Start()
    {
        await EnsureUnityServicesInitializedAsync();
        Debug.Log("[Relay] Unity Services ready.");
    }

    private async Task EnsureUnityServicesInitializedAsync()
    {
        if (_servicesInitialized)
            return;

        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
                Debug.Log("[Relay] Unity Services initialized.");
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[Relay] Signed in as player {AuthenticationService.Instance.PlayerId}");
            }

            _servicesInitialized = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Failed to initialize Unity Services: {e}");
        }
    }

    private UnityTransport GetTransport()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[Relay] NetworkManager.Singleton not found in scene.");
            return null;
        }

        var transport = nm.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("[Relay] UnityTransport component not found on NetworkManager.");
            return null;
        }

        return transport;
    }

    // ============================
    // HOST
    // ============================
    public async Task<string> StartHostWithRelayAsync()
    {
        await EnsureUnityServicesInitializedAsync();

        var transport = GetTransport();
        if (transport == null)
            return null;

        try
        {
            // 1. יצירת Allocation ל-Host
            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(maxConnections);

            // 2. יצירת RelayServerData עם "wss" (חשוב ל-WebGL)
            RelayServerData relayServerData =
                new RelayServerData(allocation, ConnectionType);

            // 3. החלת ההגדרות על UnityTransport
            transport.SetRelayServerData(relayServerData);

            // חובה ל-WebGL
            transport.UseWebSockets = true;

            // 4. קבלת Join Code
            string joinCode =
                await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"[Relay] Host allocation created. JoinCode = {joinCode}");

            // 5. StartHost של NGO
            bool started = NetworkManager.Singleton.StartHost();
            Debug.Log($"[Relay] StartHost result = {started}");

            return started ? joinCode : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Failed to start host with Relay: {e}");
            return null;
        }
    }

    // ============================
    // CLIENT
    // ============================
    public async Task<bool> StartClientWithRelayAsync(string joinCode)
    {
        await EnsureUnityServicesInitializedAsync();

        var transport = GetTransport();
        if (transport == null)
            return false;

        try
        {
            // 1. JoinAllocation לפי Join Code
            JoinAllocation joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            // 2. RelayServerData עם "wss"
            RelayServerData relayServerData =
                new RelayServerData(joinAllocation, ConnectionType);

            // 3. החלת ההגדרות על UnityTransport
            transport.SetRelayServerData(relayServerData);

            // חובה ל-WebGL
            transport.UseWebSockets = true;

            // 4. StartClient של NGO
            bool started = NetworkManager.Singleton.StartClient();
            Debug.Log($"[Relay] StartClient result = {started}");

            return started;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Failed to start client with Relay (code={joinCode}): {e}");
            return false;
        }
    }
}
