using UnityEngine;

public class StartupArgsReader : MonoBehaviour
{
    void Awake()
    {
        string[] args = System.Environment.GetCommandLineArgs();

        // ברירת מחדל
        RoleManager.Role = PlayerRole.Traveler;

        foreach (string arg in args)
        {
            if (arg.ToLower().Contains("navigator"))
            {
                RoleManager.Role = PlayerRole.Navigator;
                break;
            }

            if (arg.ToLower().Contains("traveler"))
            {
                RoleManager.Role = PlayerRole.Traveler;
                break;
            }
        }
    }
}
