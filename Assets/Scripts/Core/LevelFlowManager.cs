using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class LevelFlowManager : NetworkBehaviour
{
    public static LevelFlowManager Instance { get; private set; }

    [Header("Scene names")]
    [SerializeField] private string mainMenuSceneName = "StartScene"; // your start scene name

    private bool _ended;

    private void Awake()
    {
        Instance = this;
        Debug.Log($"[LevelFlow][Awake] name={name} IsServer={IsServer} HasNO={(GetComponent<NetworkObject>() != null)}");
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[LevelFlow][OnNetworkSpawn] IsServer={IsServer} IsSpawned={NetworkObject.IsSpawned}");
    }


    // Call these from your existing win/lose detection (SERVER SIDE)
    public void EndLevelWin_Server()
    {
        Debug.Log($"[LevelFlow] EndLevelWin_Server CALLED | IsServer={IsServer} NM.IsServer={NetworkManager.Singleton?.IsServer} Spawned={(NetworkObject != null && NetworkObject.IsSpawned)}");

        if (!IsServer)
            return;

        EndLevel_Server(LevelEndState.Win);
    }


    public void EndLevelLose_Server()
    {
        if (!IsServer) return;
        EndLevel_Server(LevelEndState.Lose);
    }

    private void EndLevel_Server(LevelEndState state)
    {
        if (_ended)
        {
            Debug.LogWarning("[LevelFlowManager] Level already ended, ignoring duplicate end call.");
            return;
        }
        _ended = true;

        ShowLevelEndPopup_ClientRpc((int)state);
        Debug.Log("[LevelFlowManager] Level end popup shown to clients.");

        // Optional: also disable gameplay input server-side via a flag you already have.
        // (Avoid Time.timeScale in multiplayer.)
    }

    [ClientRpc]
    private void ShowLevelEndPopup_ClientRpc(int state)
    {
        Debug.Log($"[LevelFlow][ClientRpc] localClient={NetworkManager.Singleton.LocalClientId} " +
                $"IsHost={NetworkManager.Singleton.IsHost} IsClient={NetworkManager.Singleton.IsClient} " +
                $"LevelEndUI.Instance={(LevelEndUI.Instance != null ? "OK" : "NULL")}");

        if (LevelEndUI.Instance != null)
            LevelEndUI.Instance.Show((LevelEndState)state);
    }


    // Called by UI (client)
    public void RequestRestart()
    {
        if (NetworkManager.Singleton == null) return;
        RequestRestart_ServerRpc();
    }

    public void RequestMainMenu()
    {
        if (NetworkManager.Singleton == null) return;
        RequestMainMenu_ServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRestart_ServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // Host-only guard (recommended)
        if (!IsRequestFromHost(rpcParams)) return;

        string current = SceneManager.GetActiveScene().name;
        NetworkManager.SceneManager.LoadScene(current, LoadSceneMode.Single);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestMainMenu_ServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // Host-only guard (recommended)
        if (!IsRequestFromHost(rpcParams)) return;

        NetworkManager.SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private bool IsRequestFromHost(ServerRpcParams rpcParams)
    {
        // If you're running Host mode, the host is the server's local client.
        if (NetworkManager.Singleton.IsHost)
            return rpcParams.Receive.SenderClientId == NetworkManager.Singleton.LocalClientId;

        // If you ever run dedicated server, you can decide policy.
        // For now: allow any client (or implement your own admin check).
        return true;
    }
}
