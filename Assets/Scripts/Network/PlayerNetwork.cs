// PlayerNetwork.cs  (Fusion 2 demo script)
using Fusion;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    public Camera playerCamera;

    private MeshRenderer rend;
    private AudioListener listener;

    private void Awake()
    {
        rend = GetComponent<MeshRenderer>();
        if (playerCamera != null)
            listener = playerCamera.GetComponent<AudioListener>();
    }

    public override void Spawned()
    {
        base.Spawned();

        if (rend != null)
        {
            // שיהיה מטריאל ייחודי לכל אובייקט
            rend.material = new Material(rend.material);
        }

        bool isMine = Object.HasInputAuthority;

        if (rend != null)
            rend.material.color = isMine ? Color.green : Color.blue;

        if (playerCamera != null)
            playerCamera.enabled = isMine;

        if (listener != null)
            listener.enabled = isMine;
    }

    private void Update()
    {
        if (!Object.HasInputAuthority)
            return;

        // תנועה פשוטה לבדיקה
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(h, 0f, v) * 5f * Time.deltaTime;
        transform.Translate(move, Space.World);
    }
}
