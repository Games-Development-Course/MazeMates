using UnityEngine;

public class DisableColliderOnStepStart : MonoBehaviour
{
    public Collider targetCollider;

    public void DisableNow()
    {
        if (targetCollider != null)
            targetCollider.enabled = false;
    }
}
