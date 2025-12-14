using Unity.Netcode;
using UnityEngine;

public class RoleHUDManager : MonoBehaviour
{
    [Header("Root HUD objects")]
    [SerializeField]
    private GameObject travellerHUD; // TravellerHUD מההיררכיה

    [SerializeField]
    private GameObject navigatorHUD; // NavigatorHUD מההיררכיה

    private void Start()
    {
        RefreshHUD();
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
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnServerStarted += OnNetworkStateChanged;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnServerStarted -= OnNetworkStateChanged;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnNetworkStateChanged()
    {
        RefreshHUD();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            RefreshHUD();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            RefreshHUD();
        }
    }

    private void RefreshHUD()
    {
        var nm = NetworkManager.Singleton;

        // אם אין NetworkManager או שעדיין לא התחברנו – מצב בחירה:
        // שני ה-HUDים פעילים.
        if (nm == null || (!nm.IsClient && !nm.IsServer))
        {
            SetActiveSafe(travellerHUD, true);
            SetActiveSafe(navigatorHUD, true);
            return;
        }

        // Host = Traveller
        if (nm.IsHost)
        {
            SetActiveSafe(travellerHUD, true);
            SetActiveSafe(navigatorHUD, false);
        }
        // Client רגיל = Navigator
        else if (nm.IsClient)
        {
            SetActiveSafe(travellerHUD, false);
            SetActiveSafe(navigatorHUD, true);
        }
        else
        {
            // fallback במקרה קצה – להדליק את שניהם
            SetActiveSafe(travellerHUD, true);
            SetActiveSafe(navigatorHUD, true);
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
