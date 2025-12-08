using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement1P : NetworkBehaviour
{
    public float speed = 6f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Debug.Log($"[PlayerMovement1P] name={name}, Owner={OwnerClientId}, " +
                   $"Local={NetworkManager.Singleton.LocalClientId}, IsOwner={IsOwner}");
    }

    void Update()
    {
        if (!IsOwner) return;                     
        if (GameManager.Instance == null) return;
        if (controller == null) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // תנועה רגילה
        Vector3 move = transform.right * h + transform.forward * v;
        controller.Move(move * speed * Time.deltaTime);

        // גרביטי
        if (controller.isGrounded)
            velocity.y = -2f;
        else
            velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    // -------------------------------------------------------
    // Used by PickupObject.cs
    // -------------------------------------------------------
    public void TeleportToStart(Vector3 pos)
    {
        velocity = Vector3.zero;

        controller.enabled = false;
        transform.position = pos;
        controller.enabled = true;
    }
}
