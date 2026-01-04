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
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 25f;
    [SerializeField] private float rotateSpeed = 360f;
    [SerializeField] private bool faceMoveDirection = true;

    [Header("Gravity")]
    [SerializeField] private float gravity = -9.81f;

    [Header("Input (Arrow Keys Only)")]
    [SerializeField] private InputAction moveAction;

    // =======================
    // Animation
    // =======================
    [Header("Animation")]
    [SerializeField] private Animator animator;

    private CharacterController controller;
    private Vector3 planarVelocity;
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
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
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
        verticalVel = 0f;

        if (animator != null)
            animator.SetFloat("Speed", 0f);
    }

    public bool IsFrozen => movementFrozen;

    public void TeleportToStart(Vector3 pos)
    {
        planarVelocity = Vector3.zero;

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

    Vector2 input = moveAction.ReadValue<Vector2>();

    // LEFT/RIGHT = rotate (yaw)
    float turn = input.x;
    if (Mathf.Abs(turn) > 0.01f)
        transform.Rotate(Vector3.up, turn * rotateSpeed * Time.deltaTime);

    // UP/DOWN = forward/back in local space
    float forward = input.y;

    Vector3 desiredVel = transform.forward * (forward * maxSpeed);

    float rate = Mathf.Abs(forward) > 0.01f ? acceleration : deceleration;
    planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVel, rate * Time.deltaTime);

    bool moved = planarVelocity.sqrMagnitude > 0.01f || Mathf.Abs(turn) > 0.01f;
    if (moved && Time.time - lastMoveNotifyTime > 0.25f)
    {
        lastMoveNotifyTime = Time.time;
        NotifyMovementServerRpc();
    }

    if (controller.isGrounded && verticalVel < 0f)
        verticalVel = -2f;

    verticalVel += gravity * Time.deltaTime;

    Vector3 move = (planarVelocity + Vector3.up * verticalVel) * Time.deltaTime;
    controller.Move(move);

    if (animator != null)
    {
        float speed = new Vector2(planarVelocity.x, planarVelocity.z).magnitude;
        animator.SetFloat("Speed", speed);
        animator.SetBool("IsMoving", planarVelocity.sqrMagnitude > 0.01f);
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
