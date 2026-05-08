using UnityEngine;

namespace AniDrag.Utility
{
    /// <summary>
    /// Show the field in the inspector only if the given expression evaluates to true.
    /// Example: [ShowIf("resolvTyoe == ButtonActivationResult.DissableChildObject || resolvTyoe == ButtonActivationResult.DissableGrandChildObject")]
    /// </summary>
    public class ShowIfAttribute : PropertyAttribute
    {
        public string expression;

        public ShowIfAttribute(string expression)
        {
            this.expression = expression;
        }
    }
}
