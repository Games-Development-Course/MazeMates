using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class PickupObject : MonoBehaviour
{
    public enum PickupType { Heart, Key, Bomb, Lifebuoy }

    public PickupType type;

    [Header("Custom Message Settings")]
    [TextArea(2, 5)]
    public string customMessage = "";

    public Color messageColor = Color.white;
    public TMP_FontAsset messageFont;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        HUDManager hud = HUDManager.Instance;
        GameManager gm = GameManager.Instance;

        // הצגת הודעה מותאמת מראש
        if (!string.IsNullOrEmpty(customMessage))
        {
            hud.SetMessageAppearanceForBoth(messageColor, messageFont);
            hud.ShowMessageForBoth(customMessage);
        }

        switch (type)
        {
            case PickupType.Heart:
                gm.lives++;
                hud.ShowMessageForBoth("אספתם לב! החיים עלו ב־1.");
                hud.UpdateHUDs();
                break;

            case PickupType.Key:
                gm.keys++;
                hud.ShowMessageForBoth("אספתם מפתח!");
                hud.UpdateHUDs();
                break;

            case PickupType.Lifebuoy:
                gm.lifebuoys++;
                hud.ShowMessageForBoth("קיבלתם גלגל הצלה! הנווט יכול להשתמש בו אם החידה קשה מדי.");
                hud.UpdateHUDs();
                break;

            case PickupType.Bomb:
                gm.lives--;
                hud.FlashLifeIcons();
                hud.UpdateHUDs();

                if (gm.lives <= 0)
                {
                    SceneManager.LoadScene("GameOver");
                }
                else
                {
                    other.GetComponentInChildren<PlayerCamera1P>()?.LockCameraForSeconds(0.5f);
                    other.GetComponent<PlayerMovement1P>()?.TeleportToStart(PlayerStartPoint.Instance.startPosition);
                }
                break;
        }

        Destroy(gameObject);
    }
}
