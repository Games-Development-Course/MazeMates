using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    [Header("Relay Settings")]
    [SerializeField] private int maxConnections = 2; // total players (host + clients)

    [Header("Dev")]
    [Tooltip("Disable RelayManager in Editor/Development builds so RelayAutoFlow can own networking.")]
    [SerializeField] private bool disableInDevBuilds = true;

    public int MaxConnections => maxConnections;

    private const string ConnectionType = "wss";
    private bool _servicesInitialized;
    private Task _initTask; // prevents double init/sign-in

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (disableInDevBuilds)
        {
            Debug.Log("[RelayManager] DEV build -> disabled (RelayAutoFlow should own networking).");
            enabled = false;
            return;
        }
#endif

        Debug.LogError($"[RelayManager] START name={gameObject.name} activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy} scene={gameObject.scene.name} id={GetInstanceID()}");

        await EnsureUnityServicesInitializedAsync();
        Debug.Log("[Relay] Unity Services ready.");
    }

    private Task EnsureUnityServicesInitializedAsync()
    {
        // If an init is already running or completed, reuse it.
        _initTask ??= EnsureUnityServicesInitializedInternalAsync();
        return _initTask;
    }

    private async Task EnsureUnityServicesInitializedInternalAsync()
    {
        if (_servicesInitialized) return;

        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
                Debug.Log("[Relay] Unity Services initialized.");
            }

            // No AuthenticationService.State API in some package versions.
            // Using Task caching (_initTask) prevents concurrent SignIn calls.
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[Relay] Signed in as player {AuthenticationService.Instance.PlayerId}");
            }

            _servicesInitialized = true;
        }
        catch (Exception e)
        {
            // Allow retry if it failed
            _initTask = null;
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

    public async Task<string> StartHostWithRelayAsync()
    {
        await EnsureUnityServicesInitializedAsync();

        var transport = GetTransport();
        if (transport == null) return null;

        try
        {
            // Relay CreateAllocationAsync expects number of clients (excluding host).
            int maxClients = Mathf.Max(1, maxConnections - 1);

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxClients);
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
    public async void JoinWithCode(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("[Relay] JoinWithCode called with empty joinCode.");
            return;
        }

        bool ok = await StartClientWithRelayAsync(joinCode);
        Debug.Log($"[Relay] Auto-join with code {joinCode} -> {ok}");
    }

}
