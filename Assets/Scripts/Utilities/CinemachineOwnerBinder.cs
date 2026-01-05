// Assets/Scripts/Utilities/CinemachineOwnerBinder.cs
using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

public class CinemachineOwnerBinder : NetworkBehaviour
{
    [Header("Assign from Player prefab")]
    [SerializeField] private Transform trackingTarget;

    [Tooltip("Optional explicit reference. If empty, will auto-find in scene.")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        if (cinemachineCamera == null)
            cinemachineCamera = Object.FindFirstObjectByType<CinemachineCamera>();

        if (cinemachineCamera == null)
        {
            Debug.LogWarning("[CinemachineOwnerBinder] No CinemachineCamera found in scene.");
            return;
        }

        var t = trackingTarget != null ? trackingTarget : transform;

        // Cinemachine 3 API:
        cinemachineCamera.Follow = t;
        cinemachineCamera.LookAt = t;
    }
}
