// =======================================================
// File: Assets/Scripts/Player/PlayerMovement1P.cs
// Arrow keys only:
//   Up/Down  = move forward/back
//   Left/Right = rotate (yaw)
// Mouse look is NOT used.
// =======================================================
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement1P : NetworkBehaviour
{
    [Header("Movement")]
    public float speed = 6f; // forward/back speed
    [SerializeField] private float turnSpeed = 180f; // degrees/sec

    [Header("Gravity")]
    public float gravity = -9.81f;

    [Header("Input (Arrow Keys Only)")]
    [SerializeField] private InputAction moveAction;

    private CharacterController controller;
    private Vector3 velocity;

    private TutorialManager tutorial;
    private bool isTraveller;
    private bool isNavigator;

    private bool movementFrozen;

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
        moveAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
            return;

        if (IsHost)
        {
            isTraveller = true;

            var tm = Object.FindFirstObjectByType<TutorialManager>();
            if (tm != null)
            {
                tm.RegisterTraveller(transform);

                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null)
                    tm.RegisterTravellerCamera(cam);
            }
        }
        else
        {
            isTraveller = false;
        }
    }

    private void Start()
    {
        tutorial = Object.FindFirstObjectByType<TutorialManager>();
        isTraveller = name.Contains("Trav");
        isNavigator = name.Contains("Nav");
    }

    public void SetFrozen(bool freeze)
    {
        movementFrozen = freeze;

        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (freeze && controller != null)
            controller.Move(Vector3.zero);
    }

    public bool IsFrozen => movementFrozen;

    private void Update()
    {
        if (!IsOwner)
            return;
        if (controller == null)
            return;
        if (GameManager.Instance == null)
            return;

        if (movementFrozen)
        {
            controller.Move(Vector3.zero);
            return;
        }

        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        bool movedFB = Mathf.Abs(input.y) > 0.01f;
        bool turned = Mathf.Abs(input.x) > 0.01f;

        if ((movedFB || turned) && tutorial != null && tutorial.TutorialActive.Value)
        {
            if (isTraveller)
                NotifyMovementServerRpc(true);
            else if (isNavigator)
                NotifyMovementServerRpc(false);

            if (turned)
            {
                if (isTraveller)
                    NotifyLookServerRpc(true);
                else if (isNavigator)
                    NotifyLookServerRpc(false);
            }
        }

        // Rotate first (yaw)
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

    [ServerRpc]
    private void NotifyMovementServerRpc(bool traveller)
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null)
            return;

        if (traveller)
            tm.NotifyTravellerMoved();
        else
            tm.NotifyNavigatorMoved();
    }

    [ServerRpc]
    private void NotifyLookServerRpc(bool traveller)
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null)
            return;

        if (traveller)
            tm.NotifyTravellerLooked();
        else
            tm.NotifyNavigatorLooked();
    }

    public void TeleportToStart(Vector3 pos)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        velocity = Vector3.zero;
        controller.enabled = false;
        transform.position = pos;
        controller.enabled = true;
    }

    // ============================================================
    // ✅ BOMB RESET (RUNS ON OWNER CLIENT)
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
        if (!IsOwner)
            return;

        if (IsHost)
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
        controller.enabled = false;
        transform.SetPositionAndRotation(worldPos, worldRot);
        controller.enabled = true;

        ConfirmTeleportServerRpc(worldPos, worldRot);

        yield return new WaitForSeconds(0.05f);
        SetFrozen(false);
    }

    [ServerRpc]
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
