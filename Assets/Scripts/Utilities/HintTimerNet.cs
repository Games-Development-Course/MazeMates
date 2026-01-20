using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class HintTimerNet : NetworkBehaviour
{
    [SerializeField] private float hintDelaySeconds = 10f;
    private Coroutine co;

    // לקרוא לזה כשהחידה מתחילה (בשרת)
    public void StartPuzzleServer()
    {
        if (!IsServer) return;

        HintReadyClientRpc(false);
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(EnableHintAfterDelay());
    }

    // לקרוא לזה כשהחידה נגמרת/הדלת נפתחת (בשרת)
    public void EndPuzzleServer()
    {
        if (!IsServer) return;

        if (co != null) StopCoroutine(co);
        co = null;
        HintReadyClientRpc(false);
    }

    private IEnumerator EnableHintAfterDelay()
    {
        yield return new WaitForSeconds(hintDelaySeconds);
        HintReadyClientRpc(true);
    }

    [ClientRpc]
    private void HintReadyClientRpc(bool ready)
    {
        NavigatorSpotlights.I?.SetHintReady(ready);
    }
}
