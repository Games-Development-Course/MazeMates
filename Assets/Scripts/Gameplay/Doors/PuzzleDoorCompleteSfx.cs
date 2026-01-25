// Assets/Scripts/Gameplay/Doors/PuzzleDoorCompleteSfx.cs
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PuzzleDoorCompleteSfx : NetworkBehaviour
{
    [SerializeField] private AudioClip puzzleCompleteClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private bool _played;

    /// <summary>
    /// Server-only: plays the puzzle-complete SFX on all clients once.
    /// Call this immediately before you open the door.
    /// </summary>
    public void PlayServer()
    {
        if (!IsServer || _played) return;
        _played = true;

        PlayPuzzleCompleteSfxRpc(transform.position, volume);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayPuzzleCompleteSfxRpc(Vector3 worldPos, float vol)
    {
        if (puzzleCompleteClip == null) return;
        AudioSource.PlayClipAtPoint(puzzleCompleteClip, worldPos, Mathf.Clamp01(vol));
    }
}
