using System;
using System.Threading.Tasks;
using MazeMates.Authentication;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    [Header("Relay Settings")]
    [SerializeField] private int maxConnections = 2;

    public int MaxConnections => maxConnections;

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
    }

    private async void Start()
    {
        await EnsureUnityServicesInitializedAsync();
        Debug.Log("[Relay] Unity Services ready (no auto sign-in).");
    }

    private async Task EnsureUnityServicesInitializedAsync()
    {
        if (_servicesInitialized) return;

        try
        {
            // אם יש UgsAuthManager, נעדיף שהוא יבצע InitializeAsync כדי לשמור על אותה תצורה (Profile וכו').
            if (UgsAuthManager.Instance != null)
            {
                await UgsAuthManager.Instance.InitializeAsync();
            }
            else
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                    Debug.Log("[Relay] Unity Services initialized.");
                }
            }

            _servicesInitialized = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Failed to initialize Unity Services: {e}");
        }
    }

    private bool IsPlayerAllowedToUseRelay()
    {
        return UgsAuthManager.Instance != null && UgsAuthManager.Instance.IsSignedIn;
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

    public async Task<string> StartHostWithRelayAsync()
    {
        await EnsureUnityServicesInitializedAsync();

        if (!IsPlayerAllowedToUseRelay())
        {
            Debug.LogWarning("[Relay] Blocked: player is not signed in. Please login first.");
            return null;
        }

        var transport = GetTransport();
        if (transport == null) return null;

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            RelayServerData relayServerData = new RelayServerData(allocation, ConnectionType);

            transport.SetRelayServerData(relayServerData);
            transport.UseWebSockets = true;

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"[Relay] Host allocation created. JoinCode = {joinCode}");

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

    public async Task<bool> StartClientWithRelayAsync(string joinCode)
    {
        await EnsureUnityServicesInitializedAsync();

        if (!IsPlayerAllowedToUseRelay())
        {
            Debug.LogWarning("[Relay] Blocked: player is not signed in. Please login first.");
            return false;
        }

        var transport = GetTransport();
        if (transport == null) return false;

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = new RelayServerData(joinAllocation, ConnectionType);

            transport.SetRelayServerData(relayServerData);
            transport.UseWebSockets = true;

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
