// ==========================================
// File: Assets/Scripts/Network/SessionStateNet.cs
// A tiny networked state holder that persists across scenes.
// Put this prefab in StartScene and register it as a NetworkPrefab.
// Spawn it once on the host (or keep it as a placed NetworkObject in StartScene).
// ==========================================
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum EndState : byte
{
    None = 0,
    Win = 1,
    Lose = 2,
}

public sealed class SessionStateNet : NetworkBehaviour
{
    public static SessionStateNet Instance { get; private set; }

    [Header("Scenes")]
    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private string gameSceneName = "GameScene";

    public NetworkVariable<EndState> End = new(
        EndState.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Ensure clean state when spawned
        if (IsServer)
            End.Value = EndState.None;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetEndServerRpc(EndState state)
    {
        if (End.Value != EndState.None)
            return;

        End.Value = state;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RestartSameGameServerRpc()
    {
        if (!IsServer)
            return;

        End.Value = EndState.None;

        // IMPORTANT: assumes GameConfigNet keeps the same seed + config values.
        // Your GameManager already reads config from GameConfigNet on Start().
        NetworkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    [ServerRpc(RequireOwnership = false)]
    public void BackToMenuServerRpc()
    {
        if (!IsServer)
            return;

        // Tell everyone (including host) to shutdown and load StartScene locally.
        ReturnToMenuClientRpc();
    }

    [ClientRpc]
    private void ReturnToMenuClientRpc()
    {
        StartCoroutine(ShutdownAndLoadMenu());
    }

    private IEnumerator ShutdownAndLoadMenu()
    {
        yield return null; // let RPC flush

        var nm = NetworkManager.Singleton;
        if (nm != null)
            nm.Shutdown();

        SceneManager.LoadScene(startSceneName, LoadSceneMode.Single);
    }
}
