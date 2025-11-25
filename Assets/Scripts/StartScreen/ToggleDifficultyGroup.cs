using UnityEngine;
using UnityEngine.UI;

public class ToggleDifficultyGroup : MonoBehaviour
{
    public Toggle easy;
    public Toggle medium;
    public Toggle hard;

    public bool HasSelection()
    {
        return easy.isOn || medium.isOn || hard.isOn;
    }

    public string GetSelectedDifficulty()
    {
        if (easy.isOn)
            return "easy";
        if (medium.isOn)
            return "medium";
        if (hard.isOn)
            return "hard";
        return "";
    }
}
