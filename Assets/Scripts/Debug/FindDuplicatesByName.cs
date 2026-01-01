// Assets/Scripts/Debug/FindDuplicatesByName.cs
using System.Linq;
using UnityEngine;



public sealed class FindDuplicatesByName : MonoBehaviour
{
    [SerializeField] private string objectName = "NavEnviroment";

    [ContextMenu("Find Duplicates")]
    public void Find()
    {
        var all = FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name == objectName)
            .ToArray();

        Debug.Log($"[DupCheck] Found {all.Length} objects named '{objectName}'");
        foreach (var t in all)
            Debug.Log($"[DupCheck] {t.name} path={GetPath(t)} pos={t.position} rot={t.rotation.eulerAngles} scale={t.lossyScale}");
    }

    private static string GetPath(Transform t)
    {
        var path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
