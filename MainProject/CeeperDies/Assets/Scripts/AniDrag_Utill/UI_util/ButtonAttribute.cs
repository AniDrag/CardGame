using UnityEngine;
namespace AniDrag.Utility
{
    public enum ButtonSize { Small, Medium, Large }
    public enum SdfIconType
    {
        None,
        ToggleOn,
        ToggleOff,
        Plus,
        Minus,
        Close,
        Custom // for assigning your own texture
    }
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class ButtonAttribute : System.Attribute
    {
        public string Label;
        public float SpaceAbove;
        public Color ButtonColor = Color.white;
        public ButtonSize Size = ButtonSize.Medium;
        public SdfIconType Icon = SdfIconType.None;

         public ButtonAttribute(string label = "", ButtonSize size = ButtonSize.Medium, float spaceAbove = 0f, 
            float r = 1f, float g = 1f, float b = 1f, SdfIconType icon = SdfIconType.None)
        {
            Label = label;
            Size = size;
            SpaceAbove = spaceAbove;
            ButtonColor = new Color(r, g, b);
            Icon = icon;
        }
    }
}