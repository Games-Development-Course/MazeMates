using UnityEngine;

public class TutorialColliderAuto : MonoBehaviour
{
    public string disableOnStepId;
    public Collider targetCollider;

    private bool hasDisabled = false;

    private void Awake()
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[AUTO][Awake] Register collider on {name} | stepId={disableOnStepId}"
        );

        TutorialManager.RegisterAutoCollider(this);

        if (targetCollider == null)
            targetCollider = GetComponent<Collider>();

        if (targetCollider == null)
        {
            Debug.LogFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                null,
                $"[AUTO] No collider found on {name}"
            );
            return;
        }

        var tm = Object.FindFirstObjectByType<TutorialManager>();

        if (tm == null)
        {
            Debug.LogFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                null,
                $"[AUTO] TutorialManager NOT FOUND in Awake on {name}"
            );
            return;
        }

        bool isRunning = tm.IsTutorialRunningForStep(disableOnStepId);

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[AUTO][Awake] {name}: IsTutorialRunningForStep({disableOnStepId}) = {isRunning}"
        );

        if (isRunning)
        {
            DisableCollider();
        }
    }

    public void OnStepStarted(string currentStepId)
    {
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[AUTO][OnStepStarted] {name}: step started={currentStepId} | myStep={disableOnStepId}"
        );

        if (!string.IsNullOrEmpty(disableOnStepId) && currentStepId == disableOnStepId)
        {
            DisableCollider();
        }
    }

    public void DisableCollider()
    {
        if (targetCollider == null)
        {
            Debug.LogFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                null,
                $"[AUTO] DisableCollider FAILED on {name}: collider=null"
            );
            return;
        }

        if (hasDisabled)
        {
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                $"[AUTO] {name}: collider already disabled previously."
            );
            return;
        }

        targetCollider.enabled = false;
        hasDisabled = true;

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            $"[AUTO] Disabled collider on {targetCollider.gameObject.name}"
        );
    }
}
