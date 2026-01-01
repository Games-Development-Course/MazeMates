// PickupObject.cs (patch)
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PickupObject : NetworkBehaviour
{
    public enum PickupType { Heart, Key, Bomb, Lifebuoy }
    public PickupType type;

    [Header("Tutorial Override")]
    [SerializeField] private string tutorialSceneName = "TutorialScene";
    [SerializeField] private Transform tutorialTravellerRespawnPoint; // assign in TutorialScene

    [Header("Custom Message Settings")]
    [TextArea(2, 5)] public string customMessage = "";
    public Color messageColor = Color.white;
    public TMP_FontAsset messageFont;

    [Header("Message Duration")]
    public float messageDuration = 1.5f;

    [Header("Bomb Reset Visuals")]
    public float bombPreTeleportDelay = 0.25f;
    public float bombRedSeconds = 0.15f;
    public float bombFadeOut = 0.25f;
    public float bombFadeIn = 0.35f;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Player")) return;

        HUDManager hud = HUDManager.Instance;
        GameManager gm = GameManager.Instance;
        if (gm == null || hud == null) return;

        string finalMessage = customMessage;
        bool gameOver = false;

        var tm = Object.FindFirstObjectByType<TutorialManager>();

        switch (type)
        {
            case PickupType.Heart:
                gm.lives++;
                if (string.IsNullOrEmpty(finalMessage)) finalMessage = "אספת לב! קיבלת חיים נוספים.";
                tm?.NotifyTravellerPickedHeart();
                break;

            case PickupType.Key:
                gm.keys++;
                if (string.IsNullOrEmpty(finalMessage)) finalMessage = "אספת מפתח!";
                tm?.NotifyTravellerPickedKey();
                break;

            case PickupType.Lifebuoy:
                gm.lifebuoys++;
                if (string.IsNullOrEmpty(finalMessage)) finalMessage = "אספת מצוף הצלה!";
                break;

            case PickupType.Bomb:
                gm.lives--;
                tm?.NotifyTravellerSteppedBomb();

                if (string.IsNullOrEmpty(finalMessage)) finalMessage = "דרכת על פצצה! איבדת לב.";

                hud.FlashTravellerLife();

                bool isTutorial = SceneManager.GetActiveScene().name == tutorialSceneName
                                  && tm != null && tm.IsSpawned && tm.TutorialActive.Value;

                if (gm.lives <= 0)
                {
                    if (isTutorial)
                    {
                        // Tutorial rule: never game over, just respawn (and keep at least 1 life)
                        gm.lives = 1;
                        TryBombResetTeleport(other, GetTutorialRespawnPos(), GetTutorialRespawnRot());
                    }
                    else
                    {
                        gameOver = true;
                    }
                }
                else
                {
                    TryBombResetTeleport(other, new Vector3(1f, 1f, 1f), Quaternion.identity);
                }

                break;
        }

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

        var netObj = GetComponent<NetworkObject>();
        if (netObj != null) netObj.Despawn(true);
        else Destroy(gameObject);
    }

    private Vector3 GetTutorialRespawnPos()
        => tutorialTravellerRespawnPoint != null ? tutorialTravellerRespawnPoint.position : new Vector3(1f, 1f, 1f);

    private Quaternion GetTutorialRespawnRot()
        => tutorialTravellerRespawnPoint != null ? tutorialTravellerRespawnPoint.rotation : Quaternion.identity;

    private void TryBombResetTeleport(Collider other, Vector3 pos, Quaternion rot)
    {
        var cam = other.GetComponentInChildren<PlayerCamera1P>();
        if (cam != null) cam.LockCameraForSeconds(0.5f);

        var move = other.GetComponentInParent<PlayerMovement1P>();
        var playerNetObj = other.GetComponentInParent<NetworkObject>();
        if (move == null || playerNetObj == null) return;

        var p = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { playerNetObj.OwnerClientId },
            },
        };

        move.BombResetAndTeleportClientRpc(
            pos,
            rot,
            bombPreTeleportDelay,
            bombRedSeconds,
            bombFadeOut,
            bombFadeIn,
            p
        );
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
        bool gameOver
    )
    {
        HUDManager hud = HUDManager.Instance;
        GameManager gm = GameManager.Instance;
        if (hud == null || gm == null) return;

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
            NetworkManager.Singleton.SceneManager.LoadScene("GameOver", LoadSceneMode.Single);
    }
}
