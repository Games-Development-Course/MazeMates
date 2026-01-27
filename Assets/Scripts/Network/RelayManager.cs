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
    private Task _initTask;

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

        Debug.Log($"[RelayManager] START scene={gameObject.scene.name} id={GetInstanceID()}");

        // ✅ Initialize ONLY (no sign-in)
        await EnsureUnityServicesInitializedAsync();
        Debug.Log("[Relay] Unity Services ready (no auto sign-in).");
    }

    private Task EnsureUnityServicesInitializedAsync()
    {
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

            // ✅ IMPORTANT: No SignInAnonymously here.
            _servicesInitialized = true;
        }
        catch (Exception e)
        {
            _initTask = null; // allow retry
            Debug.LogError($"[Relay] Failed to initialize Unity Services: {e}");
        }
    }

    private static bool IsSignedInToUGS()
    {
        return AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;
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

        // ✅ Must be signed-in by Login/Register/Guest button
        if (!IsSignedInToUGS())
        {
            Debug.LogError("[Relay] Cannot Host: not signed in. Please login/register/guest first.");
            return null;
        }

        var transport = GetTransport();
        if (transport == null) return null;

        try
        {
            int maxClients = Mathf.Max(1, maxConnections - 1);

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxClients);
            var relayServerData = new RelayServerData(allocation, ConnectionType);

            transport.SetRelayServerData(relayServerData);
            transport.UseWebSockets = true;

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"[Relay] Host allocation created. JoinCode={joinCode}");

            bool started = NetworkManager.Singleton.StartHost();
            Debug.Log($"[Relay] StartHost result={started}");

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

        // ✅ Must be signed-in by Login/Register/Guest button
        if (!IsSignedInToUGS())
        {
            Debug.LogError("[Relay] Cannot Join: not signed in. Please login/register/guest first.");
            return false;
        }

        var transport = GetTransport();
        if (transport == null) return false;

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            var relayServerData = new RelayServerData(joinAllocation, ConnectionType);

            transport.SetRelayServerData(relayServerData);
            transport.UseWebSockets = true;

            bool started = NetworkManager.Singleton.StartClient();
            Debug.Log($"[Relay] StartClient result={started}");

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

        bool ok = await StartClientWithRelayAsync(joinCode.Trim().ToUpperInvariant());
        Debug.Log($"[Relay] Auto-join with code {joinCode} -> {ok}");
    }
}
