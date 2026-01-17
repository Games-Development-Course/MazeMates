using System.Collections.Generic;
using UnityEngine;

public class OccluderFadeController : MonoBehaviour
{
    [Header("Target (CamAim / head)")]
    [SerializeField] private Transform target; // גרור לפה את CamAim של ה-Traveller

    [Header("Which layers can fade (Walls/Doors)")]
    [SerializeField] private LayerMask occluderLayers;

    [Header("Cast")]
    [SerializeField] private float sphereRadius = 0.08f;

    [Header("Fade")]
    [Range(0f, 1f)]
    [SerializeField] private float occludedFade = 0.2f; // כמה "נעלם" כשחוסם
    [SerializeField] private float fadeSpeed = 10f;

    private static readonly int FadeID = Shader.PropertyToID("_Fade");

    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly HashSet<Renderer> blockedThisFrame = new();

    private class State
    {
        public float fade = 1f;
        public MaterialPropertyBlock mpb = new();
    }

    private readonly Dictionary<Renderer, State> states = new();

    void LateUpdate()
    {
        if (!target) return;

        blockedThisFrame.Clear();

        Vector3 from = transform.position;
        Vector3 to = target.position;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.001f) return;
        dir /= dist;

        int count = Physics.SphereCastNonAlloc(
            from, sphereRadius, dir, hits, dist,
            occluderLayers, QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            var col = hits[i].collider;
            if (!col) continue;

            // חשוב למודולרים: לקחת Renderer על אותו אובייקט שנפגע
            var rend = col.GetComponent<Renderer>();
            if (!rend) continue;

            blockedThisFrame.Add(rend);
            Ensure(rend);
            MoveFade(rend, occludedFade);
        }

        // כל מי שהיה בפייד בעבר ולא חסום עכשיו -> חוזר ל-1
        foreach (var kv in states)
        {
            var rend = kv.Key;
            if (!rend) continue;

            if (!blockedThisFrame.Contains(rend))
                MoveFade(rend, 1f);
        }
    }

    private void Ensure(Renderer rend)
    {
        if (states.ContainsKey(rend)) return;
        states[rend] = new State();
    }

    private void MoveFade(Renderer rend, float targetFade)
    {
        var st = states[rend];
        st.fade = Mathf.MoveTowards(st.fade, targetFade, fadeSpeed * Time.deltaTime);

        rend.GetPropertyBlock(st.mpb);
        st.mpb.SetFloat(FadeID, st.fade);
        rend.SetPropertyBlock(st.mpb);
    }
}
