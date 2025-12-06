using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
        
    public Camera playerCamera;
    private void Start()
    {
        if (IsOwner)
        {
            GetComponent<MeshRenderer>().material.color = Color.green;
        }
        else
        {
            GetComponent<MeshRenderer>().material.color = Color.blue;
        }
    }


    public override void OnNetworkSpawn()
    {
        var renderer = GetComponent<MeshRenderer>();
        renderer.material = new Material(renderer.material);

        if (IsOwner)
        {
            renderer.material.color = Color.green;
            playerCamera.enabled = true;

            var listener = playerCamera.GetComponent<AudioListener>();
            if (listener) listener.enabled = true;
        }
        else
        {
            renderer.material.color = Color.blue;
            playerCamera.enabled = false;

            var listener = playerCamera.GetComponent<AudioListener>();
            if (listener) listener.enabled = false;
        }
    }



    void Update()
    {
        if (!IsOwner) return;

        // תנועה פושטית רק לבדיקה
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        transform.Translate(new Vector3(h, 0, v) * Time.deltaTime * 5f);
    }
}
