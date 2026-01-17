// Assets/Editor/RemapAllSkinnedMeshes.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class RemapAllSkinnedMeshes : EditorWindow
{
    private Transform skinRoot;     // Skin2 / Skin3 / Skin4
    private Transform targetRigRoot; // Skeleton.001 (או Root שלו)
    private bool zeroLocalTransform = false; // תדליק רק אם צריך

    [MenuItem("Tools/Remap ALL SMRs")]
    static void Open() => GetWindow<RemapAllSkinnedMeshes>("Remap ALL SMRs");

    private void OnGUI()
    {
        skinRoot = (Transform)EditorGUILayout.ObjectField(
            "Skin Root (Skin2/3/4)", skinRoot, typeof(Transform), true);

        targetRigRoot = (Transform)EditorGUILayout.ObjectField(
            "Target Rig Root (Skeleton.001)", targetRigRoot, typeof(Transform), true);

        zeroLocalTransform = EditorGUILayout.ToggleLeft(
            "Zero local transform of clothing objects (optional)", zeroLocalTransform);

        using (new EditorGUI.DisabledScope(skinRoot == null || targetRigRoot == null))
        {
            if (GUILayout.Button("Remap ALL SkinnedMeshRenderers"))
                RemapAll();
        }
    }

    private void RemapAll()
    {
        // build lookup of target bones by name
        var targetAll = targetRigRoot.GetComponentsInChildren<Transform>(true);
        var targetByName = targetAll
            .GroupBy(t => t.name)
            .ToDictionary(g => g.Key, g => g.First()); // first match (OK if rig has unique names)

        var smrs = skinRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs == null || smrs.Length == 0)
        {
            Debug.LogWarning($"[Remap] No SkinnedMeshRenderer found under '{skinRoot.name}'.");
            return;
        }

        int totalMissingBones = 0;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remap ALL SMRs");

        foreach (var smr in smrs)
        {
            if (smr == null) continue;

            Undo.RecordObject(smr, "Remap SMR bones");
            if (zeroLocalTransform)
            {
                Undo.RecordObject(smr.transform, "Zero clothing transform");
                smr.transform.localPosition = Vector3.zero;
                smr.transform.localRotation = Quaternion.identity;
                smr.transform.localScale = Vector3.one;
            }

            // 1) Remap bones array
            var oldBones = smr.bones;
            var newBones = new Transform[oldBones.Length];

            int missingHere = 0;

            for (int i = 0; i < oldBones.Length; i++)
            {
                var b = oldBones[i];
                if (b == null) { newBones[i] = null; continue; }

                if (targetByName.TryGetValue(b.name, out var match))
                    newBones[i] = match;
                else
                {
                    newBones[i] = b; // keep as-is
                    missingHere++;
                }
            }

            smr.bones = newBones;

            // 2) Remap rootBone by ORIGINAL rootBone name if possible
            if (smr.rootBone != null && targetByName.TryGetValue(smr.rootBone.name, out var rootMatch))
            {
                smr.rootBone = rootMatch;
            }
            else if (targetByName.TryGetValue("Hips", out var hips))
            {
                // fallback only
                smr.rootBone = hips;
            }

            // 3) Helpful logs
            totalMissingBones += missingHere;
            Debug.Log($"[Remap] {smr.name} | bones={oldBones.Length} missing={missingHere} " +
                      $"rootBone={(smr.rootBone ? smr.rootBone.name : "NULL")} | path={GetPath(smr.transform)}");

            EditorUtility.SetDirty(smr);
        }

        Undo.CollapseUndoOperations(undoGroup);

        if (totalMissingBones > 0)
            Debug.LogWarning($"[Remap] Done, but missing bones total={totalMissingBones}. " +
                             $"If clothes jump/warp: there are bone-name mismatches OR wrong rig root selected.");
        else
            Debug.Log("[Remap] Done. No missing bones.");
    }

    private static string GetPath(Transform t)
    {
        if (!t) return "";
        string p = t.name;
        while (t.parent)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }
}
