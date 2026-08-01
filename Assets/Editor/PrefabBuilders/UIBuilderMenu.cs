using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The only Unity menu surface for rebuilding generated UI.
/// </summary>
public static class UIBuilderMenu
{
    /// <summary>
    /// Rebuilds every generated UI prefab and scene.
    /// </summary>
    [MenuItem("Rebellion/UI/Build All", false, 0)]
    public static void BuildAll()
    {
        UIAuthoringGuard.EnsureEditMode();
        MainMenuPrefabBuilder.Rebuild();
        SaveMenuPrefabBuilder.Rebuild();
        StrategyViewPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    /// <summary>
    /// Generates the UI for a player build, then removes development-only Content references from
    /// the generated prefabs. Generated prefab payloads are ignored by Git, so no restoration is
    /// required after the build.
    /// </summary>
    public static void BuildAllForPlayer()
    {
        BuildAll();

        string[] prefabGuids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[]
            {
                "Assets/Prefabs/UI/Common",
                "Assets/Prefabs/UI/MainMenu",
                "Assets/Prefabs/UI/SaveMenu",
                "Assets/Prefabs/UI/StrategyView",
            }
        );
        foreach (string prefabGuid in prefabGuids)
            RemoveDevelopmentContentReferences(AssetDatabase.GUIDToAssetPath(prefabGuid));

        SaveAndRefresh();
    }

    /// <summary>
    /// Rebuilds the generated main-menu UI.
    /// </summary>
    [MenuItem("Rebellion/UI/Build Main Menu", false, 20)]
    public static void BuildMainMenu()
    {
        UIAuthoringGuard.EnsureEditMode();
        MainMenuPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    /// <summary>
    /// Rebuilds the generated save-game UI.
    /// </summary>
    [MenuItem("Rebellion/UI/Build Save Game", false, 21)]
    public static void BuildSaveGame()
    {
        UIAuthoringGuard.EnsureEditMode();
        SaveMenuPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    /// <summary>
    /// Rebuilds the generated strategy UI.
    /// </summary>
    [MenuItem("Rebellion/UI/Build Strategy", false, 22)]
    public static void BuildStrategy()
    {
        UIAuthoringGuard.EnsureEditMode();
        StrategyViewPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    /// <summary>
    /// Saves generated assets and refreshes the asset database.
    /// </summary>
    private static void SaveAndRefresh()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Removes development-content references from one generated prefab.
    /// </summary>
    /// <param name="prefabPath">The generated prefab path.</param>
    private static void RemoveDevelopmentContentReferences(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        bool changed = false;
        try
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                changed |= RemoveDevelopmentContentReferences(component);

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Removes development-content references serialized by one component.
    /// </summary>
    /// <param name="component">The component to inspect.</param>
    /// <returns>True when at least one reference was removed.</returns>
    private static bool RemoveDevelopmentContentReferences(Component component)
    {
        if (component == null)
            return false;

        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        bool changed = false;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = property.propertyType == SerializedPropertyType.Generic;
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            UnityEngine.Object value = property.objectReferenceValue;
            if (value == null)
                continue;

            string assetPath = AssetDatabase.GetAssetPath(value);
            if (!assetPath.StartsWith("Assets/Content/", StringComparison.Ordinal))
                continue;

            property.objectReferenceValue = null;
            changed = true;
        }

        if (changed)
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }
}
