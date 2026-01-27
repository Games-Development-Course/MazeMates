using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class ForceDestroyWithScene : MonoBehaviour
{
    private void Awake()
    {
        var no = GetComponent<NetworkObject>();
        if (!no) return;

        // שלא יישמר ב-DDOL בגלל owner
        no.DontDestroyWithOwner = false;

        // זה קיים אצלך (ראינו בדיבאג), אז זה אמור לקמפל
        no.DestroyWithScene = true;
    }
}
    