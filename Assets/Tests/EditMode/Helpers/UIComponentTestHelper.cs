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

        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        ApplyApplicationTextures(instance);
        return instance;
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

        T instance = UnityEngine.Object.Instantiate(component);
        ApplyApplicationTextures(instance.transform.root.gameObject);
        return instance;
    }

    public static void InvokeLifecycle(Component component, string methodName)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));

        ApplyApplicationTextures(component.transform.root.gameObject);

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

    private static void ApplyApplicationTextures(GameObject root)
    {
        foreach (
            ApplicationTextureBindings bindings in root.GetComponentsInChildren<ApplicationTextureBindings>(
                true
            )
        )
        {
            bindings.Apply(TestContent.Assets);
        }
    }
}
