using UnityEngine;

public class BombStepTargetCollider : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (other.gameObject != gm.traveller)
            return;

        var tutorial = FindFirstObjectByType<TutorialManager>();
        tutorial?.NotifyCustomEvent(); // מסמן שהמטייל הגיע
    }
}
