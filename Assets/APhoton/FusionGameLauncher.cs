using Fusion;
using UnityEngine;
using System.Threading.Tasks;

public class FusionGameLauncher : MonoBehaviour
{
    public NetworkRunner runnerPrefab;

    private NetworkRunner runner;

    public async Task StartHost(string roomCode)
    {
        runner = Instantiate(runnerPrefab);

        await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = roomCode,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    public async Task StartClient(string roomCode)
    {
        runner = Instantiate(runnerPrefab);

        await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = roomCode
        });
    }
}
