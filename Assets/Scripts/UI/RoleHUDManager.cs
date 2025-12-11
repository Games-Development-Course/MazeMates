using UnityEngine;
using Unity.Netcode;

public class RoleHUDManager : MonoBehaviour
{
    [Header("Root HUD objects")]
    [SerializeField] private GameObject travellerHUD;   // TravellerHUD מההיררכיה
    [SerializeField] private GameObject navigatorHUD;   // NavigatorHUD מההיררכיה

    [Header("Environment objects")]
    [SerializeField] private GameObject travEnvironment; // TravEnvironment
    [SerializeField] private GameObject navEnvironment;  // NavEnvironment

    private void Start()
    {
        RefreshHUDAndEnvironment();
        TrySubscribeToNetworkEvents();
    }

    private void OnEnable()
    {
        TrySubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void TrySubscribeToNetworkEvents()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnServerStarted += OnNetworkStateChanged;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnServerStarted -= OnNetworkStateChanged;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnNetworkStateChanged()
    {
        RefreshHUDAndEnvironment();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.Singleton.LocalClientId)
        {
            RefreshHUDAndEnvironment();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.Singleton.LocalClientId)
        {
            RefreshHUDAndEnvironment();
        }
    }

    private void RefreshHUDAndEnvironment()
    {
        var nm = NetworkManager.Singleton;

        // אם אין NetworkManager או שעדיין לא התחברנו – מצב בחירה:
        // שני ה-HUDים פעילים, שני ה-Envs כבויים.
        if (nm == null || (!nm.IsClient && !nm.IsServer))
        {
            SetActiveSafe(travellerHUD, true);
            SetActiveSafe(navigatorHUD, true);

            SetActiveSafe(travEnvironment, false);
            SetActiveSafe(navEnvironment, false);

            return;
        }

        // Host = Traveller
        if (nm.IsHost)
        {
            SetActiveSafe(travellerHUD, true);
            SetActiveSafe(navigatorHUD, false);

            SetActiveSafe(travEnvironment, true);
            SetActiveSafe(navEnvironment, false);
        }
        // Client רגיל = Navigator
        else if (nm.IsClient)
        {
            SetActiveSafe(travellerHUD, false);
            SetActiveSafe(navigatorHUD, true);

            SetActiveSafe(travEnvironment, false);
            SetActiveSafe(navEnvironment, true);
        }
        else
        {
            // fallback במקרה קצה
            SetActiveSafe(travellerHUD, true);
            SetActiveSafe(navigatorHUD, true);

            SetActiveSafe(travEnvironment, true);
            SetActiveSafe(navEnvironment, true);
        }
    }

    private void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
        {
            go.SetActive(active);
        }
    }
}
