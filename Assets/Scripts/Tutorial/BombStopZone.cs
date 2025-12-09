using UnityEngine;

public class BombStopZone : MonoBehaviour
{
    private BombStepHelper helper;
    private TutorialManager tutorial;

    private void Awake()
    {
        helper = FindAnyObjectByType<BombStepHelper>();
        tutorial = FindAnyObjectByType<TutorialManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // לוודא שזה המטייל
        if (other.gameObject != gm.traveller)
            return;

        Debug.Log("[BombStopZone] Traveller reached bomb trigger");

        // כבר לא צריך את הקוליידר של שלב 4-1
        helper?.DisableTargetCollider();

        // 🔑 זה מה שסוגר את Step 4-1
        // אם conditionType של Step 4-1 = CustomEvent
        tutorial?.NotifyCustomEvent();
    }
}
