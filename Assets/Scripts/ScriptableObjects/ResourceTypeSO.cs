using UnityEngine;

[CreateAssetMenu(fileName = "ResourceTypeSO", menuName = "Scriptable Objects/ResourceTypeSO")]
public class ResourceTypeSO : ScriptableObject
{
    public string id;
    public string resourceName;
    public Sprite icon;

    public enum ResourceKind { Heart, Key, Bomb }
    public ResourceKind kind;
}
