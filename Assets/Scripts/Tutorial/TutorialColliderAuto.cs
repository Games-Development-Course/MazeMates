using UnityEngine;

public class TutorialColliderAuto : MonoBehaviour
{
    [Tooltip("באיזה אינדקס שלב לכבות את הקוליידר? (Step0 = 0, Step1 = 1 וכו')")]
    public int disableOnStepIndex = -1;

    [Tooltip("אם ריק ניקח את הקוליידר שעל אותו אובייקט. אם תמלא – נכבה את מה שבחרת")]
    public Collider targetCollider;

    private void Awake()
    {
        // רישום ל-TutorialManager
        TutorialManager.RegisterAutoCollider(this);

        // אם לא גררו ידנית קוליידר – ננסה לקחת את זה שעל אותו אובייקט
        if (targetCollider == null)
            targetCollider = GetComponent<Collider>();

        if (targetCollider == null)
            Debug.LogWarning($"[TutorialColliderAuto] No collider found on {name}");
    }

    /// נקרא בתחילת כל שלב ע״י TutorialManager
    public void OnStepStarted(int currentStepIndex)
    {
        if (disableOnStepIndex >= 0 && currentStepIndex == disableOnStepIndex)
        {
            DisableCollider();
        }
    }

    public void DisableCollider()
    {
        if (targetCollider != null && targetCollider.enabled)
        {
            targetCollider.enabled = false;
            Debug.Log($"[TutorialColliderAuto] Disabled collider on {targetCollider.gameObject.name} (parent: {name})");
        }
    }
}
g