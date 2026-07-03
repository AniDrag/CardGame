using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class ViewAutoFind
{
    public static GameObject FindGameObject(Transform root, params string[] names)
    {
        Transform found = FindTransform(root, names);
        return found != null ? found.gameObject : null;
    }

    public static Transform FindTransform(Transform root, params string[] names)
    {
        if (root == null || names == null)
            return null;

        foreach (string name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            Transform found = FindTransformExact(root, name);
            if (found != null)
                return found;
        }

        return null;
    }

    public static T FindComponentByNames<T>(Transform root, params string[] names) where T : Component
    {
        Transform found = FindTransform(root, names);
        if (found == null)
            return null;

        return found.GetComponent<T>() ?? found.GetComponentInChildren<T>(true);
    }

    public static T FindComponentContainingAll<T>(Transform root, params string[] keywords) where T : Component
    {
        if (root == null || keywords == null || keywords.Length == 0)
            return null;

        T[] components = root.GetComponentsInChildren<T>(true);

        foreach (T component in components)
        {
            string objectName = component.gameObject.name.ToLowerInvariant();
            bool match = true;

            foreach (string keyword in keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    continue;

                if (!objectName.Contains(keyword.ToLowerInvariant()))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return component;
        }

        return null;
    }

    public static T FindFirstComponent<T>(Transform root) where T : Component
    {
        if (root == null)
            return null;

        return root.GetComponentInChildren<T>(true);
    }

    private static Transform FindTransformExact(Transform root, string name)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindTransformExact(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}
