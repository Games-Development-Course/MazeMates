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

        Vector3 move = transform.right * h + transform.forward * v;

        controller.Move(move * speed * Time.deltaTime);

        // gravity
        if (controller.isGrounded)
            velocity.y = -2f;
        else
            velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }
    public void TeleportToStart(Vector3 pos)
    {
        // לאפס מהירות כדי שלא ימשיך לדחוף קדימה
        velocity = Vector3.zero;

        // להזיז דרך CharacterController
        controller.enabled = false;
        transform.position = pos;
        controller.enabled = true;
    }

}
