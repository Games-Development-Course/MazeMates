// VictoryPlateRevealer.cs
using System.Collections;
using Fusion;
using UnityEngine;

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
        if (plate == null)
            plate = transform;

        startPos = plate.localPosition;
        targetPos = startPos + new Vector3(0f, riseAmount, 0f);
    }

    /// <summary>
    /// נקרא מה-ExitDoor כשהמטייל נכנס לפלטת הניצחון.
    /// אפשר לקרוא מכל קליינט – ה-StateAuthority ייענה ויעדכן את כולם.
    /// </summary>
    public void RaisePlate()
    {
        if (isRaised)
            return;

        RequestRaisePlateRpc();
    }

    /// <summary>
    /// בקשה להרים את הפלטה – נשלחת מכל שחקן, מבוצעת רק על StateAuthority.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestRaisePlateRpc(RpcInfo info = default)
    {
        if (isRaised)
            return;

        isRaised = true;
        RaisePlateRpc();
    }

    /// <summary>
    /// RPC מה-StateAuthority לכל הקליינטים כדי להריץ אנימציית הרמה מקומית.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RaisePlateRpc(RpcInfo info = default)
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

        var glow = plate.GetComponent<FloorPressurePlateGlow>();
        if (glow != null)
            glow.RefreshStartPosition();
    }
}
