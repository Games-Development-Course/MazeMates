using System.Collections.Generic;
using UnityEngine;

public class OcclusionFader: MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform target;          // השחקן
    [SerializeField] private GameObject occlusionQuad;  // ה-Quad על המצלמה

    [Header("Occlusion detection")]
    [SerializeField] private LayerMask wallsMask;       // רק Walls
    [SerializeField] private float castRadius = 0.18f;  // תגדיל אם מפספס קירות
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f); // גובה חזה

    [Header("Wall fade (optional)")]
    [SerializeField, Range(0f, 1f)] private float wallOccludedAlpha = 0.45f;
    [SerializeField] private float fadeSpeed = 10f;

    // renderer -> current alpha
    private readonly Dictionary<Renderer, float> current = new();
    private readonly HashSet<Renderer> hitThisFrame = new();

    void Awake()
    {
        if (occlusionQuad) occlusionQuad.SetActive(false);
    }

    void LateUpdate()
    {
        if (!target) return;

        hitThisFrame.Clear();

        Vector3 from = transform.position; // המצלמה (שים את הסקריפט על Main Camera)
        Vector3 to = target.position + targetOffset;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.001f) return;
        dir /= dist;

        // בדיקת חסימה בין המצלמה לשחקן
        var hits = Physics.SphereCastAll(from, castRadius, dir, dist, wallsMask, QueryTriggerInteraction.Ignore);

        bool occluded = hits != null && hits.Length > 0;

        // להדליק/לכבות את ה-Quad רק כשצריך
        if (occlusionQuad && occlusionQuad.activeSelf != occluded)
            occlusionQuad.SetActive(occluded);

        // לעשות Fade לקירות שנפגעו
        if (hits != null)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (!col) continue;

                var r = col.GetComponentInParent<Renderer>();
                if (!r) continue;

                hitThisFrame.Add(r);

                if (!current.ContainsKey(r))
                    current[r] = 1f;

                current[r] = Mathf.MoveTowards(current[r], wallOccludedAlpha, fadeSpeed * Time.deltaTime);
                SetRendererAlpha(r, current[r]);
            }
        }

        // להחזיר קירות שלא נחסמים כבר
        var keys = new List<Renderer>(current.Keys);
        foreach (var r in keys)
        {
            if (hitThisFrame.Contains(r)) continue;

            current[r] = Mathf.MoveTowards(current[r], 1f, fadeSpeed * Time.deltaTime);
            SetRendererAlpha(r, current[r]);

            if (Mathf.Approximately(current[r], 1f))
                current.Remove(r);
        }
    }

    private static void SetRendererAlpha(Renderer r, float a)
    {
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);

        // URP Lit
        if (r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseColor"))
        {
            Color c = mpb.GetColor("_BaseColor");
            if (c == default) c = r.sharedMaterial.GetColor("_BaseColor");
            c.a = a;
            mpb.SetColor("_BaseColor", c);
        }
        // Built-in Standard
        else if (r.sharedMaterial && r.sharedMaterial.HasProperty("_Color"))
        {
            Color c = mpb.GetColor("_Color");
            if (c == default) c = r.sharedMaterial.GetColor("_Color");
            c.a = a;
            mpb.SetColor("_Color", c);
        }

        r.SetPropertyBlock(mpb);
    }
}
