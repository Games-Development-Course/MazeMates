using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Unity.Cinemachine;
using System.Collections.Generic;

public class CineSplineTCache : MonoBehaviour
{
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private List<Transform> waypoints = new();

    public List<float> CachedT { get; private set; } = new();

    [ContextMenu("Cache T From Waypoints")]
    public void CacheT()
    {
        CachedT.Clear();
        if (!splineContainer || waypoints.Count < 2) return;

        var spline = splineContainer.Spline;

        for (int i = 0; i < waypoints.Count; i++)
        {
            float3 local = splineContainer.transform.InverseTransformPoint(waypoints[i].position);

            // Finds nearest point on spline and returns its t (0..1)
            SplineUtility.GetNearestPoint(spline, local, out float3 nearest, out float t);
            CachedT.Add(t);
        }

        Debug.Log($"Cached {CachedT.Count} T values from waypoints.");
    }
}
