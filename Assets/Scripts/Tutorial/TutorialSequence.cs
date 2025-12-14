using UnityEngine;

[CreateAssetMenu(
    menuName = "Scriptable Objects/Tutorial Sequence",
    fileName = "NewTutorialSequence"
)]
public class TutorialSequence : ScriptableObject
{
    public TutorialStep[] steps;
}
