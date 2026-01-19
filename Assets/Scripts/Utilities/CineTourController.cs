using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class CineTourController : MonoBehaviour
{
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private List<Transform> waypoints = new();
    [SerializeField] private bool loop = false;

    void Start()
    {
        RebuildSpline();
    }

    [ContextMenu("Rebuild Spline Now")]
    public void RebuildSpline()
    {
        if (!splineContainer)
        {
            Debug.LogError("CineTourController: Missing SplineContainer.");
            return;
        }

        if (waypoints == null || waypoints.Count < 2)
        {
            Debug.LogError("CineTourController: Need at least 2 waypoints.");
            return;
        }

        var spline = new Spline { Closed = loop };

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 localPos = splineContainer.transform.InverseTransformPoint(waypoints[i].position);
            spline.Add(new BezierKnot(localPos));
        }

        splineContainer.Spline = spline;

        Debug.Log($"CineTourController: Rebuilt spline with {waypoints.Count} knots.");
    }
}
