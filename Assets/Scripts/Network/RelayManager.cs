using System;
using System.Threading.Tasks;
using UnityEngine;

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

using Unity.Networking.Transport.Relay;

public class RelayManager : MonoBehaviour
{
    [Header("Relay settings")]
    [Tooltip("Total players in the session (Host + Clients).")]
    [SerializeField] private int maxPlayers = 2;

    // For WebGL use secured WebSockets
    private const string ConnectionType = "wss";

    public static RelayManager Instance { get; private set; }

    private bool _servicesInitialized;
    private bool _servicesInitializing;

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
        // אפשרי להתחיל את האתחול כבר בשלב Start
        await EnsureUnityServicesInitializedAsync();
    }

    // מבטיח ש-Unity Services ואת ה-Authentication יעלו פעם אחת בלבד
    private async Task EnsureUnityServicesInitializedAsync()
    {
        if (_servicesInitialized || _servicesInitializing)
            return;

        _servicesInitializing = true;

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized &&
                UnityServices.State != ServicesInitializationState.Initializing)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[Relay] Signed in. PlayerID = {AuthenticationService.Instance.PlayerId}");
            }

            _servicesInitialized = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Failed to initialize Unity Services: {e}");
        }
        finally
        {
            _servicesInitializing = false;
        }
    }

    private UnityTransport GetUnityTransport()
    {
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null)
        {
            transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        }

        if (transport == null)
        {
            Debug.LogError("[Relay] UnityTransport not found on NetworkManager.");
        }

        return transport;
    }

    // מניח ש-maxPlayers כולל את ה-Host, אז connections = maxPlayers - 1
    private int GetMaxConnections()
    {
        return Mathf.Max(1, maxPlayers - 1);
    }

    /// <summary>
    /// יוצר allocation ב-Relay, מחזיר Join Code ומפעיל Host דרך Netcode.
    /// </summary>
    public async Task<string> StartHostWithRelayAsync()
    {
        await EnsureUnityServicesInitializedAsync();

        try
        {
            int maxConnections = GetMaxConnections();

            // יצירת allocation ל-Host
            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(maxConnections);

            // הגדרת נתוני השרת עבור UnityTransport באמצעות RelayServerData
            var relayServerData = new RelayServerData(allocation, ConnectionType);

            var transport = GetUnityTransport();
            if (transport == null)
            {
                return null; // או false בפונקציה של ה-Client
            }

            transport.SetRelayServerData(relayServerData);

            // תמיד לעבוד עם WebSockets, גם באדיטור וגם בבילד
            transport.UseWebSockets = true;

            // קבלת join code
            string joinCode =
                await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            bool started = NetworkManager.Singleton.StartHost();
            Debug.Log($"[Relay] StartHost result = {started}, joinCode = {joinCode}");

            return started ? joinCode : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Failed to start host with Relay: {e}");
            return null;
        }
    }

    /// <summary>
    /// מצטרף ל-Host לפי Join Code ומפעיל Client דרך Netcode.
    /// </summary>
    public async Task<bool> StartClientWithRelayAsync(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("[Relay] Join code is null or empty.");
            return false;
        }

        await EnsureUnityServicesInitializedAsync();

        try
        {
            // הצטרפות ל-allocation קיים לפי join code
            JoinAllocation joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            // יצירת RelayServerData מתוך ה-JoinAllocation
            var relayServerData = new RelayServerData(joinAllocation, ConnectionType);

            var transport = GetUnityTransport();
            if (transport == null)
            {
                return false;
            }

            transport.SetRelayServerData(relayServerData);
            transport.UseWebSockets = true;

            bool started = NetworkManager.Singleton.StartClient();
            Debug.Log($"[Relay] StartClient result = {started} (joinCode = {joinCode})");

            return started;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Failed to start client with Relay: {e}");
            return false;
        }
    }
}
