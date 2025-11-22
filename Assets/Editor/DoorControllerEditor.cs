using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DoorController))]
public class DoorControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DoorController door = (DoorController)target;

        // Title
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Door Configuration", EditorStyles.boldLabel);

        // Door type dropdown
        door.doorType = (DoorType)EditorGUILayout.EnumPopup("Door Type", door.doorType);

        EditorGUILayout.Space();

        // Always show these:
        door.openAngle = EditorGUILayout.FloatField("Open Angle", door.openAngle);
        door.openSpeed = EditorGUILayout.FloatField("Open Speed", door.openSpeed);

        EditorGUILayout.Space();

        // Show puzzle fields ONLY for Puzzle doors
        if (door.doorType == DoorType.Puzzle)
        {
            EditorGUILayout.LabelField("Puzzle Settings", EditorStyles.boldLabel);

            door.puzzleCanvas = (Canvas)EditorGUILayout.ObjectField("Puzzle Canvas", door.puzzleCanvas, typeof(Canvas), true);

            SerializedProperty piecesProp = serializedObject.FindProperty("pieces");
            SerializedProperty targetsProp = serializedObject.FindProperty("targets");

            EditorGUILayout.PropertyField(piecesProp, true);
            EditorGUILayout.PropertyField(targetsProp, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
