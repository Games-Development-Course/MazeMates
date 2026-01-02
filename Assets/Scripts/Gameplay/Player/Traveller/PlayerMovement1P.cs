// =======================================================
// File: Assets/Scripts/Player/PlayerMovement1P.cs
// Arrow keys only:
//   Up/Down   = move forward/back
//   Left/Right= rotate (yaw)
// Mouse look is NOT used.
// NOTE: No NotifyLooked/NotifyLookServerRpc anymore.
//
// FIX (Spawn issue):
// - Disable CharacterController immediately on spawn for EVERYONE
// - For non-owner: keep CC disabled forever (NetworkTransform drives it)
// - For owner: enable CC one frame later (after Netcode sets spawn pose)
// - Block Update until readyForLocalMove == true
// =======================================================

using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement1P : NetworkBehaviour
{
    public enum PlayerRole { Traveller, Navigator }

    [Header("Role (set per prefab)")]
    [SerializeField] private PlayerRole role = PlayerRole.Traveller;

    [Header("Movement")]
    public float speed = 6f;                       // forward/back speed
    [SerializeField] private float turnSpeed = 180f; // degrees/sec

    [Header("Gravity")]
    public float gravity = -9.81f;

    [Header("Input (Arrow Keys Only)")]
    [SerializeField] private InputAction moveAction;

    private CharacterController controller;
    private Vector3 velocity;

    private TutorialManager tutorial;
    private bool movementFrozen;

    // ✅ prevents CC from messing with spawn before NetworkTransform syncs
    private bool readyForLocalMove = false;

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
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        // We will explicitly enable in OnNetworkSpawn for the owner after readiness.
        // Keeping this empty avoids enabling too early in Editor.
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        tutorial = Object.FindFirstObjectByType<TutorialManager>();

        if (controller == null)
            controller = GetComponent<CharacterController>();

        // ✅ CRITICAL: disable CC immediately so it can't push/snap on first frames
        if (controller != null)
            controller.enabled = false;

        // Non-owner: no input, CC stays off forever (NetworkTransform controls pose)
        if (!IsOwner)
        {
            moveAction?.Disable();
            readyForLocalMove = false;
            return;
        }

        // Owner: enable input now (but movement waits for CC re-enable next frame)
        moveAction?.Enable();
        readyForLocalMove = false;

        StartCoroutine(EnableControllerNextFrame());

        // Register traveller only if THIS prefab is traveller (not based on Host)
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
        // ✅ allow NGO/NetworkTransform to finish initial placement first
        yield return null;

        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (controller != null)
            controller.enabled = true;

        velocity = Vector3.zero;
        readyForLocalMove = true;
    }

    public void SetFrozen(bool freeze)
    {
        movementFrozen = freeze;

        if (freeze)
        {
            // stop any motion immediately
            velocity = Vector3.zero;

            if (controller == null)
                controller = GetComponent<CharacterController>();

            // if CC is disabled (remote), just exit
            if (controller != null && controller.enabled)
                controller.Move(Vector3.zero);
        }
    }

    public bool IsFrozen => movementFrozen;

    private void Update()
    {
        if (!IsOwner) return;
        if (!readyForLocalMove) return;
        if (controller == null || !controller.enabled) return;
        if (GameManager.Instance == null) return;

        if (movementFrozen)
        {
            controller.Move(Vector3.zero);
            return;
        }

        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        bool movedFB = Mathf.Abs(input.y) > 0.01f;
        bool turned = Mathf.Abs(input.x) > 0.01f;

        // Tutorial notify ONLY on movement/turn input (no look notify)
        if ((movedFB || turned) && tutorial != null && tutorial.TutorialActive.Value)
        {
            NotifyMovementServerRpc(role == PlayerRole.Traveller);
        }

        // Rotate (yaw)
        if (turned)
        {
            float yaw = input.x * turnSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, yaw);
        }

        // Forward/back
        if (movedFB)
        {
            Vector3 move = transform.forward * (input.y * speed * Time.deltaTime);
            controller.Move(move);
        }

        // Gravity
        if (controller.isGrounded)
            velocity.y = -2f;
        else
            velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyMovementServerRpc(bool traveller)
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null) return;

        if (traveller) tm.NotifyTravellerMoved();
        else tm.NotifyNavigatorMoved();
    }

    public void TeleportToStart(Vector3 pos)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        velocity = Vector3.zero;

        // safe teleport with CC
        if (controller != null)
            controller.enabled = false;

        transform.position = pos;

        if (controller != null)
            controller.enabled = true;
    }

    // ============================================================
    // ✅ BOMB RESET (RUNS ON OWNER CLIENT) - exact 7 args signature
    // ============================================================
    [ClientRpc]
    public void BombResetAndTeleportClientRpc(
        Vector3 worldPos,
        Quaternion worldRot,
        float preDelay,
        float redSeconds,
        float fadeOut,
        float fadeIn,
        ClientRpcParams rpcParams = default
    )
    {
        if (!IsOwner) return;

        // Only traveller HUD should play effect (role-based, not host-based)
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

    private IEnumerator BombResetRoutine(Vector3 worldPos, Quaternion worldRot, float preDelay)
    {
        yield return new WaitForSeconds(preDelay);

        if (controller == null)
            controller = GetComponent<CharacterController>();

        velocity = Vector3.zero;

        if (controller != null)
            controller.enabled = false;

        transform.SetPositionAndRotation(worldPos, worldRot);

        if (controller != null)
            controller.enabled = true;

        ConfirmTeleportServerRpc(worldPos, worldRot);

        yield return new WaitForSeconds(0.05f);
        SetFrozen(false);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ConfirmTeleportServerRpc(Vector3 worldPos, Quaternion worldRot)
    {
        var nt = GetComponent<NetworkTransform>();
        if (nt != null)
        {
            nt.Teleport(worldPos, worldRot, transform.localScale);
            return;
        }

        transform.SetPositionAndRotation(worldPos, worldRot);
    }
}
