using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartButtonController : MonoBehaviour
{
    [Header("Difficulty Toggles")]
    public Toggle easyToggle;
    public Toggle mediumToggle;
    public Toggle hardToggle;

    [Header("Input Fields")]
    public TMP_InputField travelerInput;
    public TMP_InputField navigatorInput;

    [Header("Start Button")]
    public Button startButton;

    void Update()
    {
        bool difficultySelected = easyToggle.isOn || mediumToggle.isOn || hardToggle.isOn;

        bool namesFilled =
            !string.IsNullOrWhiteSpace(travelerInput.text)
            && !string.IsNullOrWhiteSpace(navigatorInput.text);

        startButton.interactable = difficultySelected && namesFilled;
    }
}
