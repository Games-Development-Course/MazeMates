// PlayerRoleComponent.cs  (Fusion 2)
using Fusion;
using UnityEngine;

public class PlayerRoleComponent : NetworkBehaviour
{
    // Enum קיים אצלך איפשהו בפרויקט
    // public enum PlayerRole { Traveller, Navigator }

    [Networked]
    public PlayerRole Role { get; set; }

    public override void Spawned()
    {
        base.Spawned();

        // את Role עצמם תגדיר בשלב הספאון, למשל:
        // Runner.Spawn(prefab, pos, rot, player, (runner, obj) =>
        // {
        //     obj.GetComponent<PlayerRoleComponent>().Role = PlayerRole.Traveller;
        // });
    }
}
