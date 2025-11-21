using UnityEngine;
using UnityEngine.UI;

public class ForceSelectedSprite : MonoBehaviour
{
    private Toggle t;
    private Image img;
    private Sprite normalSprite;
    private Sprite selectedSprite;

    void Start()
    {
        t = GetComponent<Toggle>();
        img = t.targetGraphic as Image;

        normalSprite = img.sprite;                 // הספרייט הרגיל
        selectedSprite = t.spriteState.selectedSprite; // הספרייט הלחוץ
    }

    void Update()
    {
        if (t.isOn)
        {
            img.overrideSprite = selectedSprite;   // תמיד מציג לחוץ
        }
        else
        {
            img.overrideSprite = normalSprite;     // חוזר לרגיל
        }
    }
}
