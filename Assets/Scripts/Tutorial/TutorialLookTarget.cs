// TutorialLookTarget.cs
using System.Collections.Generic;
using UnityEngine;

public class TutorialLookTarget : MonoBehaviour
{
    [Tooltip("����� ���� ��-TutorialStep ����� �� (����: DoorLookTarget)")]
    public string targetId;

    private static readonly Dictionary<string, TutorialLookTarget> registry =
        new Dictionary<string, TutorialLookTarget>();

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(targetId))
            return;

        registry[targetId] = this;
    }

    private void OnDisable()
    {
        if (string.IsNullOrEmpty(targetId))
            return;

        if (registry.TryGetValue(targetId, out var t) && t == this)
            registry.Remove(targetId);
    }

    public static Transform Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        return registry.TryGetValue(id, out var t) ? t.transform : null;
    }
}
