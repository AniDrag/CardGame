#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace AniDrag.Utility
{
    [CustomPropertyDrawer(typeof(RequiredAttribute))]
    public class RequiredDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Draw the normal property field
            EditorGUI.PropertyField(position, property, label);

            // If nothing assigned → draw warning
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
            {
                // Draw a red box behind the property
                var oldColor = GUI.color;
                GUI.color = Color.red;
                GUI.Box(position, GUIContent.none);
                GUI.color = oldColor;

                // Draw again so text shows on top
                EditorGUI.PropertyField(position, property, label);
            }
        }
    }
}
#endif
