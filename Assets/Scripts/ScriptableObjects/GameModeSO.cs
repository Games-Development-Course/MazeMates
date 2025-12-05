using UnityEngine;

[CreateAssetMenu(fileName = "GameModeSO", menuName = "Scriptable Objects/GameModeSO")]
public class GameModeSO : ScriptableObject
{
    public string id;
    public ResourceTypeSO.ResourceKind resourceType;
    public Vector2 positionOffset;
}
