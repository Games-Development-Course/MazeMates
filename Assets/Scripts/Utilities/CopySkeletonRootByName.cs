using System.Collections.Generic;
using UnityEngine;

public sealed class CopySkeletonPoseByName : MonoBehaviour
{
    [Header("Copy FROM this (the animated rig)")]
    [SerializeField] private Transform sourceRigRoot;   // Skeleton.001

    [Header("Copy TO this (the internal rig of the skin)")]
    [SerializeField] private Transform targetRigRoot;   // Skeleton inside Skin2 (or the whole Skin2 Skeleton)

    private readonly Dictionary<string, Transform> _srcByName = new();
    private readonly List<(Transform src, Transform dst)> _pairs = new();

    private void Awake()
    {
        BuildMap();
    }

    private void OnValidate()
    {
        if (sourceRigRoot && targetRigRoot)
            BuildMap();
    }

    private void BuildMap()
    {
        _srcByName.Clear();
        _pairs.Clear();

        if (!sourceRigRoot || !targetRigRoot) return;

        foreach (var t in sourceRigRoot.GetComponentsInChildren<Transform>(true))
            if (!_srcByName.ContainsKey(t.name))
                _srcByName.Add(t.name, t);

        foreach (var dst in targetRigRoot.GetComponentsInChildren<Transform>(true))
        {
            if (_srcByName.TryGetValue(dst.name, out var src))
                _pairs.Add((src, dst));
        }

        Debug.Log($"[CopySkeletonPoseByName] pairs={_pairs.Count} srcRoot={sourceRigRoot.name} dstRoot={targetRigRoot.name}");
    }

    private void LateUpdate()
    {
        // LateUpdate כדי לנצח את האנימטור של המקור
        for (int i = 0; i < _pairs.Count; i++)
        {
            var (src, dst) = _pairs[i];

            // מעתיקים LOCAL כדי שזה יתאים להיררכיה
            dst.localPosition = src.localPosition;
            dst.localRotation = src.localRotation;
            dst.localScale = src.localScale;
        }
    }
}
