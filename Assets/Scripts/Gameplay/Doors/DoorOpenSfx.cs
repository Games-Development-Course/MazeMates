// Assets/Scripts/Gameplay/Doors/DoorOpenSfx.cs
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DoorOpenSfx : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip openClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Detection")]
    [Tooltip("Degrees of movement in a single frame to consider 'opening started'.")]
    [SerializeField, Range(0.01f, 10f)] private float startAngleThreshold = 0.25f;

    [Tooltip("Optional override. If empty, will auto-find a child named 'Pivot'.")]
    [SerializeField] private Transform pivot;

    [Header("Optional")]
    [Tooltip("If true, can play again after the door stops moving for a bit.")]
    [SerializeField] private bool allowReplay = true;

    [Tooltip("How many still frames before re-arming replay (only if allowReplay=true).")]
    [SerializeField, Range(5, 200)] private int stillFramesToRearm = 45;

    private AudioSource _source;

    private Quaternion _lastRot;
    private bool _hasLast;

    private bool _armed = true;
    private int _stillFrames;

    private void Awake()
    {
        // Dedicated server / headless: don't do audio
        if (Application.isBatchMode)
            enabled = false;
    }

    private void OnEnable()
    {
        EnsureAudioSource();
        StartCoroutine(InitPivotIfNeeded());
        _hasLast = false;
        _armed = true;
        _stillFrames = 0;
    }

    private void Update()
    {
        if (!_armed || pivot == null || openClip == null) return;

        if (!_hasLast)
        {
            _lastRot = pivot.localRotation;
            _hasLast = true;
            return;
        }

        float delta = Quaternion.Angle(pivot.localRotation, _lastRot);
        _lastRot = pivot.localRotation;

        // "Re-arm" when door is still for a while (so it can play again on next open)
        if (allowReplay)
        {
            if (delta < 0.01f) _stillFrames++;
            else _stillFrames = 0;

            if (_stillFrames >= stillFramesToRearm)
                _armed = true;
        }

        if (delta >= startAngleThreshold)
        {
            _armed = false;              // play once per open (or until re-armed)
            _stillFrames = 0;
            _source.PlayOneShot(openClip, volume);
        }
    }

    private IEnumerator InitPivotIfNeeded()
    {
        if (pivot != null) yield break;

        // Wait a few frames in case Pivot is created at runtime
        for (int i = 0; i < 60; i++)
        {
            pivot = transform.Find("Pivot");
            if (pivot != null) yield break;
            yield return null;
        }

        Debug.LogWarning($"[DoorOpenSfx] Could not find Pivot under '{name}'. Assign Pivot manually.", this);
    }

    private void EnsureAudioSource()
    {
        if (_source != null) return;

        _source = GetComponent<AudioSource>();
        if (_source == null) _source = gameObject.AddComponent<AudioSource>();

        _source.playOnAwake = false;
        _source.spatialBlend = 1f;
        _source.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    public void TriggerOpenSfxOnce()
    {
        if (openClip == null) return;

        EnsureAudioSource();
        _source.PlayOneShot(openClip, volume);

        // אל תנגן שוב כש-Pivot יתחיל לזוז
        _armed = false;
        _stillFrames = 0;
        _hasLast = false;
    }
    public void TriggerCloseSfxOnce()
    {
    }
}