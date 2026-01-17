// Assets/Scripts/Sandbox/PlayerMoveLocal.cs
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMoveLocal : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float deceleration = 40f;

    [Tooltip("How fast the movement direction turns when steering (deg/sec).")]
    [SerializeField] private float turnSpeed = 120f;

    [Tooltip("How fast the body aligns to the movement direction (deg/sec).")]
    [SerializeField] private float rotateSpeed = 720f;

    [Tooltip("When pressing Left/Right with no forward/back input, this is the speed used to 'step' + turn.")]
    [SerializeField] private float idleTurnMoveSpeed = 2.0f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStick = -2f;

    [Header("Animation")]
    [SerializeField] private Animator animator; // Animator על ה-Visual

    private CharacterController cc;

    private float speed;        // signed forward/back speed
    private Vector3 moveDir;    // current movement direction (planar)
    private float verticalVel;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        moveDir = transform.forward;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        // =========================
        // INPUT (ARROWS ONLY)
        // =========================
        float forwardInput = 0f;
        float turnInput = 0f;

        if (Input.GetKey(KeyCode.UpArrow)) forwardInput += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) forwardInput -= 1f;

        if (Input.GetKey(KeyCode.LeftArrow)) turnInput -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) turnInput += 1f;

        bool hasForwardInput = Mathf.Abs(forwardInput) > 0.01f;
        bool hasTurnInput = Mathf.Abs(turnInput) > 0.01f;

        // =========================
        // SPEED (ACCEL / DECEL)
        // =========================
        float targetSpeed = forwardInput * maxSpeed;

        // if no forward/back but yes left/right -> "step" + turn (always forward)
        if (!hasForwardInput && hasTurnInput)
            targetSpeed = idleTurnMoveSpeed;

        float rate = (hasForwardInput || (!hasForwardInput && hasTurnInput)) ? acceleration : deceleration;
        speed = Mathf.MoveTowards(speed, targetSpeed, rate * Time.deltaTime);

        if (!hasForwardInput && !hasTurnInput && Mathf.Abs(speed) < 0.05f && Mathf.Abs(targetSpeed) < 0.01f)
            speed = 0f;

        // =========================
        // STEERING (TURN)
        // =========================
        if (hasTurnInput && Mathf.Abs(speed) > 0.01f)
        {
            float turn = turnInput * turnSpeed * Time.deltaTime;
            moveDir = Quaternion.Euler(0f, turn, 0f) * moveDir;
            moveDir.Normalize();
        }

        if (Mathf.Abs(speed) <= 0.01f)
            moveDir = transform.forward;

        // =========================
        // FACE MOVE DIRECTION (Yaw)
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
        // ANIMATION UPDATE
        // =========================
        if (animator != null)
        {
            float planarSpeed01 = Mathf.Clamp01(new Vector2(cc.velocity.x, cc.velocity.z).magnitude / maxSpeed);
            animator.SetFloat(SpeedParam, planarSpeed01);
            animator.SetBool(IsMovingParam, planarSpeed01 > 0.05f);
        }
    }
}
