using System;

namespace UnityEditor
{
    public static class AssetDatabase
    {
        public static void Refresh(ImportAssetOptions options)
        {
            _ = options;
        }
    }

    public static class EditorApplication
    {
        public static event Action update
        {
            add { _ = value; }
            remove { _ = value; }
        }

        public static bool isCompiling { get; set; }

        public static double timeSinceStartup { get; set; }

        public static void Exit(int exitCode)
        {
            _ = exitCode;
        }
    }

    [Flags]
    public enum ImportAssetOptions
    {
        ForceUpdate = 1,
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string itemName)
        {
            _ = itemName;
        }
    }

    public static class Selection
    {
        public static UnityEngine.Transform activeTransform { get; set; }
    }
}
