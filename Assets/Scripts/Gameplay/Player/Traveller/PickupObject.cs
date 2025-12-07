using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class PickupObject : NetworkBehaviour
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
    public TMP_FontAsset messageFont;

    [Header("Message Duration")]
    public float messageDuration = 1.5f;

    private void OnTriggerEnter(Collider other)
    {
        // כל הלוגיקה מתבצעת רק על השרת
        if (!IsServer)
            return;

        if (!other.CompareTag("Player"))
            return;

        HUDManager hud = HUDManager.Instance;
        GameManager gm = GameManager.Instance;

        if (gm == null || hud == null)
            return;

        string finalMessage = customMessage;
        bool gameOver = false;

        // עדכון לוגיקה של המשחק על השרת
        switch (type)
        {
            case PickupType.Heart:
                gm.lives++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת לב! קיבלת חיים נוספים.";
                break;

            case PickupType.Key:
                gm.keys++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת מפתח!";
                break;

            case PickupType.Lifebuoy:
                gm.lifebuoys++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת מצוף הצלה! השתמש בו כדי להימנע מהפסד.";
                break;

            case PickupType.Bomb:
                gm.lives--;

                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "דרכת על פצצה! איבדת לב.";

                // לוגיקת בומבה – רק על השרת, כי השרת מזיז את השחקן והמצב יסונכרן
                hud.FlashTravellerLife();

                if (gm.lives <= 0)
                {
                    gameOver = true;
                }
                else
                {
                    var cam = other.GetComponentInChildren<PlayerCamera1P>();
                    if (cam != null)
                        cam.LockCameraForSeconds(0.5f);

                    var move = other.GetComponent<PlayerMovement1P>();
                    if (move != null && PlayerStartPoint.Instance != null)
                        move.TeleportToStart(PlayerStartPoint.Instance.startPosition);
                }
                break;
        }

        // שולחים לכולם סינכרון HUD + הודעה
        ApplyPickupClientRpc(
            type,
            finalMessage,
            messageColor,
            messageDuration,
            gm.lives,
            gm.keys,
            gm.lifebuoys,
            gameOver
        );

        // הורדת האובייקט מהשרת (ומשם מכל הקליינטים)
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [ClientRpc]
    private void ApplyPickupClientRpc(
        PickupType pickupType,
        string msg,
        Color color,
        float duration,
        int lives,
        int keys,
        int lifebuoys,
        bool gameOver)
    {
        HUDManager hud = HUDManager.Instance;
        GameManager gm = GameManager.Instance;

        if (hud == null || gm == null)
            return;

        // מעדכנים את הערכים לפי מה שהשרת החליט
        gm.lives = lives;
        gm.keys = keys;
        gm.lifebuoys = lifebuoys;

        if (!string.IsNullOrEmpty(msg))
        {
            hud.SetMessageAppearanceForBoth(color, duration);
            hud.ShowMessageForBoth(msg);
        }

        // בומבה – כבר הבהבנו חיים בשרת, אבל אפשר לוודא שוב לוגיקה ויזואלית
        if (pickupType == PickupType.Bomb)
        {
            hud.FlashTravellerLife();
        }

        hud.UpdateHUDs();

        if (gameOver)
        {
            SceneManager.LoadScene("GameOver");
        }
    }
}
