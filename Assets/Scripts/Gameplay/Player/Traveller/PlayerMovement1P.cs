// PlayerMovement1P.cs
using Unity.Netcode;
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

    private bool movementFrozen = false;   // Default state

    // ------------- IMPORTANT: Awake עבור ה-Controller -------------
    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        tutorial = Object.FindFirstObjectByType<TutorialManager>();
        isTraveller = name.Contains("Trav");
        isNavigator = name.Contains("Nav");
    }

    // =====================  API  =====================
    public void SetFrozen(bool freeze)
    {
        movementFrozen = freeze;

        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (freeze && controller != null)
            controller.Move(Vector3.zero);
    }

    public bool IsFrozen => movementFrozen;

    // =====================  UPDATE  =====================
    void Update()
    {
        if (!IsOwner) return;
        if (controller == null) return;
        if (GameManager.Instance == null) return;

        // --- STOP ---
        if (movementFrozen)
        {
            controller.Move(Vector3.zero);
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        bool moved = Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f;

        // ---- שליחת אירוע תנועה לסרבר (טוטוריאל) ----
        if (moved && tutorial != null && tutorial.TutorialActive.Value)
        {
            if (isTraveller)
                NotifyMovementServerRpc(true);   // Traveller
            else if (isNavigator)
                NotifyMovementServerRpc(false);  // Navigator
        }

        // ---- תנועה רגילה ----
        Vector3 move = transform.right * h + transform.forward * v;
        controller.Move(move * speed * Time.deltaTime);

        if (controller.isGrounded)
            velocity.y = -2f;
        else
            velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    // =====================  ServerRpc לתנועה  =====================

    [ServerRpc]
    private void NotifyMovementServerRpc(bool isTraveller)
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>();
        if (tm == null) return;

        if (isTraveller)
            tm.NotifyTravellerMoved();
        else
            tm.NotifyNavigatorMoved();
    }

    // Teleport safe
    public void TeleportToStart(Vector3 pos)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        velocity = Vector3.zero;
        controller.enabled = false;
        transform.position = pos;
        controller.enabled = true;
    }
}
