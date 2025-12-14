using UnityEngine;

public class MinimapSecurityCamera : MonoBehaviour
{
    [Header("References")]
    public Transform player; // Traveller
    public Transform mapCenter; // Empty in the middle of the maze

    // Internal defaults
    private float followStrength = 0.25f; // how much the camera shifts toward the player
    private float swayAmplitude = 1.6f; // subtle live wobble
    private float swaySpeed = 0.35f; // speed of wobble
    private float smooth = 3f; // smoothing for movement

    private Vector3 baseOffset; // initial offset from center
    private Quaternion initialRotation; // whatever you set in the Inspector

    void Start()
    {
        if (!mapCenter)
        {
            Debug.LogError("MinimapSecurityCamera: assign mapCenter in the Inspector!");
            enabled = false;
            return;
        }

        if (!player)
        {
            Debug.LogError("MinimapSecurityCamera: assign player in the Inspector!");
            enabled = false;
            return;
        }

        // Remember how the camera is placed and rotated in the editor
        baseOffset = transform.position - mapCenter.position;
        initialRotation = transform.rotation;
    }

    void LateUpdate()
    {
        if (!player || !mapCenter)
            return;

        Vector3 center = mapCenter.position;

        // small shift toward the player (XZ only)
        Vector3 toPlayer = player.position - center;
        toPlayer.y = 0f;
        Vector3 followOffset = toPlayer * followStrength;

        // subtle live sway
        float t = Time.time * swaySpeed;
        Vector3 sway = new Vector3(
            Mathf.Sin(t) * swayAmplitude,
            0f,
            Mathf.Cos(t) * swayAmplitude * 0.7f
        );

        // final desired position
        Vector3 desiredPos = center + baseOffset + followOffset + sway;

        // smooth move
        transform.position = Vector3.Lerp(transform.position, desiredPos, smooth * Time.deltaTime);

        // lock rotation exactly to what you set in the editor (e.g. X = 90)
        transform.rotation = initialRotation;
    }
}
