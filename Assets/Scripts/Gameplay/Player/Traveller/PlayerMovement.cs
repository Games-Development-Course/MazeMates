// Assets/Scripts/Player/PlayerMovement.cs
// Networked version that behaves like Sandbox PlayerMoveLocal (arrows-only: forward/back + turn),
// while KEEPING your existing dependencies (TutorialManager notify RPCs, bomb reset, HUD, teleport confirm).
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

    // =========================
    // Move (Sandbox-like)
    // =========================
    [Header("Move")]
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float deceleration = 40f;

    [Tooltip("When ONLY Left/Right is pressed (no Up/Down), acceleration is multiplied by this (slower ramp).")]
    [SerializeField, Range(0.05f, 1f)] private float sideAccelerationMultiplier = 0.4f;

    [Tooltip("How fast the movement direction turns when steering (deg/sec).")]
    [SerializeField] private float turnSpeed = 120f;

    [Tooltip("How fast the body aligns to the movement direction (deg/sec).")]
    [SerializeField] private float rotateSpeed = 720f;

    [Tooltip("When pressing Left/Right with no forward/back input, this is the speed used to 'step' + turn.")]
    [SerializeField] private float idleTurnMoveSpeed = 2.0f;

    // =========================
    // Camera buffer (Sandbox Solution 1)
    // =========================
    [Header("Camera buffer (Solution 1)")]
    [SerializeField] private Camera mainCam;
    [Tooltip("Point on/near the head. If empty, we try to use head bone, else fallback to this transform.")]
    [SerializeField] private Transform headPoint;
    [Tooltip("Minimum distance allowed between camera and headPoint. If closer, backward input is blocked.")]
    [SerializeField] private float minCamHeadDistance = 0.7f;
    [Tooltip("Extra slack before fully blocking (smooths the stop).")]
    [SerializeField] private float bufferSoftRange = 0.25f;

    // =========================
    // Gravity
    // =========================
    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStick = -2f;

    // =========================
    // Input (Arrow Keys Only)
    // =========================
    [Header("Input (Arrow Keys Only)")]
    [SerializeField] private InputAction moveAction;

    // =========================
    // Animation
    // =========================
    [Header("Animation")]
    [SerializeField] private Animator animator;

    private CharacterController cc;

    // Sandbox state
    private float speed;           // signed forward/back speed
    private Vector3 moveDir;       // current planar movement direction
    private float verticalVel;

    // Freeze / readiness
    private bool movementFrozen;
    private bool ready;

    // Tutorial
    private TutorialManager tutorial;
    private float lastMoveNotifyTime = -999f;
    private float lastTutorialResolveTime = -999f;

    // Animator params (match your existing controller if using these names)
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");

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
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        sideAccelerationMultiplier = Mathf.Clamp(sideAccelerationMultiplier, 0.05f, 1f);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        rotateSpeed = Mathf.Max(0f, rotateSpeed);
        idleTurnMoveSpeed = Mathf.Max(0f, idleTurnMoveSpeed);
        groundedStick = Mathf.Min(groundedStick, -0.01f);
    }

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        moveDir = transform.forward;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (mainCam == null)
            mainCam = Camera.main;

        // Head fallback
        if (headPoint == null && animator != null && animator.isHuman)
            headPoint = animator.GetBoneTransform(HumanBodyBones.Head);

        if (headPoint == null)
            headPoint = transform;
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }

    public override void OnNetworkSpawn()
    {
        tutorial = Object.FindFirstObjectByType<TutorialManager>();

        // Prevent CC weird first-frame offsets
        if (cc != null)
            cc.enabled = false;

        if (!IsOwner)
        {
            moveAction?.Disable();
            ready = false;
            return;
        }

        // Owner uses local camera
        if (mainCam == null)
            mainCam = Camera.main;

        moveAction?.Enable();
        StartCoroutine(EnableControllerNextFrame());

        // Keep your existing tutorial registration behavior (Traveller on server)
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

        if (cc != null)
            cc.enabled = true;

        speed = 0f;
        moveDir = transform.forward;
        verticalVel = 0f;
        ready = true;
    }

    // =======================
    // Public API (kept)
    // =======================
    public void SetFrozen(bool freeze)
    {
        movementFrozen = freeze;
        speed = 0f;
        verticalVel = 0f;

        if (animator != null)
        {
            animator.SetFloat(SpeedParam, 0f);
            animator.SetBool(IsMovingParam, false);
        }
    }

    public bool IsFrozen => movementFrozen;

    public void TeleportToStart(Vector3 pos)
    {
        speed = 0f;
        verticalVel = 0f;

        if (cc != null)
            cc.enabled = false;

        transform.position = pos;

        if (cc != null)
            cc.enabled = true;
    }

    public void SetRole(PlayerRole r) => role = r;

    // =======================
    // Update
    // =======================
    private void Update()
    {
        if (!IsOwner || cc == null)
            return;

        if (!ready)
        {
            // safety if something spawned weirdly
            ready = true;
            moveAction?.Enable();
            speed = 0f;
            verticalVel = 0f;
            moveDir = transform.forward;
        }

        if (!cc.enabled)
            return;

        if (tutorial == null && Time.time - lastTutorialResolveTime > 0.5f)
        {
            lastTutorialResolveTime = Time.time;
            tutorial = Object.FindFirstObjectByType<TutorialManager>();
        }

        if (movementFrozen)
        {
            cc.Move(Vector3.zero);
            return;
        }

        // =========================
        // INPUT (ARROWS ONLY)
        // InputAction gives us a 2DVector:
        //   x = Left/Right (TURN), y = Up/Down (FORWARD)
        // =========================
        Vector2 input = moveAction.ReadValue<Vector2>();
        float turnInput = Mathf.Clamp(input.x, -1f, 1f);
        float forwardInput = Mathf.Clamp(input.y, -1f, 1f);

        // =========================
        // Solution 1: Block backward if camera too close
        // =========================
        if (forwardInput < -0.01f)
        {
            if (mainCam == null) mainCam = Camera.main;

            if (mainCam != null && headPoint != null)
            {
                float d = Vector3.Distance(mainCam.transform.position, headPoint.position);

                if (d <= minCamHeadDistance)
                {
                    forwardInput = 0f;
                }
                else if (bufferSoftRange > 0.001f)
                {
                    float scale = Mathf.InverseLerp(minCamHeadDistance, minCamHeadDistance + bufferSoftRange, d);
                    forwardInput *= Mathf.Clamp01(scale);
                }
            }
        }

        bool hasForwardInput = Mathf.Abs(forwardInput) > 0.01f;
        bool hasTurnInput = Mathf.Abs(turnInput) > 0.01f;

        // Tutorial notify (unchanged behavior)
        // Notify when there is meaningful motion intent (forward or turning step)
        if ((hasForwardInput || hasTurnInput) && Time.time - lastMoveNotifyTime > 0.25f)
        {
            lastMoveNotifyTime = Time.time;
            NotifyMovementServerRpc();
        }

        // =========================
        // SPEED (ACCEL / DECEL) - sandbox logic
        // =========================
        float targetSpeed = forwardInput * maxSpeed;

        // If no forward/back but turning -> small forward step + turn
        bool idleTurnStep = !hasForwardInput && hasTurnInput;
        if (idleTurnStep)
            targetSpeed = idleTurnMoveSpeed;

        // ✅ Accel slower when ONLY Left/Right is pressed (idle turn step)
        float accelRate = idleTurnStep ? (acceleration * sideAccelerationMultiplier) : acceleration;

        float rate = (hasForwardInput || idleTurnStep) ? accelRate : deceleration;
        speed = Mathf.MoveTowards(speed, targetSpeed, rate * Time.deltaTime);

        if (!hasForwardInput && !hasTurnInput && Mathf.Abs(speed) < 0.05f && Mathf.Abs(targetSpeed) < 0.01f)
            speed = 0f;

        // =========================
        // STEERING (TURN) - sandbox logic
        // rotate moveDir while moving
        // =========================
        if (hasTurnInput && Mathf.Abs(speed) > 0.01f)
        {
            float turn = turnInput * turnSpeed * Time.deltaTime;
            moveDir = Quaternion.Euler(0f, turn, 0f) * moveDir;
            moveDir.Normalize();
        }

        // If not moving -> keep moveDir synced with facing
        if (Mathf.Abs(speed) <= 0.01f)
            moveDir = transform.forward;

        // =========================
        // FACE MOVE DIRECTION (Yaw) - sandbox logic
        // =========================
        if (moveDir.sqrMagnitude > 0.001f && Mathf.Abs(speed) > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotateSpeed * Time.deltaTime
            );
        }

        // =========================
        // GRAVITY
        // =========================
        if (cc.isGrounded && verticalVel < 0f)
            verticalVel = groundedStick;

        verticalVel += gravity * Time.deltaTime;

        // =========================
        // APPLY MOVE
        // =========================
        Vector3 move = (moveDir * speed + Vector3.up * verticalVel) * Time.deltaTime;
        cc.Move(move);

        // =========================
        // ANIMATION UPDATE (safe)
        // =========================
        if (animator != null)
        {
            float planarSpeed01 = Mathf.Clamp01(new Vector2(cc.velocity.x, cc.velocity.z).magnitude / Mathf.Max(0.001f, maxSpeed));

            // Only set if parameters exist in the controller (prevents "Hash ... does not exist")
            if (HasAnimParam(animator, AnimatorControllerParameterType.Float, "Speed"))
                animator.SetFloat("Speed", planarSpeed01);

            if (HasAnimParam(animator, AnimatorControllerParameterType.Bool, "IsMoving"))
                animator.SetBool("IsMoving", planarSpeed01 > 0.05f);
        }

    }

    // =======================
    // Tutorial RPC (kept)
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
    // BOMB RESET (kept)
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

        speed = 0f;
        verticalVel = 0f;
        moveDir = transform.forward;

        if (cc != null)
            cc.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        if (cc != null)
            cc.enabled = true;

        ConfirmTeleportServerRpc(pos, rot);

        yield return new WaitForSeconds(0.05f);
        SetFrozen(false);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ConfirmTeleportServerRpc(Vector3 pos, Quaternion rot)
    {
        // If you use NetworkTransform/ClientNetworkTransform, Teleport helps snap remote interpolation cleanly
        var nt = GetComponent<NetworkTransform>();
        if (nt != null)
            nt.Teleport(pos, rot, transform.localScale);
        else
            transform.SetPositionAndRotation(pos, rot);
    }
    private static bool HasAnimParam(Animator a, AnimatorControllerParameterType type, string name)
    {
        if (a == null || a.runtimeAnimatorController == null) return false;

        foreach (var p in a.parameters)
            if (p.type == type && p.name == name)
                return true;

        return false;
    }

}

