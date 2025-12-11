using UnityEngine;
using System.Collections;

public class TutorialColliderAuto : MonoBehaviour
{
    public string disableOnStepId;
    public Collider targetCollider;

    private bool hasDisabled = false;

    private IEnumerator Start()
    {
        Debug.Log($"[AUTO][Start] Register collider on {name} | stepId={disableOnStepId}");

        TutorialManager.RegisterAutoCollider(this);

        if (targetCollider == null)
            targetCollider = GetComponent<Collider>();

        if (targetCollider == null)
        {
            Debug.LogWarning($"[AUTO] No collider found on {name}");
            yield break;
        }

        // ⛔ מחכים עד שה-TutorialManager קיים
        while (TutorialManager.Instance == null)
            yield return null;

        // ⛔ מחכים עד שה-TutorialManager עבר Spawned() (Object.IsValid == true)
        while (!TutorialManager.Instance.Object || !TutorialManager.Instance.Object.IsValid)
            yield return null;

        // ⛔ עכשיו מותר לקרוא Networked properly
        bool isRunning = TutorialManager.Instance.IsTutorialRunningForStep(disableOnStepId);

        Debug.Log($"[AUTO][Start] {name}: IsTutorialRunningForStep({disableOnStepId}) = {isRunning}");

        if (isRunning)
            DisableCollider();
    }

    // נקרא מה-RPC כששלב מתחיל
    public void OnStepStarted(string currentStepId)
    {
        Debug.Log($"[AUTO][OnStepStarted] {name}: step started={currentStepId} | myStep={disableOnStepId}");

        if (!string.IsNullOrEmpty(disableOnStepId) &&
            currentStepId == disableOnStepId)
        {
            DisableCollider();
        }
    }

    public void DisableCollider()
    {
        if (targetCollider == null)
        {
            Debug.LogWarning($"[AUTO] DisableCollider FAILED on {name}: collider=null");
            return;
        }

        if (hasDisabled)
        {
            Debug.Log($"[AUTO] {name}: collider already disabled previously.");
            return;
        }

        targetCollider.enabled = false;
        hasDisabled = true;

        Debug.Log($"[AUTO] Disabled collider on {targetCollider.gameObject.name}");
    }
}
