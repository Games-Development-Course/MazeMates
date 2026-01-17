// Assets/Scripts/Debug/SkinRigDebugReport.cs
using System.Text;
using UnityEngine;

public sealed class SkinRigDebugReport : MonoBehaviour
{
    [Header("Assign (optional)")]
    [Tooltip("If empty, will use Animator on this object or its parents.")]
    [SerializeField] private Animator animator;

    [Tooltip("If empty, will try to find a child named 'Skeleton.001' under this prefab.")]
    [SerializeField] private Transform mainRigRoot;

    [Header("Where are your skins?")]
    [Tooltip("Drag Skin1..Skin4 objects here. If empty, will try find children named Skin1..Skin4.")]
    [SerializeField] private Transform[] skinRoots;

    [Header("Run")]
    [SerializeField] private bool runOnStart = true;

    private void Start()
    {
        if (runOnStart)
            PrintReport();
    }

    [ContextMenu("Print Skin Rig Report")]
    public void PrintReport()
    {
        ResolveRefs();

        var sb = new StringBuilder(4096);

        sb.AppendLine("========== Skin Rig Debug Report ==========");
        sb.AppendLine($"PrefabRoot: {transform.name}");
        sb.AppendLine($"Animator: {(animator ? animator.name : "NULL")} | Avatar={(animator && animator.avatar ? animator.avatar.name : "NULL")}");
        sb.AppendLine($"MainRigRoot: {(mainRigRoot ? GetPath(mainRigRoot) : "NULL")}");
        sb.AppendLine();

        if (!animator)
        {
            sb.AppendLine("❌ No Animator found. Skins won't animate.");
            Debug.Log(sb.ToString(), this);
            return;
        }

        if (!mainRigRoot)
        {
            sb.AppendLine("❌ No mainRigRoot found. Assign Skeleton.001 (or your rig root) to 'Main Rig Root'.");
            Debug.Log(sb.ToString(), this);
            return;
        }

        if (skinRoots == null || skinRoots.Length == 0)
        {
            sb.AppendLine("❌ No skin roots found. Assign Skin1..Skin4 to Skin Roots array, or name them Skin1..Skin4 under this object.");
            Debug.Log(sb.ToString(), this);
            return;
        }

        for (int s = 0; s < skinRoots.Length; s++)
        {
            var skin = skinRoots[s];
            if (!skin) continue;

            sb.AppendLine($"--- {skin.name} --- path={GetPath(skin)}");

            var smrs = skin.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs == null || smrs.Length == 0)
            {
                sb.AppendLine("  (no SkinnedMeshRenderers found)");
                sb.AppendLine();
                continue;
            }

            int okCount = 0, badCount = 0;

            foreach (var smr in smrs)
            {
                if (!smr) continue;

                // RootBone check
                var rb = smr.rootBone;
                bool rbInMainRig = rb && IsChildOf(rb, mainRigRoot);

                // Bones[] check
                var bones = smr.bones;
                int totalBones = bones != null ? bones.Length : 0;
                int bonesInMainRig = 0;
                int bonesOutsideMainRig = 0;

                if (bones != null)
                {
                    for (int i = 0; i < bones.Length; i++)
                    {
                        var b = bones[i];
                        if (!b) { bonesOutsideMainRig++; continue; }
                        if (IsChildOf(b, mainRigRoot)) bonesInMainRig++;
                        else bonesOutsideMainRig++;
                    }
                }

                // Classification:
                // Good = rootBone in main rig AND majority of bones in main rig
                bool looksGood = rbInMainRig && totalBones > 0 && bonesInMainRig >= Mathf.Max(1, totalBones * 8 / 10);

                if (looksGood) okCount++;
                else badCount++;

                if (!looksGood)
                {
                    sb.AppendLine($"  ❌ SMR: {smr.name} | mesh={((smr.sharedMesh != null) ? smr.sharedMesh.name : "NULL")}");
                    sb.AppendLine($"     rootBone: {(rb ? GetPath(rb) : "NULL")} | rootBoneInMainRig={rbInMainRig}");
                    sb.AppendLine($"     bones: total={totalBones} | inMainRig={bonesInMainRig} | outsideMainRig={bonesOutsideMainRig}");

                    // Identify likely cause quickly
                    if (!rb)
                        sb.AppendLine("     ⚠️ Cause: rootBone is NULL (won't follow animation).");
                    else if (!rbInMainRig)
                        sb.AppendLine("     ⚠️ Cause: rootBone is NOT under main rig -> this SMR is bound to a different (internal) skeleton.");
                    else if (bonesOutsideMainRig > 0)
                        sb.AppendLine("     ⚠️ Cause: bones[] contains transforms outside main rig -> mixed/incorrect bone mapping.");

                    sb.AppendLine();
                }
            }

            sb.AppendLine($"  Summary: OK={okCount} BAD={badCount} (BAD means: not bound to main animated skeleton)");
            sb.AppendLine();
        }

        sb.AppendLine("===========================================");
        Debug.Log(sb.ToString(), this);
    }

    private void ResolveRefs()
    {
        if (!animator) animator = GetComponent<Animator>() ?? GetComponentInParent<Animator>();

        if (!mainRigRoot)
        {
            // Try common names
            var t = transform.Find("Skeleton.001");
            if (!t) t = FindDeepChild(transform, "Skeleton.001");
            if (!t) t = transform.Find("Skeleton");
            if (!t) t = FindDeepChild(transform, "Skeleton");
            mainRigRoot = t;
        }

        if (skinRoots == null || skinRoots.Length == 0)
        {
            // Try Skin1..Skin4
            var list = new System.Collections.Generic.List<Transform>();
            for (int i = 1; i <= 4; i++)
            {
                var s = transform.Find($"Skin{i}");
                if (!s) s = FindDeepChild(transform, $"Skin{i}");
                if (s) list.Add(s);
            }
            skinRoots = list.ToArray();
        }
    }

    private static bool IsChildOf(Transform t, Transform parent)
    {
        if (!t || !parent) return false;
        var cur = t;
        while (cur)
        {
            if (cur == parent) return true;
            cur = cur.parent;
        }
        return false;
    }

    private static string GetPath(Transform t)
    {
        if (!t) return "NULL";
        var sb = new StringBuilder();
        sb.Append(t.name);
        while (t.parent)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        if (!root) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
