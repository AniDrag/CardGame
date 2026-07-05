#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AniDrag.Utility.Inspector.Editor
{
    /// <summary>
    /// Base editor you can inherit from when you write a custom inspector.
    /// </summary>
    public class AniDragUtilityEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            AniDragInspectorUtility.DrawAniDragInspector(this);
        }
    }

    /// <summary>
    /// Auto-applies the AniDrag inspector to MonoBehaviours that do not have their own custom editor.
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
    [CanEditMultipleObjects]
    public sealed class AniDragMonoBehaviourEditor : AniDragUtilityEditor
    {
    }

    /// <summary>
    /// Auto-applies the AniDrag inspector to ScriptableObjects that do not have their own custom editor.
    /// </summary>
    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    [CanEditMultipleObjects]
    public sealed class AniDragScriptableObjectEditor : AniDragUtilityEditor
    {
    }
}
#endif
