// Assets/Scripts/Player/PlayerMovement.cs
// (Based on your current file, only movement/camera-relative smoothing logic replaced; deps & RPCs kept.)
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    public enum PlayerRole { Traveller, Navigator }

    [Header("Role (kept for tutorial / bomb logic)")]
    [SerializeField] private PlayerRole role = PlayerRole.Traveller;

    // =======================
    // Movement (TOP DOWN)
    // =======================
    [Header("Move")]
    [SerializeField] private float maxSpeed = 6f;

    [Tooltip("Time (seconds) to reach target speed. Smaller = snappier, bigger = smoother.")]
    [SerializeField] private float accelTime = 0.12f;

    [SerializeField] private float rotateSpeed = 12f;
    [SerializeField] private bool faceMoveDirection = true;

    [Header("Move Relative To Camera (Yaw Only)")]
    [SerializeField] private bool moveRelativeToCamera = true;
    [SerializeField] private Transform cameraTransform; // if null -> uses Camera.main on owner

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStick = -2f;

    [Header("Input (Arrow Keys Only)")]
    [SerializeField] private InputAction moveAction;

    // =======================
    // Animation
    // =======================
    [Header("Animation")]
    [SerializeField] private Animator animator;

    private CharacterController controller;

    // Smooth planar motion
    private Vector3 planarVelocity;          // what we actually move with (XZ)
    private Vector3 planarVelSmoothRef;      // SmoothDamp ref
    private float verticalVel;

    private bool movementFrozen;
    private bool ready;

    // =======================
    // Tutorial
    // =======================
    private TutorialManager tutorial;
    private float lastMoveNotifyTime = -999f;
    private float lastTutorialResolveTime = -999f;

    // =======================
    // Setup
    // =======================
    private void OnValidate()
    {
        if (moveAction == null)
            moveAction = new InputAction(type: InputActionType.Value);

        if (moveAction.bindings.Count == 0)
        {
            moveAction
                .AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
        }

        maxSpeed = Mathf.Max(0f, maxSpeed);
        accelTime = Mathf.Max(0.01f, accelTime);
        rotateSpeed = Mathf.Max(0f, rotateSpeed);
        groundedStick = Mathf.Min(groundedStick, -0.01f); // should be negative
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }

    public override void OnNetworkSpawn()
    {
        tutorial = Object.FindFirstObjectByType<TutorialManager>();

        if (controller != null)
            controller.enabled = false;

        if (!IsOwner)
        {
            moveAction?.Disable();
            ready = false;
            return;
        }

        // Prefer owner's camera
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        moveAction?.Enable();
        StartCoroutine(EnableControllerNextFrame());

        if (IsServer && role == PlayerRole.Traveller)
        {
            var tm = Object.FindFirstObjectByType<TutorialManager>();
            if (tm != null)
            {
                tm.RegisterTraveller(transform);

                Camera cam = GetComponentInChildren<Camera>(true);
                if (cam != null)
                    tm.RegisterTravellerCamera(cam);
            }
        }
    }

    private IEnumerator EnableControllerNextFrame()
    {
        yield return null;

        if (controller != null)
            controller.enabled = true;

        planarVelocity = Vector3.zero;
        planarVelSmoothRef = Vector3.zero;
        verticalVel = 0f;
        ready = true;
    }

    // =======================
    // Public API
    // =======================
    public void SetFrozen(bool freeze)
    {
        movementFrozen = freeze;
        planarVelocity = Vector3.zero;
        planarVelSmoothRef = Vector3.zero;
        verticalVel = 0f;

        if (animator != null)
            animator.SetFloat("Speed", 0f);
    }

    public bool IsFrozen => movementFrozen;

    public void TeleportToStart(Vector3 pos)
    {
        planarVelocity = Vector3.zero;
        planarVelSmoothRef = Vector3.zero;

        if (controller != null)
            controller.enabled = false;

        transform.position = pos;

        if (controller != null)
            controller.enabled = true;
    }

    // =======================
    // Update
    // =======================
    private void Update()
    {
#if UNITY_EDITOR
        bool allowLocal = true;
#else
        bool allowLocal = false;
#endif

        if ((!IsOwner && !allowLocal) || controller == null || !controller.enabled)
            return;

        if (!ready)
        {
            ready = true;
            moveAction?.Enable();
            planarVelocity = Vector3.zero;
            planarVelSmoothRef = Vector3.zero;
            verticalVel = 0f;
        }

        if (tutorial == null && Time.time - lastTutorialResolveTime > 0.5f)
        {
            lastTutorialResolveTime = Time.time;
            tutorial = Object.FindFirstObjectByType<TutorialManager>();
        }

        if (movementFrozen)
        {
            controller.Move(Vector3.zero);
            return;
        }

        // ---------
        // Input (arrows only, via InputAction)
        // ---------
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 rawDir = new Vector3(input.x, 0f, input.y);
        rawDir = Vector3.ClampMagnitude(rawDir, 1f);

        // Tutorial notify (unchanged behavior)
        if (rawDir.sqrMagnitude > 0.01f && Time.time - lastMoveNotifyTime > 0.25f)
        {
            lastMoveNotifyTime = Time.time;
            NotifyMovementServerRpc();
        }

        // ---------
        // Camera-relative yaw (optional)
        // ---------
        Vector3 desiredDir = rawDir;

        if (moveRelativeToCamera)
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (cameraTransform != null)
            {
                Vector3 camFwd = cameraTransform.forward;
                camFwd.y = 0f;
                camFwd.Normalize();

                Vector3 camRight = cameraTransform.right;
                camRight.y = 0f;
                camRight.Normalize();

                desiredDir = (camRight * rawDir.x + camFwd * rawDir.z);
                if (desiredDir.sqrMagnitude > 1f) desiredDir.Normalize();
            }
        }

        // ---------
        // Smooth planar velocity (SmoothDamp -> premium-feel accel/decel)
        // ---------
        Vector3 desiredPlanarVel = desiredDir * maxSpeed;
        planarVelocity = Vector3.SmoothDamp(planarVelocity, desiredPlanarVel, ref planarVelSmoothRef, accelTime);

        // ---------
        // Face move direction (smooth)
        // ---------
        if (faceMoveDirection)
        {
            Vector3 flat = new Vector3(planarVelocity.x, 0f, planarVelocity.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flat.normalized, Vector3.up);
                // exponential smoothing (stable across fps)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    1f - Mathf.Exp(-rotateSpeed * Time.deltaTime)
                );
            }
        }

        // ---------
        // Gravity (with grounded stick)
        // ---------
        if (controller.isGrounded && verticalVel < 0f)
            verticalVel = groundedStick;

        verticalVel += gravity * Time.deltaTime;

        Vector3 move = (new Vector3(planarVelocity.x, 0f, planarVelocity.z) + Vector3.up * verticalVel) * Time.deltaTime;
        controller.Move(move);

        // =======================
        // Animation Update (kept)
        // =======================
        if (animator != null)
        {
            float speed = new Vector2(planarVelocity.x, planarVelocity.z).magnitude;
            animator.SetFloat("Speed", speed);

            bool isMoving = planarVelocity.sqrMagnitude > 0.01f;
            animator.SetBool("IsMoving", isMoving);
        }
    }

    // =======================
    // Tutorial RPC
    // =======================
    [ServerRpc(RequireOwnership = false)]
    private void NotifyMovementServerRpc(ServerRpcParams rpcParams = default)
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null) return;

        bool senderIsTraveller = rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId;
        if (senderIsTraveller) tm.NotifyTravellerMoved();
        else tm.NotifyNavigatorMoved();
    }

    // =======================
    // BOMB RESET
    // =======================
    [ClientRpc]
    public void BombResetAndTeleportClientRpc(
        Vector3 worldPos,
        Quaternion worldRot,
        float preDelay,
        float redSeconds,
        float fadeOut,
        float fadeIn,
        ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        if (role == PlayerRole.Traveller)
        {
            var hud = HUDManager.Instance;
            if (hud != null && hud.TravellerHUD != null)
                hud.TravellerHUD.PlayBombResetEffect(redSeconds, fadeOut, fadeIn);
        }

        SetFrozen(true);
        StopAllCoroutines();
        StartCoroutine(BombResetRoutine(worldPos, worldRot, preDelay));
    }

    private IEnumerator BombResetRoutine(Vector3 pos, Quaternion rot, float delay)
    {
        yield return new WaitForSeconds(delay);

        planarVelocity = Vector3.zero;
        planarVelSmoothRef = Vector3.zero;

        if (controller != null)
            controller.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        if (controller != null)
            controller.enabled = true;

        ConfirmTeleportServerRpc(pos, rot);

        yield return new WaitForSeconds(0.05f);
        SetFrozen(false);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ConfirmTeleportServerRpc(Vector3 pos, Quaternion rot)
    {
        var nt = GetComponent<NetworkTransform>();
        if (nt != null)
            nt.Teleport(pos, rot, transform.localScale);
        else
            transform.SetPositionAndRotation(pos, rot);
    }
}
