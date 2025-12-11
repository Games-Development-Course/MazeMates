using Fusion;
using Fusion.Sockets;
using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

public class PlayerSpawnManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Fallback spawn points")]
    public Transform travSpawn;
    public Transform navSpawn;

    [Header("Prefabs")]
    public NetworkPrefabRef travellerPrefab;
    public NetworkPrefabRef navigatorPrefab;

    private bool navigatorSpawned = false;
    private NetworkRunner runner;
    private int joinCount = 0;

    private void Start()
    {
        runner = FindObjectOfType<NetworkRunner>();
        if (runner != null)
            runner.AddCallbacks(this);
    }

    // =====================================================
    // PLAYER JOIN LOGIC
    // =====================================================

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        joinCount++;

        if (joinCount == 1)
        {
            SpawnTraveller(player);
        }
        else if (joinCount == 2)
        {
            SpawnNavigator(player);
            OnNavigatorSpawned();
        }
    }

    // =====================================================
    // TRAVELLER
    // =====================================================

    private void SpawnTraveller(PlayerRef player)
    {
        var gm = GameManager.Instance;

        Vector3 pos = travSpawn.position;
        Quaternion rot = travSpawn.rotation;

        var obj = runner.Spawn(
            travellerPrefab,
            pos,
            rot,
            player
        );

        gm.traveller = obj.gameObject;
        gm.travellerMove = obj.GetComponent<PlayerMovement1P>();
        gm.travellerCam = obj.GetComponentInChildren<PlayerCamera1P>();

        Freeze(obj.gameObject);

        HUDManager.Instance?.Traveller?.ShowMessage("ממתין להתחברות הנווט…");

        Debug.Log($"[Spawn] Traveller at {pos}");
    }

    // =====================================================
    // NAVIGATOR
    // =====================================================

    private void SpawnNavigator(PlayerRef player)
    {
        var gm = GameManager.Instance;

        Vector3 pos = navSpawn.position;
        Quaternion rot = navSpawn.rotation;

        var obj = runner.Spawn(
            navigatorPrefab,
            pos,
            rot,
            player
        );

        gm.navigator = obj.gameObject;
        gm.navigatorMove = obj.GetComponent<PlayerMovement1P>();
        gm.navigatorCam = obj.GetComponentInChildren<PlayerCamera1P>();

        Freeze(obj.gameObject);

        navigatorSpawned = true;

        Debug.Log($"[Spawn] Navigator at {pos}");
    }

    // =====================================================
    // FREEZE / UNFREEZE
    // =====================================================

    private void Freeze(GameObject obj)
    {
        var move = obj.GetComponent<PlayerMovement1P>();
        if (move != null) move.SetFrozen(true);

        var cam = obj.GetComponentInChildren<PlayerCamera1P>();
        if (cam != null) cam.SetCameraFrozen(true);
    }

    private void Unfreeze(GameObject obj)
    {
        var move = obj.GetComponent<PlayerMovement1P>();
        if (move != null) move.SetFrozen(false);

        var cam = obj.GetComponentInChildren<PlayerCamera1P>();
        if (cam != null) cam.SetCameraFrozen(false);
    }

    // =====================================================
    // TUTORIAL START
    // =====================================================

    private void OnNavigatorSpawned()
    {
        var gm = GameManager.Instance;

        if (!navigatorSpawned || gm.traveller == null)
            return;

        StartCoroutine(StartTutorialDelayed());
    }

    private IEnumerator StartTutorialDelayed()
    {
        yield return new WaitForSeconds(0.2f);

        HUDManager.Instance?.Traveller?.Clear();

        var t = FindFirstObjectByType<TutorialManager>();
        if (t != null)
            t.StartTutorial();
    }

    // =====================================================
    // REQUIRED EMPTY CALLBACKS (Fusion 2.x)
    // =====================================================

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr msg) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
}
