using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PickupObject : MonoBehaviour
{
    public enum PickupType
    {
        Heart,
        Key,
        Bomb,
        Lifebuoy,
    }

    public PickupType type;

    [Header("Custom Message Settings")]
    [TextArea(2, 5)]
    public string customMessage = "";

    public Color messageColor = Color.white;
    public TMP_FontAsset messageFont; // נשאר לשימוש עתידי אך לא חלק מהפונקציה החדשה

    [Header("Message Duration")]
    public float messageDuration = 1.5f; // כמה זמן הצבע המיוחד יישאר

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        HUDManager hud = HUDManager.Instance;
        GameManager gm = GameManager.Instance;

        // --- הודעה מותאמת אישית ---
        if (!string.IsNullOrEmpty(customMessage))
        {
            hud.SetMessageAppearanceForBoth(messageColor, messageDuration);
            hud.ShowMessageForBoth(customMessage);
        }

        // --- טיפוסים ---
        switch (type)
        {
            case PickupType.Heart:
                gm.lives++;
                hud.ShowMessageForBoth("אספת לב! קיבלת חיים נוספים.");
                hud.UpdateHUDs();
                break;

            case PickupType.Key:
                gm.keys++;
                hud.ShowMessageForBoth("אספת מפתח!");
                hud.UpdateHUDs();
                break;

            case PickupType.Lifebuoy:
                gm.lifebuoys++;
                hud.ShowMessageForBoth("אספת מצוף הצלה! השתמש בו כדי להימנע מהפסד.");
                hud.UpdateHUDs();
                break;

            case PickupType.Bomb:
                gm.lives--;
                HUDManager.Instance.FlashTravellerLife();



                hud.UpdateHUDs();

                if (gm.lives <= 0)
                {
                    SceneManager.LoadScene("GameOver");
                }
                else
                {
                    other.GetComponentInChildren<PlayerCamera1P>()?.LockCameraForSeconds(0.5f);
                    other
                        .GetComponent<PlayerMovement1P>()
                        ?.TeleportToStart(PlayerStartPoint.Instance.startPosition);
                }
                break;
        }

        Destroy(gameObject);
    }
}
