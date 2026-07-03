using System;
using UnityEngine;

namespace AniDrag.Utility.Inspector
{
    public enum ColorFieldColor
    {
        White,
        Red,
        Green,
        Blue,
        Yellow,
        Cyan,
        Magenta,
        Gray,
        Orange,
        Purple
    }

    /// <summary>
    /// Highlights the background of a serialized field in the inspector.
    /// Unity attributes cannot accept UnityEngine.Color directly, so use rgb floats, rgb ints,
    /// hex strings, or ColorFieldColor presets.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class ColorFieldAttribute : PropertyAttribute
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;
        public readonly string Hex;
        public readonly ColorFieldColor? Preset;

        public ColorFieldAttribute(float r, float g, float b, float a = 0.25f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public ColorFieldAttribute(int r, int g, int b, int a = 64)
        {
            R = Mathf.Clamp01(r / 255f);
            G = Mathf.Clamp01(g / 255f);
            B = Mathf.Clamp01(b / 255f);
            A = Mathf.Clamp01(a / 255f);
        }

        public ColorFieldAttribute(string hex, float alpha = 0.25f)
        {
            Hex = hex;
            A = alpha;
        }

        public ColorFieldAttribute(ColorFieldColor preset, float alpha = 0.25f)
        {
            Preset = preset;
            A = alpha;
        }
    }
}
