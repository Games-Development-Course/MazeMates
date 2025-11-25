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
        // מניעת NRE אם GameManager עדיין לא קיים
        if (GameManager.Instance == null)
            return;

        // מניעת NRE אם טרם נקלט CharacterController
        if (controller == null)
            return;

        // ============================
        //   SPACE → Try open door
        // ============================
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Traveller pressed SPACE");

            if (RoleManager.Role == PlayerRole.Traveler)
            {
                DoorController[] allDoors = FindObjectsByType<DoorController>(
                    FindObjectsSortMode.None
                );

                foreach (var d in allDoors)
                {
                    if (d.TravellerIsOnPad())
                    {
                        Debug.Log("Traveller is on pad of: " + d.name);

                        if (
                            DoorPadToggle.Instance != null
                            && !DoorPadToggle.Instance.allowSpaceActivation
                        )
                        {
                            Debug.Log("SPACE BLOCKED by DoorPadToggle");
                            break;
                        }

                        d.Interact();
                        break;
                    }
                }
            }
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // יציאה מהחידה אם השחקן זז
        if (GameManager.Instance.inPuzzle && (h != 0 || v != 0))
        {
            GameManager.Instance.inPuzzle = false;

            DoorController[] allDoors = FindObjectsByType<DoorController>(FindObjectsSortMode.None);

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
