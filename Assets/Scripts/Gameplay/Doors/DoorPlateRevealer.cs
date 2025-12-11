using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class VictoryPlateRevealer : NetworkBehaviour
{
    [Header("Plate Settings")]
    public Transform plate;      // הפלטה עצמה
    public float riseAmount = 0.4f;
    public float riseSpeed = 2f;

    private Vector3 startPos;
    private Vector3 targetPos;

    private bool isRaised = false;

    private void Awake()
    {
        startPos = plate.localPosition;
        targetPos = startPos + new Vector3(0, riseAmount, 0);
    }

    // פונקציה שתקרא מה-ExitDoor כשהמטייל נכנס
    public void RaisePlate()
    {
        if (isRaised) return;
        isRaised = true;

        // קריאה לשרת כדי שיעשה RPC לכולם
        RaisePlateServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RaisePlateServerRpc()
    {
        // עדכן אצל כולם
        RaisePlateClientRpc();
    }

    [ClientRpc]
    private void RaisePlateClientRpc()
    {
        StopAllCoroutines();
        StartCoroutine(RaiseAnimation());
    }

    private IEnumerator RaiseAnimation()
    {
        Vector3 from = plate.localPosition;
        Vector3 to = targetPos;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * riseSpeed;
            plate.localPosition = Vector3.Lerp(from, to, t);
            yield return null;
        }

        plate.localPosition = to;
    }
}
