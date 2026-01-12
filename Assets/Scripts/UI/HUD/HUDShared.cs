// /Assets/Scripts/HUD/HUDShared.cs
using TMPro;
using UnityEngine;

public class HUDShared : MonoBehaviour
{
    [Header("Texts")]
    public TMP_Text livesText;
    public TMP_Text keysText;
    public TMP_Text lifebuoysText;
    public TMP_Text giftsText;
    public TMP_Text bombRemovalText;

    [Header("Icon Targets (for fly-to HUD effects)")]
    public RectTransform livesIconTarget;
    public RectTransform keysIconTarget;
    public RectTransform lifebuoysIconTarget;
    public RectTransform giftsIconTarget;
    public RectTransform bombRemovalIconTarget;

    public void UpdateValues(GameManager gm)
    {
        if (livesText)
            livesText.text = "x " + gm.lives;
        if (keysText)
            keysText.text = "x " + gm.keys;
        if (lifebuoysText)
            lifebuoysText.text = "x " + gm.lifebuoys;
        if (giftsText)
            giftsText.text = "x " + gm.HeartPlacements;
        if (bombRemovalText)
            bombRemovalText.text = "x " + gm.BombRemovals;
    }
}
