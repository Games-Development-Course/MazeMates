using UnityEngine;
using Unity.Netcode;

public class PlayerRoleComponent : NetworkBehaviour
{
    public PlayerRole Role;

    // כדי שהשרת יוכל לזהות מי זה מי
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // רק דוגמא — אתה תגדיר את זה בזמן הספאון
        // Role = RoleManager.Role; 
    }
}


