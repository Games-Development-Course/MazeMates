using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject representing a tool in the game.
/// </summary>
[CreateAssetMenu(fileName = "ToolSO", menuName = "Scriptable Objects/ToolSO")]
public class ToolSO : ScriptableObject
{
    public string toolName;
    public Sprite icon;
    public float cooldown;

    public enum ToolType { PlaceResource, Ping, MarkDanger, HighlightPath }
    public ToolType type;
}
