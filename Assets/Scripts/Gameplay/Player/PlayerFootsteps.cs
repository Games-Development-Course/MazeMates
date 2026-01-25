using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerFootsteps : NetworkBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip[] defaultClips;

    [Header("Step Tuning")]
    [Tooltip("Meters walked between steps.")]
    [SerializeField] private float stepDistance = 1.8f;

    [Tooltip("Multiply step distance when running (smaller = faster steps).")]
    [SerializeField] private float runningStepMultiplier = 0.7f;

    [Tooltip("Minimum speed to count as walking.")]
    [SerializeField] private float minSpeed = 0.2f;

    [Header("Pitch/Volume Randomization")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private Vector2 volumeRange = new Vector2(0.85f, 1.0f);

    private CharacterController cc;
    private Vector3 lastPos;
    private float distAccum;

    // Optional: if you have a "run" state, set this from your movement script.
    public bool IsRunning { get; set; }

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!source) source = GetComponent<AudioSource>();
    }

    public override void OnNetworkSpawn()
    {
        // Only the owning client should play local footsteps
        enabled = IsOwner;
        lastPos = transform.position;
        distAccum = 0f;
    }

    private void Update()
    {
        if (!source) return;
        if (!cc || !cc.isGrounded) { ResetAccum(); return; }

        Vector3 pos = transform.position;
        float moved = Vector3.Distance(pos, lastPos);
        lastPos = pos;

        // Estimate horizontal speed by ignoring vertical
        Vector3 vel = cc.velocity; 
        vel.y = 0f;
        float speed = vel.magnitude;

        if (speed < minSpeed) { ResetAccum(); return; }

        distAccum += moved;

        float targetStepDist = stepDistance * (IsRunning ? runningStepMultiplier : 1f);

        if (distAccum >= targetStepDist)
        {
            distAccum = 0f;
            PlayStep(defaultClips);
        }
    }

    private void ResetAccum()
    {
        distAccum = 0f;
        lastPos = transform.position;
    }

    private void PlayStep(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;

        var clip = clips[Random.Range(0, clips.Length)];
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.volume = Random.Range(volumeRange.x, volumeRange.y);
        source.PlayOneShot(clip);
    }
}