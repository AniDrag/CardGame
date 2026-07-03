using System;
using UnityEngine;

namespace AniDrag.Utility.Inspector
{
    /// <summary>
    /// Starts an inspector fold/toggle section. Fields below it marked with [ShowIf]
    /// are shown only when the header checkbox is enabled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class HeaderShowIfAttribute : PropertyAttribute
    {
        public readonly string Title;
        public readonly int Size;
        public readonly string Color;
        public readonly bool DefaultVisible;

        public HeaderShowIfAttribute(
            string title,
            int size = 12,
            string color = "#FFFFFF",
            bool defaultVisible = true)
        {
            Title = title;
            Size = size;
            Color = color;
            DefaultVisible = defaultVisible;
        }
    }

    /// <summary>
    /// Marks a field as controlled by the most recent [HeaderShowIf] above it.
    /// Fields without [ShowIf] stay visible even when the header checkbox is disabled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class ShowIfAttribute : PropertyAttribute
    {
    }
}
