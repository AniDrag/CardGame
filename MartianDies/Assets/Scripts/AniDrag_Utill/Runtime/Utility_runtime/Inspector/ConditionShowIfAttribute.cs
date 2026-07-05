using System;
using UnityEngine;

namespace AniDrag.Utility.Inspector
{
    public enum ConditionShowMode
    {
        ShowWhenTrue,
        ShowWhenFalse
    }

    /// <summary>
    /// Shows a field when a condition is true.
    /// Supported:
    /// [ConditionShowIf(nameof(MyBool))]
    /// [ConditionShowIf(nameof(MyBoolMethod))]
    /// [ConditionShowIf(nameof(MyEnum), "Grounded")]
    /// [ConditionShowIf(nameof(MyInt), 5)]
    /// [ConditionShowIf(nameof(MyFloat), 2.5f)]
    /// [ConditionShowIf(nameof(MyString), "SomeValue")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class ConditionShowIfAttribute : PropertyAttribute
    {
        public readonly string MemberName;
        public readonly string ExpectedValue;
        public readonly bool HasExpectedValue;
        public readonly ConditionShowMode Mode;

        public ConditionShowIfAttribute(string memberName)
        {
            MemberName = memberName;
            ExpectedValue = null;
            HasExpectedValue = false;
            Mode = ConditionShowMode.ShowWhenTrue;
        }

        public ConditionShowIfAttribute(string memberName, ConditionShowMode mode)
        {
            MemberName = memberName;
            ExpectedValue = null;
            HasExpectedValue = false;
            Mode = mode;
        }

        public ConditionShowIfAttribute(string memberName, bool expectedValue)
            : this(memberName, expectedValue.ToString(), ConditionShowMode.ShowWhenTrue)
        {
        }

        public ConditionShowIfAttribute(string memberName, bool expectedValue, ConditionShowMode mode)
            : this(memberName, expectedValue.ToString(), mode)
        {
        }

        public ConditionShowIfAttribute(string memberName, int expectedValue)
            : this(memberName, expectedValue.ToString(), ConditionShowMode.ShowWhenTrue)
        {
        }

        public ConditionShowIfAttribute(string memberName, int expectedValue, ConditionShowMode mode)
            : this(memberName, expectedValue.ToString(), mode)
        {
        }

        public ConditionShowIfAttribute(string memberName, float expectedValue)
            : this(memberName, expectedValue.ToString(System.Globalization.CultureInfo.InvariantCulture), ConditionShowMode.ShowWhenTrue)
        {
        }

        public ConditionShowIfAttribute(string memberName, float expectedValue, ConditionShowMode mode)
            : this(memberName, expectedValue.ToString(System.Globalization.CultureInfo.InvariantCulture), mode)
        {
        }

        public ConditionShowIfAttribute(string memberName, string expectedValue)
            : this(memberName, expectedValue, ConditionShowMode.ShowWhenTrue)
        {
        }

        public ConditionShowIfAttribute(string memberName, string expectedValue, ConditionShowMode mode)
        {
            MemberName = memberName;
            ExpectedValue = expectedValue;
            HasExpectedValue = true;
            Mode = mode;
        }
    }
}
