using System.Linq;
using UnityEditor;
using UnityEngine;

public class RemapSkinnedMeshBones : EditorWindow
{
    private SkinnedMeshRenderer source;   // הבגד
    private Transform targetRoot;         // Skeleton.001 (או Root שלו)

    [MenuItem("Tools/Remap SkinnedMesh Bones")]
    static void Open() => GetWindow<RemapSkinnedMeshBones>("Remap SkinnedMesh Bones");

    void OnGUI()
    {
        source = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Clothing SMR", source, typeof(SkinnedMeshRenderer), true);
        targetRoot = (Transform)EditorGUILayout.ObjectField("Target Rig Root (Skeleton.001)", targetRoot, typeof(Transform), true);

        if (GUILayout.Button("Remap Bones By Name") && source && targetRoot)
            Remap();
    }

    void Remap()
    {
        var all = targetRoot.GetComponentsInChildren<Transform>(true)
                            .GroupBy(t => t.name)
                            .ToDictionary(g => g.Key, g => g.First());

        var newBones = source.bones.Select(b =>
        {
            if (b == null) return null;
            return all.TryGetValue(b.name, out var match) ? match : b;
        }).ToArray();

        source.rootBone = all.TryGetValue("Hips", out var hips) ? hips : source.rootBone;
        source.bones = newBones;

        EditorUtility.SetDirty(source);
        Debug.Log($"Remapped bones for {source.name}");
    }
}
