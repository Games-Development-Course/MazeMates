// Assets/Scripts/Gameplay/Doors/DoorOpenSfx.cs
using System.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DoorOpenSfx : NetworkBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip openClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Detection")]
    [Tooltip("Degrees away from closed rotation to consider 'opening started'.")]
    [SerializeField, Range(0.01f, 10f)] private float startAngleThreshold = 0.25f;

    [Tooltip("Optional override. If empty, will auto-find a child named 'Pivot', otherwise uses this transform.")]
    [SerializeField] private Transform pivot;

    private Quaternion _closedLocalRot;
    private bool _closedCached;
    private bool _played;

    private AudioSource _source;

    public override void OnNetworkSpawn()
    {
        if (!IsClient) return; // dedicated server: no audio

        EnsureAudioSource();
        StartCoroutine(InitPivotAndCacheClosed());
    }

    private void Update()
    {
        if (!IsClient) return;
        if (_played || !_closedCached || pivot == null || openClip == null) return;

        float angle = Quaternion.Angle(pivot.localRotation, _closedLocalRot);
        if (angle >= startAngleThreshold)
        {
            _played = true;
            _source.PlayOneShot(openClip, volume);
        }
    }

    private IEnumerator InitPivotAndCacheClosed()
    {
        // DoorController may create the Pivot on spawn; wait a few frames.
        for (int i = 0; i < 60; i++)
        {
            if (pivot == null)
                pivot = transform.Find("Pivot");

            if (pivot != null)
                break;

            yield return null;
        }

        if (pivot == null)
        {
            Debug.LogWarning($"[DoorOpenSfx] Could not find Pivot under '{name}'. Assign Pivot manually.", this);
            yield break;
        }

        _closedLocalRot = pivot.localRotation;
        _closedCached = true;
    }

    private void EnsureAudioSource()
    {
        if (_source != null) return;

        _source = GetComponent<AudioSource>();
        if (_source == null) _source = gameObject.AddComponent<AudioSource>();

        _source.playOnAwake = false;
        _source.spatialBlend = 1f; // 3D sound. Set to 0 for UI-style.
        _source.rolloffMode = AudioRolloffMode.Logarithmic;
    }
}
