#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AniDrag.Utility
{
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ShowIfAttribute showIf = (ShowIfAttribute)attribute;

            // Get the target object
            var targetObject = property.serializedObject.targetObject;

            // Find the boolean field specified in the attribute
            var field = targetObject.GetType().GetField(
                showIf.expression,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance
            );

            // Only draw if the field exists and is true
            if (field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(targetObject))
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            ShowIfAttribute showIf = (ShowIfAttribute)attribute;
            var targetObject = property.serializedObject.targetObject;

            var field = targetObject.GetType().GetField(
                showIf.expression,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance
            );

            if (field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(targetObject))
            {
                return EditorGUI.GetPropertyHeight(property, label);
            }

            return 0f; // Hide the field
        }
    }
}
#endif
