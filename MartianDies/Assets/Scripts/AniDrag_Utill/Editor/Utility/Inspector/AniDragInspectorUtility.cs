#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AniDrag.Utility.Inspector.Editor
{
    public static class AniDragInspectorUtility
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<string, object[]> MethodParameterValues = new Dictionary<string, object[]>();

        public static void DrawAniDragInspector(UnityEditor.Editor editor)
        {
            if (editor == null || editor.serializedObject == null)
                return;

            DrawSerializedProperties(editor.serializedObject, editor.target);
            DrawDebugButtons(editor.targets);
        }

        public static void DrawSerializedProperties(SerializedObject serializedObject, UnityEngine.Object target)
        {
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            bool headerActive = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                using (new EditorGUI.DisabledScope(iterator.propertyPath == "m_Script"))
                {
                    FieldInfo fieldInfo = FindFieldInfo(target.GetType(), iterator.propertyPath);
                    bool isScriptField = iterator.propertyPath == "m_Script";

                    HeaderShowIfAttribute header = fieldInfo?.GetCustomAttribute<HeaderShowIfAttribute>(true);
                    if (header != null)
                    {
                        string currentHeaderKey = MakeHeaderKey(target, iterator.propertyPath, header.Title);
                        headerActive = SessionState.GetBool(currentHeaderKey, header.DefaultVisible);
                        headerActive = DrawHeaderShowIf(header, headerActive);
                        SessionState.SetBool(currentHeaderKey, headerActive);
                    }

                    bool isControlledByHeader = fieldInfo?.GetCustomAttribute<ShowIfAttribute>(true) != null;
                    if (isControlledByHeader && !headerActive)
                        continue;

                    ConditionShowIfAttribute condition = fieldInfo?.GetCustomAttribute<ConditionShowIfAttribute>(true);
                    if (condition != null && !EvaluateCondition(target, condition))
                        continue;

                    DrawPropertyWithOptionalColor(iterator, fieldInfo, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        public static void DrawDebugButtons(IEnumerable<UnityEngine.Object> targets)
        {
            if (targets == null)
                return;

            UnityEngine.Object[] targetArray = targets.Where(t => t != null).ToArray();
            if (targetArray.Length == 0)
                return;

            Type targetType = targetArray[0].GetType();
            MethodInfo[] methods = targetType
                .GetMethods(InstanceFlags)
                .Where(m => m.GetCustomAttribute<DebugButtonAttribute>(true) != null)
                .OrderBy(m => m.MetadataToken)
                .ToArray();

            if (methods.Length == 0)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Debug Buttons", EditorStyles.boldLabel);

            foreach (MethodInfo method in methods)
            {
                DebugButtonAttribute attr = method.GetCustomAttribute<DebugButtonAttribute>(true);
                DrawDebugButtonForMethod(targetArray, method, attr);
            }
        }

        private static void DrawDebugButtonForMethod(UnityEngine.Object[] targets, MethodInfo method, DebugButtonAttribute attr)
        {
            ParameterInfo[] parameters = method.GetParameters();
            string key = MakeMethodKey(targets[0], method);

            if (!MethodParameterValues.TryGetValue(key, out object[] values) || values.Length != parameters.Length)
            {
                values = parameters.Select(p => GetDefaultValue(p.ParameterType)).ToArray();
                MethodParameterValues[key] = values;
            }

            Color oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = ParseColor(attr.Color, oldBackground);

            bool enabled = !attr.EnableOnlyOnPlay || EditorApplication.isPlaying;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                string buttonName = string.IsNullOrWhiteSpace(attr.Title) ? ObjectNames.NicifyVariableName(method.Name) : attr.Title;
                if (GUILayout.Button(buttonName, GUILayout.Height(Mathf.Max(18f, attr.Height))))
                {
                    foreach (UnityEngine.Object target in targets)
                    {
                        InvokeDebugMethod(target, method, parameters, attr.ShowParameters ? values : null);
                    }
                }
            }

            GUI.backgroundColor = oldBackground;

            if (attr.ShowParameters && parameters.Length > 0)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < parameters.Length; i++)
                {
                    values[i] = DrawParameterField(parameters[i].Name, parameters[i].ParameterType, values[i]);
                }
                EditorGUI.indentLevel--;
            }
            else if (parameters.Length > 0 && !attr.ShowParameters)
            {
                EditorGUILayout.HelpBox($"{method.Name} has parameters. Enable showParameters to edit and pass values.", MessageType.Info);
            }
        }

        private static void InvokeDebugMethod(UnityEngine.Object target, MethodInfo method, ParameterInfo[] parameters, object[] values)
        {
            try
            {
                object[] args = parameters.Length == 0 ? null : values;
                Undo.RecordObject(target, $"Debug Button {method.Name}");
                method.Invoke(target, args);
                EditorUtility.SetDirty(target);
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception, target);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, target);
            }
        }

        private static object DrawParameterField(string label, Type type, object value)
        {
            label = ObjectNames.NicifyVariableName(label);

            if (type == typeof(int)) return EditorGUILayout.IntField(label, value is int v ? v : 0);
            if (type == typeof(float)) return EditorGUILayout.FloatField(label, value is float v ? v : 0f);
            if (type == typeof(double)) return EditorGUILayout.DoubleField(label, value is double v ? v : 0d);
            if (type == typeof(bool)) return EditorGUILayout.Toggle(label, value is bool v && v);
            if (type == typeof(string)) return EditorGUILayout.TextField(label, value as string ?? string.Empty);
            if (type == typeof(Vector2)) return EditorGUILayout.Vector2Field(label, value is Vector2 v ? v : Vector2.zero);
            if (type == typeof(Vector3)) return EditorGUILayout.Vector3Field(label, value is Vector3 v ? v : Vector3.zero);
            if (type == typeof(Vector4)) return EditorGUILayout.Vector4Field(label, value is Vector4 v ? v : Vector4.zero);
            if (type == typeof(Color)) return EditorGUILayout.ColorField(label, value is Color v ? v : Color.white);
            if (type.IsEnum) return EditorGUILayout.EnumPopup(label, value is Enum v ? v : (Enum)Enum.GetValues(type).GetValue(0));
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return EditorGUILayout.ObjectField(label, value as UnityEngine.Object, type, true);

            EditorGUILayout.HelpBox($"Parameter '{label}' type '{type.Name}' is not supported by DebugButton yet.", MessageType.Warning);
            return value;
        }

        public static bool EvaluateCondition(UnityEngine.Object target, ConditionShowIfAttribute attr)
        {
            if (target == null || attr == null || string.IsNullOrWhiteSpace(attr.MemberName))
                return true;

            Type type = target.GetType();
            object value = null;
            bool found = false;

            FieldInfo field = FindFieldInfo(type, attr.MemberName, true);
            if (field != null)
            {
                value = field.GetValue(target);
                found = true;
            }

            if (!found)
            {
                PropertyInfo property = type.GetProperty(attr.MemberName, InstanceFlags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    value = property.GetValue(target);
                    found = true;
                }
            }

            if (!found)
            {
                MethodInfo method = type.GetMethod(attr.MemberName, InstanceFlags);
                if (method != null && method.GetParameters().Length == 0)
                {
                    value = method.Invoke(target, null);
                    found = true;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"ConditionShowIf could not find member '{attr.MemberName}' on {type.Name}.", target);
                return true;
            }

            bool result;
            if (attr.HasExpectedValue)
                result = ValuesMatch(value, attr.ExpectedValue);
            else
                result = value is bool b && b;

            if (attr.Mode == ConditionShowMode.ShowWhenFalse)
                result = !result;

            return result;
        }

        public static void DrawPropertyWithOptionalColor(SerializedProperty property, FieldInfo fieldInfo, bool includeChildren)
        {
            ColorFieldAttribute colorAttribute = fieldInfo?.GetCustomAttribute<ColorFieldAttribute>(true);
            if (colorAttribute == null)
            {
                EditorGUILayout.PropertyField(property, includeChildren);
                return;
            }

            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(property, includeChildren));
            Color color = GetColor(colorAttribute);
            EditorGUI.DrawRect(rect, color);
            EditorGUI.PropertyField(rect, property, includeChildren);
        }

        public static Color GetColor(ColorFieldAttribute attribute)
        {
            if (attribute == null)
                return new Color(1f, 1f, 1f, 0.25f);

            if (!string.IsNullOrWhiteSpace(attribute.Hex) && ColorUtility.TryParseHtmlString(attribute.Hex, out Color hexColor))
            {
                hexColor.a = attribute.A;
                return hexColor;
            }

            if (attribute.Preset.HasValue)
            {
                Color color;
                switch (attribute.Preset.Value)
                {
                    case ColorFieldColor.White:
                        color = Color.white;
                        break;
                    case ColorFieldColor.Red:
                        color = Color.red;
                        break;
                    case ColorFieldColor.Green:
                        color = Color.green;
                        break;
                    case ColorFieldColor.Blue:
                        color = Color.blue;
                        break;
                    case ColorFieldColor.Yellow:
                        color = Color.yellow;
                        break;
                    case ColorFieldColor.Cyan:
                        color = Color.cyan;
                        break;
                    case ColorFieldColor.Magenta:
                        color = Color.magenta;
                        break;
                    case ColorFieldColor.Gray:
                        color = Color.gray;
                        break;
                    case ColorFieldColor.Orange:
                        color = new Color(1f, 0.55f, 0f, 1f);
                        break;
                    case ColorFieldColor.Purple:
                        color = new Color(0.55f, 0.25f, 0.85f, 1f);
                        break;
                    default:
                        color = Color.white;
                        break;
                }
                color.a = attribute.A;
                return color;
            }

            return new Color(attribute.R, attribute.G, attribute.B, attribute.A);
        }

        private static bool DrawHeaderShowIf(HeaderShowIfAttribute header, bool active)
        {
            Color oldColor = GUI.color;
            GUI.color = ParseColor(header.Color, oldColor);

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = header.Size,
                alignment = TextAnchor.MiddleLeft
            };

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            active = EditorGUILayout.Toggle(active, GUILayout.Width(18f));
            EditorGUILayout.LabelField(header.Title, style);
            EditorGUILayout.EndHorizontal();

            GUI.color = oldColor;
            return active;
        }

        private static bool ValuesMatch(object currentValue, string expectedValue)
        {
            if (currentValue == null || expectedValue == null)
                return currentValue == null && expectedValue == null;

            Type currentType = currentValue.GetType();

            if (currentType.IsEnum)
                return string.Equals(currentValue.ToString(), expectedValue, StringComparison.Ordinal);

            try
            {
                object converted = Convert.ChangeType(expectedValue, currentType, CultureInfo.InvariantCulture);
                return Equals(currentValue, converted);
            }
            catch
            {
                return string.Equals(currentValue.ToString(), expectedValue, StringComparison.Ordinal);
            }
        }

        private static Color ParseColor(string color, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(color))
                return fallback;

            return ColorUtility.TryParseHtmlString(color, out Color parsed) ? parsed : fallback;
        }

        private static object GetDefaultValue(Type type)
        {
            if (type == typeof(string)) return string.Empty;
            if (type == typeof(Color)) return Color.white;
            if (type == typeof(Vector2)) return Vector2.zero;
            if (type == typeof(Vector3)) return Vector3.zero;
            if (type == typeof(Vector4)) return Vector4.zero;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return null;
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static string MakeHeaderKey(UnityEngine.Object target, string propertyPath, string title)
        {
            return $"AniDrag.HeaderShowIf.{target.GetInstanceID()}.{propertyPath}.{title}";
        }

        private static string MakeMethodKey(UnityEngine.Object target, MethodInfo method)
        {
            return $"AniDrag.DebugButton.{target.GetInstanceID()}.{method.DeclaringType?.FullName}.{method.Name}";
        }

        public static FieldInfo FindFieldInfo(Type hostType, string propertyPath, bool directName = false)
        {
            if (hostType == null || string.IsNullOrWhiteSpace(propertyPath))
                return null;

            if (directName)
                return FindFieldInTypeHierarchy(hostType, propertyPath);

            string cleanPath = propertyPath.Replace(".Array.data[", "[");
            string[] parts = cleanPath.Split('.');
            Type currentType = hostType;
            FieldInfo field = null;

            foreach (string rawPart in parts)
            {
                string part = rawPart;
                int bracketIndex = part.IndexOf('[');
                if (bracketIndex >= 0)
                    part = part.Substring(0, bracketIndex);

                field = FindFieldInTypeHierarchy(currentType, part);
                if (field == null)
                    return null;

                currentType = GetFieldElementType(field.FieldType);
            }

            return field;
        }

        private static FieldInfo FindFieldInTypeHierarchy(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, InstanceFlags);
                if (field != null)
                    return field;

                type = type.BaseType;
            }

            return null;
        }

        private static Type GetFieldElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType && typeof(IList<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
                return type.GetGenericArguments()[0];

            if (type.IsGenericType && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)))
                return type.GetGenericArguments()[0];

            return type;
        }
    }
}
#endif
