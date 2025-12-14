// PickupObject.cs
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

    [Header("Bomb Reset Visuals")]
    [Tooltip("כמה זמן האדום+פייד-אאוט יהיו לפני הטלפורט")]
    public float bombPreTeleportDelay = 0.25f;

    [Tooltip("כמה זמן האדום נשאר דולק (חופף עם הפייד)")]
    public float bombRedSeconds = 0.15f;

    [Tooltip("זמן פייד-אאוט")]
    public float bombFadeOut = 0.25f;

    [Tooltip("זמן פייד-אין")]
    public float bombFadeIn = 0.35f;

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

        var tm = Object.FindFirstObjectByType<TutorialManager>();

        switch (type)
        {
            case PickupType.Heart:
                gm.lives++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת לב! קיבלת חיים נוספים.";
                tm?.NotifyTravellerPickedHeart();
                break;

            case PickupType.Key:
                gm.keys++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת מפתח!";
                tm?.NotifyTravellerPickedKey();
                break;

            case PickupType.Lifebuoy:
                gm.lifebuoys++;
                if (string.IsNullOrEmpty(finalMessage))
                    finalMessage = "אספת מצוף הצלה!";
                break;

            case PickupType.Bomb:
                gm.lives--;
                tm?.NotifyTravellerSteppedBomb();

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

                    // ✅ עושים אפקט+טלפורט אצל ה-Owner של השחקן (כי יש ClientNetworkTransform)
                    var move = other.GetComponentInParent<PlayerMovement1P>();
                    var playerNetObj = other.GetComponentInParent<NetworkObject>();

                    if (move != null && playerNetObj != null)
                    {
                        var p = new ClientRpcParams
                        {
                            Send = new ClientRpcSendParams
                            {
                                TargetClientIds = new ulong[] { playerNetObj.OwnerClientId },
                            },
                        };

                        // ✅ חזרה ל-(1,1,1) ב-World Space
                        move.BombResetAndTeleportClientRpc(
                            new Vector3(1f, 1f, 1f),
                            Quaternion.identity,
                            bombPreTeleportDelay,
                            bombRedSeconds,
                            bombFadeOut,
                            bombFadeIn,
                            p
                        );
                    }
                }
                break;
        }

        // שולחים ל־Clients לעדכן HUD ולהציג הודעה
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

        // השמדת האובייקט מהרשת (עכשיו בטוח – הטלפורט כבר "יושב" על השחקן ולא על הפצצה)
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
        bool gameOver
    )
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
