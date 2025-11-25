using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuController : MonoBehaviour
{
    public TMP_InputField travelerInput;
    public TMP_InputField navigatorInput;

    public ToggleDifficultyGroup difficultyGroup;

    public string travelerScene = "TravelerScene";
    public string navigatorScene = "NavigatorScene";

    public void OnStartButtonPressed()
    {
        // 1) Validate names
        if (
            string.IsNullOrWhiteSpace(travelerInput.text)
            || string.IsNullOrWhiteSpace(navigatorInput.text)
        )
        {
            Debug.Log("Names missing");
            return;
        }

        // 2) Validate difficulty
        if (!difficultyGroup.HasSelection())
        {
            Debug.Log("Difficulty not selected");
            return;
        }

        // 3) Store data for next scenes
        PlayerPrefs.SetString("TravelerName", travelerInput.text);
        PlayerPrefs.SetString("NavigatorName", navigatorInput.text);
        PlayerPrefs.SetString("Difficulty", difficultyGroup.GetSelectedDifficulty());

        // 4) Load windows:
        // Load Traveler
        System.Diagnostics.Process.Start(
            Application.dataPath.Replace("_Data", ".exe"),
            "-role traveler"
        );

        // Load Navigator
        System.Diagnostics.Process.Start(
            Application.dataPath.Replace("_Data", ".exe"),
            "-role navigator"
        );

        // 5) Close this window
        Application.Quit();
    }
}
