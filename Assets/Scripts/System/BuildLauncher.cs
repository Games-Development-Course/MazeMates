using System.Diagnostics;
using System.IO;
using UnityEngine;

public class BuildLauncher : MonoBehaviour
{
    public static void LaunchBothWindows()
    {
        string exePath = @"C:/UnityProjects/MazeMates/EXE/MazeMates.exe";

        if (!File.Exists(exePath))
        {
            UnityEngine.Debug.LogError("Executable not found: " + exePath);
            return;
        }

        // Traveler
        Process.Start(exePath, "-role traveler");

        // Navigator
        Process.Start(exePath, "-role navigator");
    }
}
