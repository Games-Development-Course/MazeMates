using Unity.Netcode;
using UnityEngine;

public class FlyMovement : NetworkBehaviour
{
    public float speed = 10f;
    public float fastSpeed = 25f;
    public float verticalSpeed = 8f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            enabled = false; // �� ��� �������� ���� �� ������
    }

    void Update()
    {
        // �� �� ����� ������ � �� ����
        if (!IsOwner)
            return;

        float moveSpeed = Input.GetKey(KeyCode.LeftShift) ? fastSpeed : speed;

        // ����� ������
        float h = Input.GetAxis("Horizontal"); // A D
        float v = Input.GetAxis("Vertical"); // W S

        Vector3 dir = transform.right * h + transform.forward * v;
        transform.position += dir * moveSpeed * Time.deltaTime;

        // ����� ����� � ��� �� ����� ����� ����
        // ����� �����
        if (Input.GetKey(KeyCode.E))
            transform.position += Vector3.up * verticalSpeed * Time.deltaTime;

        // ����� ����
        if (Input.GetKey(KeyCode.Q))
            transform.position -= Vector3.up * verticalSpeed * Time.deltaTime;
    }
}
