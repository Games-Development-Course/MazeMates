using UnityEngine;

[CreateAssetMenu(menuName = "MazeMates/Tutorial Sequence", fileName = "NewTutorialSequence")]
public class TutorialSequence : ScriptableObject
{
    public TutorialStep[] steps;
}
