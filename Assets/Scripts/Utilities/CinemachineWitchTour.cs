using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Cinemachine;

[ExecuteAlways]
public class CinemachineWitchTour : MonoBehaviour
{
    [System.Serializable]
    public class Waypoint
    {
        public Transform point;

        [Header("Timing")]
        public float holdSeconds = 0.5f;
        public float travelToNextSeconds = 1.8f;

        [Header("Insert TopDown Before Next")]
        public bool goViaTopDownBeforeNext = false;
        public float travelToTopDownSeconds = 1.2f;
        public float travelFromTopDownToNextSeconds = 1.2f;
    }

    [Header("VCams")]
    [SerializeField] private CinemachineCamera vcamTopDownHold;
    [SerializeField] private CinemachineCamera vcamWitchFly;

    [Header("Initial Hold (TopDown at start)")]
    [SerializeField] private float initialHoldSeconds = 3f;

    [Header("Spline + Dolly (Fly Cam)")]
    [SerializeField] private SplineContainer splineContainer;     // Dolly Spline
    [SerializeField] private CinemachineSplineDolly splineDolly;  // component on FlyingCamera

    [Header("TopDown Pivot (REAL route knot)")]
    [SerializeField] private Transform topDownPivot;
    [SerializeField] private float topDownHoldSeconds = 1.0f;

    [Header("Waypoints (base list)")]
    [SerializeField] private List<Waypoint> waypoints = new();

    [Header("Rotation Target (usually FlyingCamera transform)")]
    [SerializeField] private Transform rotationTarget;

    [Header("Easing")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Route Build Options")]
    [SerializeField] private bool rebuildOnValidate = true;
    [SerializeField] private bool linearSpline = true;
    [SerializeField] private bool debugLogRoute = false;
    [SerializeField] private bool drawRouteGizmos = true;

    [Header("Rotation Blend Mode")]
    [Tooltip("If ON: blends waypoint rotations using LerpAngle on Euler (guarantees reaching EXACT target at end, prevents sudden Y cuts).")]
    [SerializeField] private bool useEulerAngleBlend = true;

    private readonly List<RouteNode> route = new();
    private readonly List<float> routeT = new();
    private Coroutine tourRoutine;

    private struct RouteNode
    {
        public Transform tr;
        public float hold;
        public float travelToNext;
    }

    void Awake()
    {
        if (!rotationTarget && vcamWitchFly)
            rotationTarget = vcamWitchFly.transform;
    }

    void OnEnable()
    {
        if (!Application.isPlaying && rebuildOnValidate)
            RebuildRouteSpline();
    }

    void OnValidate()
    {
        if (!rebuildOnValidate) return;
        if (Application.isPlaying) return;
        RebuildRouteSpline();
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            RebuildRouteSpline();
            StartTour();
        }
    }

    public void StartTour()
    {
        if (tourRoutine != null) StopCoroutine(tourRoutine);
        tourRoutine = StartCoroutine(RunTour());
    }

    [ContextMenu("Rebuild Route Spline")]
    public void RebuildRouteSpline()
    {
        route.Clear();
        routeT.Clear();

        if (!splineContainer || !topDownPivot || waypoints == null || waypoints.Count < 2)
            return;

        // ---- Build ROUTE (with real TopDownPivot insertions) ----
        AddRouteNode(waypoints[0].point, waypoints[0].holdSeconds, waypoints[0].travelToNextSeconds);

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            var cur = waypoints[i];
            var next = waypoints[i + 1];

            if (!cur.point || !next.point) return;

            if (cur.goViaTopDownBeforeNext)
            {
                // current -> topdown
                SetLastTravel(cur.travelToTopDownSeconds);

                // insert TopDown as REAL route node
                AddRouteNode(topDownPivot, topDownHoldSeconds, cur.travelFromTopDownToNextSeconds);

                // then topdown -> next
                AddRouteNode(next.point, next.holdSeconds, next.travelToNextSeconds);
            }
            else
            {
                SetLastTravel(cur.travelToNextSeconds);
                AddRouteNode(next.point, next.holdSeconds, next.travelToNextSeconds);
            }
        }

        // ---- Build spline from ROUTE positions ----
        var spline = new Spline();

        for (int i = 0; i < route.Count; i++)
        {
            Vector3 localPos = splineContainer.transform.InverseTransformPoint(route[i].tr.position);

            if (linearSpline)
                spline.Add(new BezierKnot(localPos, Vector3.zero, Vector3.zero, Quaternion.identity));
            else
                spline.Add(new BezierKnot(localPos));
        }

        spline.Closed = false;
        splineContainer.Spline = spline;

        // ---- Compute normalized t per route node (by cumulative segment lengths) ----
        float total = 0f;
        List<float> cum = new(route.Count) { 0f };

        for (int i = 1; i < route.Count; i++)
        {
            float d = Vector3.Distance(route[i - 1].tr.position, route[i].tr.position);
            total += d;
            cum.Add(total);
        }

        if (total < 0.0001f)
        {
            for (int i = 0; i < route.Count; i++) routeT.Add(0f);
        }
        else
        {
            for (int i = 0; i < route.Count; i++) routeT.Add(cum[i] / total);
        }

        if (debugLogRoute)
        {
            string s = $"[WitchTour] RouteNodes={route.Count}\n";
            for (int i = 0; i < route.Count; i++)
                s += $"{i:00}  {route[i].tr.name}  t={routeT[i]:F3}\n";
            Debug.Log(s);
        }
    }

    private void AddRouteNode(Transform tr, float hold, float travelToNext)
    {
        if (!tr) return;

        route.Add(new RouteNode
        {
            tr = tr,
            hold = Mathf.Max(0f, hold),
            travelToNext = Mathf.Max(0.2f, travelToNext)
        });
    }

    private void SetLastTravel(float seconds)
    {
        if (route.Count == 0) return;
        var last = route[route.Count - 1];
        last.travelToNext = Mathf.Max(0.2f, seconds);
        route[route.Count - 1] = last;
    }

    private IEnumerator RunTour()
    {
        SetPriority(vcamTopDownHold, 20);
        SetPriority(vcamWitchFly, 0);

        yield return new WaitForSeconds(initialHoldSeconds);

        SetPriority(vcamTopDownHold, 0);
        SetPriority(vcamWitchFly, 20);

        if (!splineDolly || route.Count < 2 || routeT.Count != route.Count)
            yield break;

        // Snap to first node
        splineDolly.CameraPosition = routeT[0];
        SetExactRotation(route[0].tr.rotation);

        for (int i = 0; i < route.Count; i++)
        {
            if (route[i].hold > 0f)
                yield return new WaitForSeconds(route[i].hold);

            if (i >= route.Count - 1) break;

            yield return TravelSegment(
                routeT[i], routeT[i + 1],
                route[i].tr.rotation, route[i + 1].tr.rotation,
                route[i].travelToNext
            );

            // Snap exact rotation at arrival (should already match now)
            SetExactRotation(route[i + 1].tr.rotation);
        }
    }

    private IEnumerator TravelSegment(float tA, float tB, Quaternion rotA, Quaternion rotB, float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            float u = Mathf.Clamp01(elapsed / seconds);
            float eu = ease.Evaluate(u);

            splineDolly.CameraPosition = Mathf.Lerp(tA, tB, eu);
            SetExactRotation(BlendWaypointRotation(rotA, rotB, eu));

            elapsed += Time.deltaTime;
            yield return null;
        }

        splineDolly.CameraPosition = tB;
        SetExactRotation(rotB);
    }

    private Quaternion BlendWaypointRotation(Quaternion a, Quaternion b, float t)
    {
        if (!useEulerAngleBlend)
            return Quaternion.Slerp(a, b, t);

        // Euler blend with LerpAngle guarantees reaching EXACT target at t=1
        Vector3 ea = a.eulerAngles;
        Vector3 eb = b.eulerAngles;

        float x = Mathf.LerpAngle(ea.x, eb.x, t);
        float y = Mathf.LerpAngle(ea.y, eb.y, t);
        float z = Mathf.LerpAngle(ea.z, eb.z, t);

        return Quaternion.Euler(x, y, z);
    }

    private void SetExactRotation(Quaternion q)
    {
        if (!rotationTarget) return;
        rotationTarget.rotation = q;
    }

    private void SetPriority(CinemachineCamera cam, int p)
    {
        if (cam) cam.Priority = p;
    }

    void OnDrawGizmos()
    {
        if (!drawRouteGizmos) return;
        if (route.Count < 2) return;

        for (int i = 0; i < route.Count; i++)
        {
            if (!route[i].tr) continue;

            Gizmos.DrawSphere(route[i].tr.position, 0.25f);
            if (i < route.Count - 1 && route[i + 1].tr)
                Gizmos.DrawLine(route[i].tr.position, route[i + 1].tr.position);
        }
    }
}
