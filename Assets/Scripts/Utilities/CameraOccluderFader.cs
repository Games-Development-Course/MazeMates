using System.Collections.Generic;
using UnityEngine;

public class CameraOccluderFader : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform target;          // CamAim של השחקן
    [SerializeField] private LayerMask occluderLayers;  // Walls / Doors

    [Header("Cast")]
    [Tooltip("רדיוס ביחס ל'עובי' רצוי של בדיקת חסימה. לרוב 0.05-0.12 ביחידות עולם.")]
    [SerializeField] private float sphereRadius = 0.08f;

    [Header("Fade")]
    [Range(0f, 1f)]
    [SerializeField] private float occludedAlpha = 0.2f;
    [SerializeField] private float fadeSpeed = 8f;

    // Internal
    private readonly Dictionary<Renderer, MaterialPropertyBlock> _blocks = new();
    private readonly Dictionary<Renderer, float> _currentAlpha = new();
    private readonly HashSet<Renderer> _hitThisFrame = new();

    // property names (URP Lit uses "_BaseColor", Standard uses "_Color")
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    void LateUpdate()
    {
        if (!target) return;

        _hitThisFrame.Clear();

        Vector3 from = transform.position;
        Vector3 to = target.position;
        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        if (dist < 0.001f) return;
        dir /= dist;

        // SphereCastAll: כל מה שחוסם בין המצלמה ליעד
        RaycastHit[] hits = Physics.SphereCastAll(from, sphereRadius, dir, dist, occluderLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var rend = hits[i].collider.GetComponentInParent<Renderer>();
            if (!rend) continue;

            _hitThisFrame.Add(rend);
            SetTargetAlpha(rend, occludedAlpha);
        }

        // כל מה שהיה מוסתר בעבר אבל לא נחסם עכשיו -> החזרה לאט ל-1
        var keys = new List<Renderer>(_currentAlpha.Keys);
        foreach (var rend in keys)
        {
            if (!rend) { _currentAlpha.Remove(rend); continue; }

            if (!_hitThisFrame.Contains(rend))
                SetTargetAlpha(rend, 1f);
        }
    }

    private void SetTargetAlpha(Renderer rend, float targetAlpha)
    {
        if (!_blocks.TryGetValue(rend, out var block))
        {
            block = new MaterialPropertyBlock();
            _blocks[rend] = block;
        }

        float cur = _currentAlpha.TryGetValue(rend, out var a) ? a : 1f;
        float next = Mathf.MoveTowards(cur, targetAlpha, fadeSpeed * Time.deltaTime);
        _currentAlpha[rend] = next;

        // קרא את הצבע המקורי מהחומר הראשון
        // (אם יש כמה חומרים, אפשר להרחיב, אבל לרוב מספיק להתחלה)
        var mat = rend.sharedMaterial;
        if (!mat) return;

        Color c;
        if (mat.HasProperty(BaseColorID)) c = mat.GetColor(BaseColorID);
        else if (mat.HasProperty(ColorID)) c = mat.GetColor(ColorID);
        else return;

        c.a = next;

        rend.GetPropertyBlock(block);
        if (mat.HasProperty(BaseColorID)) block.SetColor(BaseColorID, c);
        else block.SetColor(ColorID, c);
        rend.SetPropertyBlock(block);
    }
}
