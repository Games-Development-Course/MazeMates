using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement1P : MonoBehaviour
{
    public float speed = 6f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // יציאה מהחידה אם השחקן זז
        if (GameManager.Instance.inPuzzle && (h != 0 || v != 0))
        {
            GameManager.Instance.inPuzzle = false;
            HUD.Instance.HidePuzzle();

            // סוגר את ה-Canvas של החידה בפועל
            DoorController[] allDoors =
            FindObjectsByType<DoorController>(FindObjectsSortMode.None);

            foreach (var d in allDoors)
            {
                if (d.doorType == DoorType.Puzzle)
                {
                    PuzzleDoor puzzle = d.GetPuzzle();
                    if (puzzle != null)
                        puzzle.ForceClosePuzzle();
                    break;
                }
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            return;
        }

        // תנועה רגילה
        Vector3 move = transform.right * h + transform.forward * v;
        controller.Move(move * speed * Time.deltaTime);

        // Gravity
        if (controller.isGrounded)
            velocity.y = -2f;
        else
            velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    public void TeleportToStart(Vector3 pos)
    {
        velocity = Vector3.zero;
        controller.enabled = false;
        transform.position = pos;
        controller.enabled = true;
    }
}
