// =======================================================
// File: Assets/Scripts/Player/PlayerMovement1P.cs
// FIX: Tutorial step "BothMoved" not progressing.
//
// Why it was stuck:
// - Update() returned early when GameManager.Instance == null (common in TutorialScene on client).
// - tutorial ref could be null on client if TutorialManager spawns after this player's OnNetworkSpawn.
//
// Fixes:
// 1) Remove GameManager gate from movement & tutorial notify.
// 2) Re-resolve TutorialManager if null (throttled).
// 3) Notify server on movement input even if tutorial ref is not yet resolved.
// 4) Server determines Traveller vs Navigator by SenderClientId (authoritative).
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

    [Header("Role (optional / kept for other systems)")]
    [SerializeField] private PlayerRole role = PlayerRole.Traveller;

    [Header("Movement")]
    public float speed = 6f;
    [SerializeField] private float turnSpeed = 180f;

    [Header("Gravity")]
    public float gravity = -9.81f;

    [Header("Input (Arrow Keys Only)")]
    [SerializeField] private InputAction moveAction;

    private CharacterController controller;
    private Vector3 velocity;

    private TutorialManager tutorial;
    private bool movementFrozen;
    private bool readyForLocalMove = false;

    private float lastMoveNotifyTime = -999f;
    private float lastTutorialResolveTime = -999f;

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

        if (controller != null)
            controller.enabled = false;

        if (!IsOwner)
        {
            moveAction?.Disable();
            readyForLocalMove = false;
            return;
        }

        moveAction?.Enable();
        readyForLocalMove = false;

        StartCoroutine(EnableControllerNextFrame());

        // Kept: traveller registration (server-side)
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

        Debug.Log($"[PM] OnNetworkSpawn | IsOwner={IsOwner} IsServer={IsServer} role(prefab)={role} LocalClientId={(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId.ToString() : "NULL")} tutorial={(tutorial != null ? "FOUND" : "NULL")}");
    }

    private IEnumerator EnableControllerNextFrame()
    {
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
            velocity = Vector3.zero;

            if (controller == null)
                controller = GetComponent<CharacterController>();

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

        // ✅ Re-resolve TutorialManager if it spawned after us (common on client)
        if (tutorial == null && Time.time - lastTutorialResolveTime > 0.5f)
        {
            lastTutorialResolveTime = Time.time;
            tutorial = Object.FindFirstObjectByType<TutorialManager>();
            if (tutorial != null)
                Debug.Log("[PM] TutorialManager resolved late on this client.");
        }

        if (movementFrozen)
        {
            controller.Move(Vector3.zero);
            return;
        }

        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        bool movedFB = Mathf.Abs(input.y) > 0.01f;
        bool turned = Mathf.Abs(input.x) > 0.01f;

        // ✅ Always notify server when movement input happens.
        // (Do NOT depend on GameManager or TutorialActive being synced yet on client)
        if ((movedFB || turned) && Time.time - lastMoveNotifyTime > 0.25f)
        {
            lastMoveNotifyTime = Time.time;

            var nm = NetworkManager.Singleton;
            string side = (nm != null && nm.LocalClientId == NetworkManager.ServerClientId) ? "HOST/Traveller" : "CLIENT/Navigator";
            Debug.Log($"[PM] MoveInput -> NotifyMovementServerRpc | side={side} input={input} tutorial={(tutorial != null ? "FOUND" : "NULL")}");

            NotifyMovementServerRpc();
        }

        if (turned)
        {
            float yaw = input.x * turnSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, yaw);
        }

        if (movedFB)
        {
            Vector3 move = transform.forward * (input.y * speed * Time.deltaTime);
            controller.Move(move);
        }

        if (controller.isGrounded)
            velocity.y = -2f;
        else
            velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    // ✅ No parameters. Server decides role by SenderClientId (authoritative).
    [ServerRpc(RequireOwnership = false)]
    private void NotifyMovementServerRpc(ServerRpcParams rpcParams = default)
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null)
        {
            Debug.LogWarning("[PM][ServerRpc] NotifyMovementServerRpc: TutorialManager not found on server");
            return;
        }

        ulong sender = rpcParams.Receive.SenderClientId;
        bool senderIsTraveller = (sender == NetworkManager.ServerClientId);

        Debug.Log($"[PM][ServerRpc] NotifyMovementServerRpc | sender={sender} => {(senderIsTraveller ? "Traveller" : "Navigator")}");

        if (senderIsTraveller) tm.NotifyTravellerMoved();
        else tm.NotifyNavigatorMoved();
    }

    public void TeleportToStart(Vector3 pos)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        velocity = Vector3.zero;

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
