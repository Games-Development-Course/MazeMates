using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartScreenManager : MonoBehaviour
{
    [Header("Inputs")]
    public TMP_InputField travelerName;
    public TMP_InputField navigatorName;

    [Header("Difficulty")]
    public Difficulty selectedDifficulty = Difficulty.None;
    public Button[] difficultyButtons;

    [Header("Start Button")]
    public Button startButton;

    void Update()
    {
        startButton.interactable =
            selectedDifficulty != Difficulty.None
            && !string.IsNullOrEmpty(travelerName.text)
            && !string.IsNullOrEmpty(navigatorName.text);
    }

    public void SetDifficulty(int diff)
    {
        selectedDifficulty = (Difficulty)diff;

        // ����� �������
        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            var colors = difficultyButtons[i].colors;

            if (i + 1 == diff)
                colors.normalColor = new Color(0.7f, 0.9f, 1f); // ����
            else
                colors.normalColor = Color.white; // �� ����

            difficultyButtons[i].colors = colors;
        }
    }

    public void OnStartPressed()
    {
        PlayerPrefs.SetString("traveler_name", travelerName.text);
        PlayerPrefs.SetString("navigator_name", navigatorName.text);
        PlayerPrefs.SetInt("difficulty", (int)selectedDifficulty);
        PlayerPrefs.Save();

        // ����� ��� ������
        BuildLauncher.LaunchBothWindows();

        // ���� �� ����� ������
        Application.Quit();
    }
}
