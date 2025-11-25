using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DoorController))]
public class DoorControllerEditor : Editor
{
    SerializedProperty piecesProp;
    SerializedProperty targetsProp;

    void OnEnable()
    {
        piecesProp = serializedObject.FindProperty("pieces");
        targetsProp = serializedObject.FindProperty("targets");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DoorController door = (DoorController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Door Configuration", EditorStyles.boldLabel);

        door.doorType = (DoorType)EditorGUILayout.EnumPopup("Door Type", door.doorType);

        EditorGUILayout.Space();
        door.openAngle = EditorGUILayout.FloatField("Open Angle", door.openAngle);
        door.openSpeed = EditorGUILayout.FloatField("Open Speed", door.openSpeed);

        EditorGUILayout.Space();

        if (door.doorType == DoorType.Puzzle)
        {
            EditorGUILayout.LabelField("Puzzle Settings", EditorStyles.boldLabel);

            door.puzzleCanvas = (Canvas)EditorGUILayout.ObjectField(
                "Puzzle Canvas",
                door.puzzleCanvas,
                typeof(Canvas),
                true
            );

            door.puzzleSprite = (Sprite)EditorGUILayout.ObjectField(
          "Puzzle Sprite",
          door.puzzleSprite,
          typeof(Sprite),
          false
      );


            EditorGUILayout.PropertyField(piecesProp, true);
            EditorGUILayout.PropertyField(targetsProp, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
