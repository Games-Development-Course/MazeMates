using UnityEngine;
using UnityEngine.SceneManagement;

public class StartupArgsReader : MonoBehaviour
{
    void Start()
    {
        string[] args = System.Environment.GetCommandLineArgs();

        foreach (string arg in args)
        {
            if (arg == "-role" || arg == "-role ")
                continue;

            if (arg.Contains("traveler"))
            {
                RoleManager.Role = PlayerRole.Traveler;
                SceneManager.LoadScene("TravellerScene");
            }
            else if (arg.Contains("navigator"))
            {
                RoleManager.Role = PlayerRole.Navigator;
                SceneManager.LoadScene("NavigatorScene");
            }
        }
    }
}
