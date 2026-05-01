#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(ButtonBehaviour))]
public class ButtonBehaviourEditor : Editor
{
    SerializedProperty resolveTypeProp;
    SerializedProperty targetProp;
    SerializedProperty audioClipProp;
    SerializedProperty audioOutputProp;
    SerializedProperty childIndexProp;
    SerializedProperty grandchildIndexProp;

    private void OnEnable()
    {
        resolveTypeProp = serializedObject.FindProperty("resolveType");
        targetProp = serializedObject.FindProperty("target");
        audioClipProp = serializedObject.FindProperty("audioClip");
        audioOutputProp = serializedObject.FindProperty("audioAutput");
        childIndexProp = serializedObject.FindProperty("childIndex");
        grandchildIndexProp = serializedObject.FindProperty("grandchildIndex");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(resolveTypeProp);
        EditorGUILayout.PropertyField(targetProp);

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(audioClipProp);
        EditorGUILayout.PropertyField(audioOutputProp);

        var action = (ButtonBehaviour.ActivationType)resolveTypeProp.enumValueIndex;

        bool showChild =
            action == ButtonBehaviour.ActivationType.DisableChildObject ||
            action == ButtonBehaviour.ActivationType.EnableChildObject ||
            action == ButtonBehaviour.ActivationType.DisableGrandChildObject ||
            action == ButtonBehaviour.ActivationType.EnableGrandChildObject;

        bool showGrandChild =
            action == ButtonBehaviour.ActivationType.DisableGrandChildObject ||
            action == ButtonBehaviour.ActivationType.EnableGrandChildObject;

        if (showChild)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Child Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(childIndexProp);

            if (showGrandChild)
                EditorGUILayout.PropertyField(grandchildIndexProp);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif