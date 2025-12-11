// PlayerMovement1P.cs
using Fusion;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement1P : NetworkBehaviour
{
    [Header("Movement")]
    public float speed = 6f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;

    private TutorialManager tutorial;

    private bool isTraveller;
    private bool isNavigator;

    private bool movementFrozen = false;   // Default state

    public bool IsTraveller => CompareTag("Traveller");
    public bool IsNavigator => CompareTag("Navigator");

    // -------------------------------------------------------
    // LIFECYCLE
    // -------------------------------------------------------

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void Spawned()
    {
        base.Spawned();

        // זיהוי תפקיד לפי שם האובייקט (Traveller/Nav) – כמו בקוד המקורי
        DetectRoleByName();

        // המטייל נרשם ל-TutorialManager בקליינט שיש לו InputAuthority עליו
        if (HasInputAuthority && isTraveller)
        {
            var tm = FindFirstObjectByType<TutorialManager>();
            if (tm != null)
            {
                tm.RegisterTraveller(transform);

                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null)
                    tm.RegisterTravellerCamera(cam);
            }
        }
    }

    private void Start()
    {
        tutorial = FindFirstObjectByType<TutorialManager>();

        // גיבוי – אם משום מה Spawned לא רץ לפני Start
        if (!isTraveller && !isNavigator)
            DetectRoleByName();
    }

    private void DetectRoleByName()
    {
        string n = gameObject.name;
        isTraveller = n.Contains("Trav");
        isNavigator = n.Contains("Nav");
    }

    // -------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------

    public void SetFrozen(bool freeze)
    {
        movementFrozen = freeze;

        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (freeze && controller != null)
            controller.Move(Vector3.zero);
    }

    public bool IsFrozen => movementFrozen;

    /// <summary>
    /// Teleport בטוח לנקודת התחלה (משתמש ב-CharacterController כמו קודם).
    /// </summary>
    public void TeleportToStart(Vector3 pos)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        velocity = Vector3.zero;
        controller.enabled = false;
        transform.position = pos;
        controller.enabled = true;
    }

    // -------------------------------------------------------
    // UPDATE (LOCAL INPUT)
    // -------------------------------------------------------

    private void Update()
    {
        // רק מי שיש לו InputAuthority קורא קלט ומזיז את הדמות שלו
        if (!HasInputAuthority) return;
        if (controller == null) return;
        if (GameManager.Instance == null) return;

        // --- עצירה מוחלטת (טוטוריאל/לוקים) ---
        if (movementFrozen)
        {
            controller.Move(Vector3.zero);
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        bool moved = Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f;

        // ---- שליחת אירוע תנועה ל-StateAuthority (טוטוריאל) ----
        if (moved && tutorial != null && tutorial.TutorialActive)
        {
            if (isTraveller)
                NotifyMovementRpc(true);   // Traveller
            else if (isNavigator)
                NotifyMovementRpc(false);  // Navigator
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

    // -------------------------------------------------------
    // RPC – תנועת שחקן לצורך טוטוריאל
    // -------------------------------------------------------

    /// <summary>
    /// נקרא מהקליינט שבבעלותו השחקן, ומגיע ל-StateAuthority בלבד.
    /// שם אנחנו מעדכנים את ה-TutorialManager.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void NotifyMovementRpc(bool traveller, RpcInfo info = default)
    {
        var tm = FindFirstObjectByType<TutorialManager>();
        if (tm == null) return;

        if (traveller)
            tm.NotifyTravellerMoved();
        else
            tm.NotifyNavigatorMoved();
    }
}
