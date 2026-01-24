using UnityEngine;

[CreateAssetMenu(menuName = "MazeMates/Puzzle", fileName = "Puzzle_")]
public class Puzzle : ScriptableObject
{
    [Header("Images")]
    public Sprite originalImage;      // Navigator TV
    public Sprite backgroundImage;    // Traveller background

    [Header("Targets (normalized 0..1 over the background rect)")]
    public TargetDef[] targets;

    [Header("Hints (order matters)")]
    public HintDef[] hints;

    [Header("Pieces")]
    public PieceDef[] pieces;

    [System.Serializable]
    public struct TargetDef
    {
        public string id;                // "Bird", "Car"...
        public Vector2 normalizedPos;    // 0..1 center within background rect
        public Vector2 normalizedSize;   // 0..1 size relative to background rect (optional, useful as snap area)
    }

    [System.Serializable]
    public struct HintDef
    {
        public string id;                // "BirdHint" etc
        public string targetId;          // which target it relates to

        public Sprite sprite;            // ✅ the hint image itself

        public Vector2 normalizedPos;    // 0..1 center within background rect
        public Vector2 normalizedSize;   // 0..1 size relative to background rect (if 0 -> fallback)
    }

    [System.Serializable]
    public struct PieceDef
    {
        public string id;                // "Bird", "Car"...
        public Sprite sprite;            // piece sprite
        public Vector2 normalizedSize;   // 0..1 size relative to background rect
        public string targetId;          // which target it should snap to
    }
}
