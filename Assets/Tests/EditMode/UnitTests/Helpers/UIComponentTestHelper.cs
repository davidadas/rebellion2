using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEngine;

public static class UIComponentTestHelper
{
    public static GameObject InstantiatePrefab(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Missing test prefab at {prefabPath}.");

        return UnityEngine.Object.Instantiate(prefab);
    }

    public static T InstantiatePrefabComponent<T>(string prefabPath)
        where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        T component = prefab == null ? null : prefab.GetComponentInChildren<T>(true);
        if (component == null)
            throw new InvalidOperationException(
                $"Missing {typeof(T).Name} test component in {prefabPath}."
            );

        return UnityEngine.Object.Instantiate(component);
    }

    public static void InvokeLifecycle(Component component, string methodName)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));

        MethodInfo method = component
            .GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(component.GetType().FullName, methodName);

        try
        {
            method.Invoke(component, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
