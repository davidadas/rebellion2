using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The only Unity menu surface for rebuilding generated UI.
/// </summary>
public static class UIBuilderMenu
{
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

    [MenuItem("Rebellion/UI/Build Main Menu", false, 20)]
    public static void BuildMainMenu()
    {
        UIAuthoringGuard.EnsureEditMode();
        MainMenuPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    [MenuItem("Rebellion/UI/Build Save Game", false, 21)]
    public static void BuildSaveGame()
    {
        UIAuthoringGuard.EnsureEditMode();
        SaveMenuPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    [MenuItem("Rebellion/UI/Build Strategy", false, 22)]
    public static void BuildStrategy()
    {
        UIAuthoringGuard.EnsureEditMode();
        StrategyViewPrefabBuilder.Rebuild();
        SaveAndRefresh();
    }

    private static void SaveAndRefresh()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

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
