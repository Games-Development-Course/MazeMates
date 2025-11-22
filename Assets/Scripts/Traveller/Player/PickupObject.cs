using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class PickupObject : MonoBehaviour
{
    public enum PickupType { Heart, Key, Bomb }
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

        // -----------------------------
        // שלב 1 – עדכון צבע ופונט בפועל
        // -----------------------------
        if (HUD.Instance != null)
        {
            // פונט
            if (messageFont != null)
                HUD.Instance.messageText.font = messageFont;

            // צבע בסיס (Vertex color)
            HUD.Instance.messageText.color = messageColor;

            // שינוי צבע אמיתי בחומר (FaceColor)
            var mat = HUD.Instance.messageText.fontMaterial;

            if (mat.HasProperty("_FaceColor"))
                mat.SetColor("_FaceColor", messageColor);

            // אם לא רוצים outline
            if (mat.HasProperty("_OutlineColor"))
                mat.SetColor("_OutlineColor", Color.clear);

            // הצגת הודעה
            if (!string.IsNullOrEmpty(customMessage))
                HUD.Instance.ShowMessage(customMessage);
        }

        // -----------------------------
        // שלב 2 – לוגיקה לפי סוג האובייקט
        // -----------------------------
        switch (type)
        {
            case PickupType.Heart:
                GameManager.Instance.lives++;
                HUD.Instance.UpdateHUD();
                break;

            case PickupType.Key:
                GameManager.Instance.keys++;
                HUD.Instance.UpdateHUD();
                break;

            case PickupType.Bomb:
                GameManager.Instance.lives--;
                HUD.Instance.UpdateHUD();
                HUD.Instance.FlashLifeIcon();

                if (GameManager.Instance.lives <= 0)
                {
                    SceneManager.LoadScene("GameOver");
                }
                else
                {
                    // נועל מצלמה
                    other.GetComponentInChildren<PlayerCamera1P>()
                        ?.LockCameraForSeconds(2f);

                    // שולח לנקודת התחלה
                    other.GetComponent<PlayerMovement1P>()
                         ?.TeleportToStart(PlayerStartPoint.Instance.startPosition);
                }
                break;
        }

        Destroy(gameObject);
    }
}
