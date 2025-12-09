using UnityEngine;

public class DisableColliderOnStepStart : MonoBehaviour
{
    [Header("The collider that should be disabled")]
    public Collider targetCollider;

    public void Disable()
    {
        if (targetCollider != null)
            targetCollider.enabled = false;
        else
            Debug.LogWarning("DisableColliderOnStepStart: No collider assigned.");
    }
}
