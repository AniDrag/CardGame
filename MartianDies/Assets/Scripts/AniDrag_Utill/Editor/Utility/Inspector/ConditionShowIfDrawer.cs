#if UNITY_EDITOR
using AniDrag.Utility.Inspector;
using UnityEditor;
using UnityEngine;

namespace AniDrag.Utility.Inspector.Editor
{
    /// <summary>
    /// Fallback drawer for direct PropertyField usage. The main AniDragUtilityEditor also handles this.
    /// Note: Unity uses one main PropertyDrawer per field, so if you stack this with another drawer,
    /// prefer using AniDragUtilityEditor or AniDragInspectorUtility.DrawSerializedProperties.
    /// </summary>
    [CustomPropertyDrawer(typeof(ConditionShowIfAttribute))]
    public sealed class ConditionShowIfDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ConditionShowIfAttribute condition = (ConditionShowIfAttribute)attribute;
            if (AniDragInspectorUtility.EvaluateCondition(property.serializedObject.targetObject, condition))
                EditorGUI.PropertyField(position, property, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            ConditionShowIfAttribute condition = (ConditionShowIfAttribute)attribute;
            return AniDragInspectorUtility.EvaluateCondition(property.serializedObject.targetObject, condition)
                ? EditorGUI.GetPropertyHeight(property, label, true)
                : -EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
#endif
