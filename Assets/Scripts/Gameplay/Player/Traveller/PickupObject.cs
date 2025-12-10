using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        // נשלוף טוטוריאל פעם אחת
        var tm = Object.FindFirstObjectByType<TutorialManager>();

        // ---------------------------------------------------------
        // עדכון ערכי משחק (רק בשרת)
        // ---------------------------------------------------------
        switch (type)
        {
            case PickupType.Heart:
                gm.lives++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת לב! קיבלת חיים נוספים.";

                // ⭐ הטוטוריאל – המטייל אסף לב
                tm?.NotifyTravellerPickedHeart();
                break;

            case PickupType.Key:
                gm.keys++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת מפתח!";

                // ⭐ הטוטוריאל – המטייל אסף מפתח
                tm?.NotifyTravellerPickedKey();
                break;

            case PickupType.Lifebuoy:
                gm.lifebuoys++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת מצוף הצלה!";
                break;

            case PickupType.Bomb:
                gm.lives--;

                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "דרכת על פצצה! איבדת לב.";

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

                    // -------------------------------
                    // תיקון חשוב – Traveller StartPoint
                    // -------------------------------
                    if (move != null && PlayerStartPoint.TravellerPoint != null)
                    {
                        Vector3 resetPos = PlayerStartPoint.TravellerPoint.startPosition;
                        Quaternion resetRot = PlayerStartPoint.TravellerPoint.startRotation;

                        move.TeleportToStart(resetPos);
                        other.transform.rotation = resetRot;
                    }
                }
                break;
        }

        // ---------------------------------------------------------
        // שולחים ל־Clients לעדכן HUD ולהציג הודעה
        // ---------------------------------------------------------
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

        // השמדת האובייקט מהרשת
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Despawn(true);
        else
            Destroy(gameObject);
    }

    // ================================================================
    //  CLIENT RPC – מציג הודעה ומעדכן HUD
    // ================================================================
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

        gm.lives = lives;
        gm.keys = keys;
        gm.lifebuoys = lifebuoys;

        if (!string.IsNullOrEmpty(msg))
        {
            hud.SetMessageAppearanceForBoth(color, duration);
            hud.ShowMessageForBoth(msg);
        }

        if (pickupType == PickupType.Bomb)
            hud.FlashTravellerLife();

        hud.UpdateHUDs();

        if (gameOver)
            SceneManager.LoadScene("GameOver");
    }
}
