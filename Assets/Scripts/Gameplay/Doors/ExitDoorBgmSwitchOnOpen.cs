// // Assets/Scripts/Gameplay/Doors/ExitDoorBgmSwitchOnOpen.cs
// using System.Collections;
// using Unity.Netcode;
// using UnityEngine;

// [DisallowMultipleComponent]
// public sealed class ExitDoorBgmSwitchOnOpen : NetworkBehaviour
// {
//     [Header("Door")]
//     [Tooltip("Optional. If empty, tries to find a child named 'Pivot'.")]
//     [SerializeField] private Transform pivot;

//     [Tooltip("Angle away from closed (degrees) to consider 'door opened'.")]
//     [SerializeField, Range(1f, 180f)] private float openedAngleThreshold = 30f;

//     [Tooltip("How long rotation must be stable before we consider the door 'finished opening'.")]
//     [SerializeField, Range(0.05f, 2f)] private float stableSecondsToConfirmOpen = 0.2f;

//     [Tooltip("If pivot rotates less than this per-frame (degrees), we treat it as stable.")]
//     [SerializeField, Range(0.001f, 2f)] private float stableDeltaAnglePerFrame = 0.03f;

//     [Header("Music")]
//     [Tooltip("The AudioSource that plays your background music.")]
//     [SerializeField] private AudioSource musicSource;

//     [Tooltip("The new background music to switch to after the exit door is opened.")]
//     [SerializeField] private AudioClip newBgm;

//     [Tooltip("Fade out/in duration when switching music.")]
//     [SerializeField, Range(0f, 5f)] private float fadeSeconds = 0.75f;

//     [Tooltip("Play the new BGM immediately (restart), even if it's already the current clip.")]
//     [SerializeField] private bool forceRestart = false;

//     private DoorController _door;
//     private Quaternion _closedLocalRot;
//     private Quaternion _lastLocalRot;

//     private bool _initialized;
//     private bool _openingStarted;
//     private float _stableTimer;
//     private bool _switched;

//     private void Awake()
//     {
//         _door = GetComponent<DoorController>();
//     }

//     public override void OnNetworkSpawn()
//     {
//         // Dedicated server has no audio; door opening visuals run on clients via RPC anyway.
//         if (!IsClient) return;

//         if (_door == null || _door.doorType != DoorType.Exit)
//         {
//             enabled = false;
//             return;
//         }

//         if (pivot == null)
//             pivot = transform.Find("Pivot");

//         if (pivot == null)
//         {
//             Debug.LogWarning($"[ExitDoorBgmSwitchOnOpen] Pivot not found on '{name}'. Assign it in the Inspector.", this);
//             enabled = false;
//             return;
//         }

//         if (musicSource == null)
//         {
//             // Best effort: use any AudioSource tagged "BGM" if you have one.
//             var bgmObj = GameObject.FindWithTag("BGM");
//             if (bgmObj != null) musicSource = bgmObj.GetComponent<AudioSource>();
//         }

//         if (musicSource == null)
//         {
//             Debug.LogWarning($"[ExitDoorBgmSwitchOnOpen] MusicSource not assigned on '{name}'.", this);
//             enabled = false;
//             return;
//         }

//         _closedLocalRot = pivot.localRotation;
//         _lastLocalRot = _closedLocalRot;
//         _initialized = true;
//     }

//     private void Update()
//     {
//         if (!IsClient || !_initialized || _switched) return;

//         Quaternion now = pivot.localRotation;

//         float delta = Quaternion.Angle(_lastLocalRot, now);
//         float angleFromClosed = Quaternion.Angle(_closedLocalRot, now);

//         if (!_openingStarted)
//         {
//             // Door started moving away from closed.
//             if (angleFromClosed > 0.2f)
//                 _openingStarted = true;

//             _lastLocalRot = now;
//             return;
//         }

//         if (delta <= stableDeltaAnglePerFrame)
//             _stableTimer += Time.deltaTime;
//         else
//             _stableTimer = 0f;

//         // Finished opening = (far enough from closed) AND (rotation stable for a moment)
//         if (_stableTimer >= stableSecondsToConfirmOpen && angleFromClosed >= openedAngleThreshold)
//         {
//             _switched = true;
//             StartCoroutine(SwitchMusicRoutine());
//         }

//         _lastLocalRot = now;
//     }

//     private IEnumerator SwitchMusicRoutine()
//     {
//         if (newBgm == null) yield break;

//         if (!forceRestart && musicSource.clip == newBgm && musicSource.isPlaying)
//             yield break;

//         float initialVol = musicSource.volume;

//         if (fadeSeconds > 0f && musicSource.isPlaying)
//         {
//             for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
//             {
//                 musicSource.volume = Mathf.Lerp(initialVol, 0f, t / fadeSeconds);
//                 yield return null;
//             }
//         }

//         musicSource.volume = 0f;
//         musicSource.clip = newBgm;
//         musicSource.Play();

//         if (fadeSeconds > 0f)
//         {
//             for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
//             {
//                 musicSource.volume = Mathf.Lerp(0f, initialVol, t / fadeSeconds);
//                 yield return null;
//             }
//         }

//         musicSource.volume = initialVol;
//     }
// }
