using UnityEngine;
using UnityEngine.UI;

public class ArrowTurnManager : MonoBehaviour
{
    [Header("Arrow UI")]
    public Image arrowImage;

    private RectTransform rt;

    private Vector2 leftPos;   // originalX - 540
    private Vector2 rightPos;  // originalX + 540
    private bool onLeft = true;

    public static ArrowTurnManager Instance;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        rt = arrowImage.rectTransform;

        // נקודה התחלתית:
        Vector2 original = rt.anchoredPosition;

        // קובעים מראש שתי נקודות קבועות
        leftPos = new Vector2(original.x - 540f, original.y);
        rightPos = new Vector2(original.x + 540f, original.y);

        // מתחילים בצד ימין (כי אצלך זה 270)
        rt.anchoredPosition = rightPos;
        onLeft = false;
    }

    public void SwitchTurn()
    {
        if (onLeft)
            rt.anchoredPosition = rightPos;
        else
            rt.anchoredPosition = leftPos;

        onLeft = !onLeft;
    }
}
