// PlayerMovement1P.cs
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement1P : NetworkBehaviour
{
    public float speed = 6f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;

    private TutorialManager tutorial;
    private bool isTraveller;
    private bool isNavigator;

    private bool movementFrozen = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
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

    void Start()
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

    void Update()
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

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        bool moved = Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f;

        if (moved && tutorial != null && tutorial.TutorialActive.Value)
        {
            if (isTraveller)
                NotifyMovementServerRpc(true);
            else if (isNavigator)
                NotifyMovementServerRpc(false);
        }

        Vector3 move = transform.right * h + transform.forward * v;
        controller.Move(move * speed * Time.deltaTime);

        if (controller.isGrounded)
            velocity.y = -2f;
        else
            velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    [ServerRpc]
    private void NotifyMovementServerRpc(bool isTraveller)
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null)
            return;

        if (isTraveller)
            tm.NotifyTravellerMoved();
        else
            tm.NotifyNavigatorMoved();
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

        // ✅ Tell server immediately (keeps server in sync, prevents delayed state)
        ConfirmTeleportServerRpc(worldPos, worldRot);

        yield return new WaitForSeconds(0.05f);
        SetFrozen(false);
    }

    [ServerRpc]
    private void ConfirmTeleportServerRpc(Vector3 worldPos, Quaternion worldRot)
    {
        // If you have NetworkTransform, use Teleport (best). Otherwise set transform.
        var nt = GetComponent<NetworkTransform>();
        if (nt != null)
        {
            nt.Teleport(worldPos, worldRot, transform.localScale);
            return;
        }

        transform.SetPositionAndRotation(worldPos, worldRot);
    }
}
