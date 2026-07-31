using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Removes editor-preview references to Assets/Content from the temporary scene copies Unity
/// serializes into a player. Runtime controllers replace these previews from installation Content.
/// </summary>
public sealed class DevelopmentContentBuildStripper : IProcessSceneWithReport
{
    private const string _developmentContentRoot = "Assets/Content/";

    public int callbackOrder => 0;

    /// <summary>
    /// Temporarily removes preview references from generated UI prefabs while Unity collects
    /// player dependencies. Disposing the returned scope restores the exact original files.
    /// </summary>
    public static IDisposable StripPrefabPreviews()
    {
        return new PrefabPreviewStripScope();
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                StripPreviewReferences(component);
        }
    }

    private static bool StripPreviewReferences(Component component)
    {
        if (component == null)
            return false;

        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        bool changed = false;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            UnityEngine.Object value = property.objectReferenceValue;
            if (value == null)
                continue;

            string assetPath = AssetDatabase.GetAssetPath(value);
            if (!assetPath.StartsWith(_developmentContentRoot, System.StringComparison.Ordinal))
                continue;

            property.objectReferenceValue = null;
            changed = true;
        }

        if (changed)
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    private sealed class PrefabPreviewStripScope : IDisposable
    {
        private readonly Dictionary<string, byte[]> originalFiles = new Dictionary<string, byte[]>(
            StringComparer.Ordinal
        );
        private bool disposed;

        public PrefabPreviewStripScope()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StripPrefab(path);
            }

            AssetDatabase.SaveAssets();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            try
            {
                foreach (KeyValuePair<string, byte[]> file in originalFiles)
                    File.WriteAllBytes(file.Key, file.Value);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                disposed = true;
            }
        }

        private void StripPrefab(string assetPath)
        {
            string absolutePath = Path.GetFullPath(assetPath);
            GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
            bool changed = false;
            try
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    changed |= StripPreviewReferences(component);

                if (!changed)
                    return;

                originalFiles.Add(absolutePath, File.ReadAllBytes(absolutePath));
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
