#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace AniDrag.Utility
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class MethodButtonEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); // draw regular serialized fields

            MonoBehaviour myTarget = (MonoBehaviour)target;

            MethodInfo[] methods = myTarget.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<ButtonAttribute>();
                if (attr != null)
                {
                    GUILayout.Space(attr.SpaceAbove);

                    Color oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = attr.ButtonColor;

                    string label = string.IsNullOrEmpty(attr.Label) ? method.Name : attr.Label;

                    // Add icon prefix if assigned
                    if (attr.Icon != SdfIconType.None)
                        label = GetIconString(attr.Icon) + " " + label;

                    GUILayoutOption[] options = GetButtonSize(attr.Size);

                    if (GUILayout.Button(label, options))
                        method.Invoke(myTarget, null);

                    GUI.backgroundColor = oldColor;
                }
            }
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
