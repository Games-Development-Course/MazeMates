using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.SceneManagement;

public class PickupObject: MonoBehaviour
{
    public enum PickupType { Heart, Key, Bomb }
    public PickupType type;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

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
                    // הודעה כמה חיים נשארו
                    HUD.Instance.ShowMessage("נשארו לכם " + GameManager.Instance.lives + " חיים, חזרתם לנקודת ההתחלה, נסו שוב :)");

                    //  נועל את המצלמה שוב למשך אותו מספר שניות כמו בתחילת המשחק
                    other.GetComponentInChildren<PlayerCamera1P>()
                         .LockCameraForSeconds(2f); // לשים את אותו מספר שניות כמו במשחק שלך

                    // שולח את השחקן לנקודת ההתחלה
                    other.GetComponent<PlayerMovement1P>()
                         .TeleportToStart(PlayerStartPoint.Instance.startPosition);
                }
                break;


        }

        Destroy(gameObject);
    }
}
