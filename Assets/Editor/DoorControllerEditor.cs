//using UnityEditor;
//using UnityEngine;

//[CustomEditor(typeof(DoorController))]
//public class DoorControllerEditor : Editor
//{
//    SerializedProperty piecesProp;
//    SerializedProperty targetsProp;
//    SerializedProperty canvasProp;
//    SerializedProperty originalImageProp;
//    SerializedProperty doorTypeProp;
//    SerializedProperty openAngleProp;
//    SerializedProperty openSpeedProp;

//    void OnEnable()
//    {
//        doorTypeProp = serializedObject.FindProperty("doorType");
//        openAngleProp = serializedObject.FindProperty("openAngle");
//        openSpeedProp = serializedObject.FindProperty("openSpeed");

//        canvasProp = serializedObject.FindProperty("puzzleCanvas");
//        piecesProp = serializedObject.FindProperty("pieces");
//        targetsProp = serializedObject.FindProperty("targets");
//        originalImageProp = serializedObject.FindProperty("puzzleOriginalImage");
//    }

//    public override void OnInspectorGUI()
//    {
//        serializedObject.Update();

//        EditorGUILayout.PropertyField(doorTypeProp);
//        EditorGUILayout.PropertyField(openAngleProp);
//        EditorGUILayout.PropertyField(openSpeedProp);

//        DoorController door = (DoorController)target;

//        EditorGUILayout.Space();

//        if (door.doorType == DoorType.Puzzle)
//        {
//            EditorGUILayout.LabelField("Puzzle Settings", EditorStyles.boldLabel);

//            EditorGUILayout.PropertyField(canvasProp, new GUIContent("Puzzle Canvas"));
//            EditorGUILayout.PropertyField(
//                originalImageProp,
//                new GUIContent("Original Puzzle Image (GameObject)")
//            );

//            EditorGUILayout.PropertyField(piecesProp, true);
//            EditorGUILayout.PropertyField(targetsProp, true);
//        }

//        serializedObject.ApplyModifiedProperties();
//    }
//}
