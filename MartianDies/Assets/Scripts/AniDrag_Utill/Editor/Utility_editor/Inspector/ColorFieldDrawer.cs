#if UNITY_EDITOR
using AniDrag.Utility.Inspector;
using UnityEditor;
using UnityEngine;

namespace AniDrag.Utility.Inspector.Editor
{
    /// <summary>
    /// Fallback drawer when a field is drawn by Unity's default property drawer system.
    /// The main AniDragUtilityEditor also handles this, so this is mostly for custom editors
    /// that manually call EditorGUILayout.PropertyField.
    /// </summary>
    [CustomPropertyDrawer(typeof(ColorFieldAttribute))]
    public sealed class ColorFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ColorFieldAttribute colorField = (ColorFieldAttribute)attribute;
            EditorGUI.DrawRect(position, AniDragInspectorUtility.GetColor(colorField));
            EditorGUI.PropertyField(position, property, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
#endif
