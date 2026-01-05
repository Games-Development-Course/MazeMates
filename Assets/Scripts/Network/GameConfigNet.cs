// Assets/Scripts/Net/GameConfigNet.cs
using Unity.Netcode;
using UnityEngine;

public sealed class GameConfigNet : NetworkBehaviour
{
    public static GameConfigNet Instance { get; private set; }

    public readonly NetworkVariable<int> MazeWidth = new(21);
    public readonly NetworkVariable<int> MazeHeight = new(21);

    public readonly NetworkVariable<int> Hearts = new(3);
    public readonly NetworkVariable<int> Bombs = new(2);
    public readonly NetworkVariable<int> Keys = new(2);

    public readonly NetworkVariable<int> NormalDoors = new(3);
    public readonly NetworkVariable<int> PuzzleDoors = new(2);

    public readonly NetworkVariable<int> Difficulty = new(0); // 0 easy, 1 medium, 2 hard
    public readonly NetworkVariable<int> Seed = new(0);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetConfigServerRpc(
        int mazeW,
        int mazeH,
        int hearts,
        int bombs,
        int keys,
        int normalDoors,
        int puzzleDoors,
        int difficulty,
        int seed
    )
    {
        MazeWidth.Value = Mathf.Max(7, mazeW);
        MazeHeight.Value = Mathf.Max(7, mazeH);

        Hearts.Value = Mathf.Max(0, hearts);
        Bombs.Value = Mathf.Max(0, bombs);
        Keys.Value = Mathf.Max(0, keys);
        KeysToCollect.Value = keys;

        NormalDoors.Value = Mathf.Max(0, normalDoors);
        PuzzleDoors.Value = Mathf.Max(0, puzzleDoors);

        Difficulty.Value = Mathf.Clamp(difficulty, 0, 2);
        Seed.Value = seed;
    }
    public NetworkVariable<int> KeysToCollect { get; } = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

}
