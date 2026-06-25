#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AniDrag.Utility
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class MethodButtonEditor : Editor
    {
        // Holds parameter values per method (method name → list of current values)
        private Dictionary<string, List<object>> _methodParams = new Dictionary<string, List<object>>();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); // draw regular serialized fields

            MonoBehaviour targetBehaviour = (MonoBehaviour)target;

            // Get all methods with ButtonAttribute (instance, public + non-public)
            MethodInfo[] methods = targetBehaviour.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<ButtonAttribute>();
                if (attr == null) continue;

                // Get parameter info
                ParameterInfo[] parameters = method.GetParameters();
                string methodKey = method.Name;

                // Ensure we have a value list for this method
                if (!_methodParams.ContainsKey(methodKey))
                {
                    _methodParams[methodKey] = new List<object>();
                    // Initialise with default values
                    foreach (var p in parameters)
                        _methodParams[methodKey].Add(GetDefaultValue(p.ParameterType));
                }

                // --- Draw parameter fields ---
                if (parameters.Length > 0)
                {
                    EditorGUILayout.BeginVertical("helpbox");
                    EditorGUILayout.LabelField($"Parameters for {method.Name}", EditorStyles.miniBoldLabel);

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var param = parameters[i];
                        string label = ObjectNames.NicifyVariableName(param.Name);
                        object currentValue = _methodParams[methodKey][i];
                        Type paramType = param.ParameterType;

                        // Create appropriate field based on type
                        object newValue = DrawParameterField(label, paramType, currentValue);

                        // Store if changed
                        if (!Equals(currentValue, newValue))
                            _methodParams[methodKey][i] = newValue;
                    }

                    EditorGUILayout.EndVertical();
                }

                // --- Draw the button ---
                GUILayout.Space(attr.SpaceAbove);

                Color oldColor = GUI.backgroundColor;
                GUI.backgroundColor = attr.ButtonColor;

                string buttonLabel = string.IsNullOrEmpty(attr.Label) ? method.Name : attr.Label;
                if (attr.Icon != SdfIconType.None)
                    buttonLabel = GetIconString(attr.Icon) + " " + buttonLabel;

                GUILayoutOption[] options = GetButtonSize(attr.Size);

                if (GUILayout.Button(buttonLabel, options))
                {
                    // Build parameter array
                    object[] paramValues = _methodParams[methodKey].ToArray();
                    try
                    {
                        method.Invoke(targetBehaviour, paramValues);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error invoking method {method.Name}: {ex.Message}");
                    }
                }

                GUI.backgroundColor = oldColor;
            }
        }

        // ----------------------------------------------------------------------------
        // Parameter field drawing – supports common Unity and C# types
        // ----------------------------------------------------------------------------
        private object DrawParameterField(string label, Type type, object currentValue)
        {
            // Nullable types – unwrap and handle accordingly
            Type underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
                type = underlyingType;

            // --- Handle known types ---
            if (type == typeof(int))
                return EditorGUILayout.IntField(label, (int)currentValue);
            if (type == typeof(float))
                return EditorGUILayout.FloatField(label, (float)currentValue);
            if (type == typeof(double))
                return EditorGUILayout.DoubleField(label, (double)currentValue);
            if (type == typeof(string))
                return EditorGUILayout.TextField(label, (string)currentValue);
            if (type == typeof(bool))
                return EditorGUILayout.Toggle(label, (bool)currentValue);
            if (type == typeof(Vector2))
                return EditorGUILayout.Vector2Field(label, (Vector2)currentValue);
            if (type == typeof(Vector3))
                return EditorGUILayout.Vector3Field(label, (Vector3)currentValue);
            if (type == typeof(Vector4))
                return EditorGUILayout.Vector4Field(label, (Vector4)currentValue);
            if (type == typeof(Color))
                return EditorGUILayout.ColorField(label, (Color)currentValue);
            if (type == typeof(AnimationCurve))
                return EditorGUILayout.CurveField(label, (AnimationCurve)currentValue);
            if (type == typeof(Gradient))
                return EditorGUILayout.GradientField(label, (Gradient)currentValue);
            if (type == typeof(LayerMask))
                return (LayerMask)EditorGUILayout.LayerField(label, (LayerMask)currentValue);

            // --- Enums ---
            if (type.IsEnum)
                return EditorGUILayout.EnumPopup(label, (Enum)currentValue);

            // --- Unity Objects (GameObject, Component, ScriptableObject, etc.) ---
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                // Use generic ObjectField – restrict to the specific type
                return EditorGUILayout.ObjectField(label, (UnityEngine.Object)currentValue, type, allowSceneObjects: true);
            }

            // --- Fallback: display as text (maybe use a text field for unknown types) ---
            EditorGUILayout.LabelField(label, $"Unsupported type: {type.Name}");
            return currentValue;
        }

        // ----------------------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------------------
        private object GetDefaultValue(Type type)
        {
            if (type == typeof(string)) return "";
            if (type.IsValueType) return Activator.CreateInstance(type);
            return null;
        }

        private GUILayoutOption[] GetButtonSize(ButtonSize size)
        {
            switch (size)
            {
                case ButtonSize.Small: return new GUILayoutOption[] { GUILayout.Height(20) };
                case ButtonSize.Medium: return new GUILayoutOption[] { GUILayout.Height(30) };
                case ButtonSize.Large: return new GUILayoutOption[] { GUILayout.Height(40) };
                default: return new GUILayoutOption[] { GUILayout.Height(30) };
            }
        }

        private string GetIconString(SdfIconType icon)
        {
            return icon switch
            {
                SdfIconType.ToggleOn => "✓",
                SdfIconType.ToggleOff => "✗",
                SdfIconType.Plus => "+",
                SdfIconType.Minus => "-",
                SdfIconType.Close => "×",
                _ => ""
            };
        }
    }
}
#endif