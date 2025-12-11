//using Unity.Netcode;
//using UnityEngine;

//public class FlyMovement : NetworkBehaviour
//{
//    public float speed = 10f;
//    public float fastSpeed = 25f;
//    public float verticalSpeed = 8f;

//    public override void OnNetworkSpawn()
//    {
//        if (!IsOwner)
//            enabled = false;  // רק בעל האובייקט שולט על התנועה
//    }

//    void Update()
//    {
//        // אם לא השחקן המקומי — לא זזים
//        if (!IsOwner) return;

//        float moveSpeed = Input.GetKey(KeyCode.LeftShift) ? fastSpeed : speed;

//        // תנועה אופקית
//        float h = Input.GetAxis("Horizontal");  // A D
//        float v = Input.GetAxis("Vertical");    // W S

//        Vector3 dir = transform.right * h + transform.forward * v;
//        transform.position += dir * moveSpeed * Time.deltaTime;

//        // עמידה במקום — מבט עם העכבר עדיין עובד
//        // תנועה למעלה
//        if (Input.GetKey(KeyCode.E))
//            transform.position += Vector3.up * verticalSpeed * Time.deltaTime;

//        // תנועה למטה
//        if (Input.GetKey(KeyCode.Q))
//            transform.position -= Vector3.up * verticalSpeed * Time.deltaTime;
//    }
//}
