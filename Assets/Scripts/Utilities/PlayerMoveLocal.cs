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

    [Header("Camera buffer (Solution 1)")]
    [SerializeField] private Camera mainCam;
    [Tooltip("Point on/near the head. If empty, leave null and we'll try to find it or fallback to this transform.")]
    [SerializeField] private Transform headPoint;
    [Tooltip("Minimum distance allowed between camera and headPoint. If closer, backward input is blocked.")]
    [SerializeField] private float minCamHeadDistance = 0.7f;
    [Tooltip("Extra slack before fully blocking (smooths the stop).")]
    [SerializeField] private float bufferSoftRange = 0.25f;

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

        if (mainCam == null)
            mainCam = Camera.main;

        // fallback: try to find a head bone if you didn't assign headPoint
        if (headPoint == null && animator != null && animator.isHuman)
            headPoint = animator.GetBoneTransform(HumanBodyBones.Head);

        if (headPoint == null)
            headPoint = transform; // fallback (still works, just less "head-accurate")
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

        // =========================
        // Solution 1: Block backward if camera too close
        // =========================
        if (forwardInput < -0.01f && mainCam != null && headPoint != null)
        {
            float d = Vector3.Distance(mainCam.transform.position, headPoint.position);

            // If already inside the hard minimum -> fully block backward
            if (d <= minCamHeadDistance)
            {
                forwardInput = 0f;
            }
            else if (bufferSoftRange > 0.001f)
            {
                // Soft block as you approach the minimum (prevents "snap stop")
                // When d == min -> scale 0, when d == min+soft -> scale 1
                float scale = Mathf.InverseLerp(minCamHeadDistance, minCamHeadDistance + bufferSoftRange, d);

                // Only scale backward (negative) input
                forwardInput *= Mathf.Clamp01(scale);
            }
        }

        bool hasForwardInput = Mathf.Abs(forwardInput) > 0.01f;
        bool hasTurnInput = Mathf.Abs(turnInput) > 0.01f;

        // =========================
        // SPEED (ACCEL / DECEL)
        // =========================
        float targetSpeed = forwardInput * maxSpeed;

        // אם אין קדימה/אחורה אבל כן לוחצים שמאלה/ימינה -> "צעד" + סיבוב
        // נבחר כיוון חיובי תמיד (קדימה), כדי להרגיש טבעי
        if (!hasForwardInput && hasTurnInput)
            targetSpeed = idleTurnMoveSpeed;

        float rate = (hasForwardInput || (!hasForwardInput && hasTurnInput)) ? acceleration : deceleration;
        speed = Mathf.MoveTowards(speed, targetSpeed, rate * Time.deltaTime);

        if (!hasForwardInput && !hasTurnInput && Mathf.Abs(speed) < 0.05f && Mathf.Abs(targetSpeed) < 0.01f)
            speed = 0f;

        // =========================
        // STEERING (TURN)
        // =========================
        // נרצה שהכיוון (moveDir) יסתובב גם כשעומדים ומסובבים
        if (hasTurnInput && Mathf.Abs(speed) > 0.01f)
        {
            float turn = turnInput * turnSpeed * Time.deltaTime;
            moveDir = Quaternion.Euler(0f, turn, 0f) * moveDir;
            moveDir.Normalize();
        }

        // אם אין תנועה בכלל, שמור moveDir מסונכרן לכיוון השחקן
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
