// =========================
// File: Assets/Scripts/Net/GameConfigNet.cs
// =========================
using Unity.Collections;
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

    public NetworkVariable<bool> ShowHints = new NetworkVariable<bool>(true);

    public NetworkVariable<int> HostSkin { get; } = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> ClientSkin { get; } = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Lives { get; } = new(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> Hints { get; } = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<FixedString32Bytes> HostName { get; } = new(
        new FixedString32Bytes("Host"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<FixedString32Bytes> ClientName { get; } = new(
        new FixedString32Bytes("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> BombRemovals { get; } = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 0=Easy, 1=Medium, 2=Hard
    public readonly NetworkVariable<int> Difficulty = new(0);
    public readonly NetworkVariable<int> Seed = new(0);

    public NetworkVariable<int> KeysToCollect { get; } = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> SkinSelectOpen { get; } = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ✅ NEW: tutorial mode flag (read by MazeGenerator3D)
    public NetworkVariable<bool> IsTutorial { get; } = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public global::Difficulty CurrentDifficulty
    {
        get
        {
            int idx = Mathf.Clamp(Difficulty.Value, 0, 2);
            return (global::Difficulty)(idx + 1); // 1=Easy,2=Medium,3=Hard
        }
    }

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
    public void SetShowHintsServerRpc(bool value) => ShowHints.Value = value;

    [ServerRpc(RequireOwnership = false)]
    public void SetSkinSelectOpenServerRpc(bool open) => SkinSelectOpen.Value = open;

    [ServerRpc(RequireOwnership = false)]
    public void SetDifficultyServerRpc(global::Difficulty difficulty)
    {
        int idx = Mathf.Clamp(((int)difficulty) - 1, 0, 2);
        Difficulty.Value = idx;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetTutorialModeServerRpc(bool isTutorial)
    {
        IsTutorial.Value = isTutorial;
    }

    /// <summary>
    /// Fixed tutorial config (5x5 T-shape maze, deterministic placements handled in MazeGenerator3D).
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SetTutorialConfigServerRpc()
    {
        IsTutorial.Value = true;

        // Keep it deterministic even if you later decide to use Random for tutorial placements.
        Seed.Value = 1337;

        // Allow 5x5 for tutorial.
        MazeWidth.Value = 5;
        MazeHeight.Value = 5;

        Hearts.Value = 1;
        Bombs.Value = 0;
        Keys.Value = 1;

        KeysToCollect.Value = 1;

        NormalDoors.Value = 1;
        PuzzleDoors.Value = 0;

        Difficulty.Value = 0;   // Easy
        Lives.Value = 3;
        BombRemovals.Value = 0;
        Hints.Value = 1;
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
        int seed,
        int lives,
        int bombRemovals,
        int hints
    )
    {
        // Normal game => not tutorial.
        IsTutorial.Value = false;

        // Keep your original safety min, but still allow 5 for tutorial via SetTutorialConfigServerRpc.
        MazeWidth.Value = Mathf.Max(7, mazeW);
        MazeHeight.Value = Mathf.Max(7, mazeH);

        Hearts.Value = Mathf.Max(0, hearts);
        Bombs.Value = Mathf.Max(0, bombs);
        Keys.Value = Mathf.Max(0, keys);

        KeysToCollect.Value = Mathf.Max(0, keys);

        NormalDoors.Value = Mathf.Max(0, normalDoors);
        PuzzleDoors.Value = Mathf.Max(0, puzzleDoors);

        Difficulty.Value = Mathf.Clamp(difficulty, 0, 2);
        Seed.Value = seed;

        Lives.Value = Mathf.Max(0, lives);
        BombRemovals.Value = Mathf.Max(0, bombRemovals);
        Hints.Value = Mathf.Max(0, hints);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetBombRemovalsRuntimeServerRpc(int bombRemovals)
    {
        BombRemovals.Value = Mathf.Max(0, bombRemovals);
    }
}