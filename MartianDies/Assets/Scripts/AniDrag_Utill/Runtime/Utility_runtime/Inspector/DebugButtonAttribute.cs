using System;

namespace AniDrag.Utility.Inspector
{
    /// <summary>
    /// Draws a clickable method button in the inspector.
    /// Works on methods in MonoBehaviours and ScriptableObjects.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class DebugButtonAttribute : Attribute
    {
        public readonly bool ShowParameters;
        public readonly string Title;
        public readonly float Height;
        public readonly string Color;
        public readonly bool EnableOnlyOnPlay;

        /// <param name="showParameters">If true, supported method parameters are shown as editable fields under the button.</param>
        /// <param name="title">Button label. Empty/null uses the method name.</param>
        /// <param name="size">Button height in pixels.</param>
        /// <param name="color">Hex color string, for example "#66CCFF".</param>
        /// <param name="enableOnlyOnPlay">If true, button is disabled outside Play Mode.</param>
        public DebugButtonAttribute(
            bool showParameters = false,
            string title = null,
            float size = 24f,
            string color = "#6FA8DC",
            bool enableOnlyOnPlay = true)
        {
            ShowParameters = showParameters;
            Title = title;
            Height = size;
            Color = color;
            EnableOnlyOnPlay = enableOnlyOnPlay;
        }
    }
}
