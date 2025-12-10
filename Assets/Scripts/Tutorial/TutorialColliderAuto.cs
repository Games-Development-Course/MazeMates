using UnityEngine;

public class TutorialColliderAuto : MonoBehaviour
{
    public string disableOnStepId;
    public Collider targetCollider;

    private void Awake()
    {
        TutorialManager.RegisterAutoCollider(this);

        if (targetCollider == null)
            targetCollider = GetComponent<Collider>();

        if (targetCollider == null)
        {
            Debug.LogWarning($"[TutorialColliderAuto] No collider found on {name}");
            return;
        }

        // ⭐ פתרון קסם:
        // אם הטוטוריאל כבר נמצא בשלב שבו צריך לכבות את הקוליידר —
        // נכבה אותו מיד. בלי להסתמך על RPC.
        var tm = Object.FindFirstObjectByType<TutorialManager>();

        if (tm != null && tm.IsTutorialRunningForStep(disableOnStepId))
        {
            DisableCollider();
        }
    }

    public void OnStepStarted(string currentStepId)
    {
        if (!string.IsNullOrEmpty(disableOnStepId) &&
            currentStepId == disableOnStepId)
        {
            DisableCollider();
        }
    }

    public void DisableCollider()
    {
        if (targetCollider != null && targetCollider.enabled)
        {
            targetCollider.enabled = false;
            Debug.Log($"[TutorialColliderAuto] Disabled collider on {targetCollider.gameObject.name}");
        }
    }
}
