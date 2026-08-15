using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The only Unity menu surface for rebuilding generated UI.
/// </summary>
public static class UIBuilderMenu
{
    private const string _mainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    /// <summary>
    /// Rebuilds every generated UI prefab and scene.
    /// </summary>
    [MenuItem("Rebellion/Build/Build All UI %#u", false, 0)]
    public static void BuildAll()
    {
        RunInAuthoringScene(() =>
        {
            BootPrefabBuilder.Rebuild();
            RebuildOptionsMenuAndDependencies();
            MainMenuPrefabBuilder.Rebuild();
            StrategyViewPrefabBuilder.Rebuild();
        });
        SaveAndRefresh();
    }

    /// <summary>
    /// Generates the UI for a player build, then removes development-only Content references from
    /// the generated prefabs. Generated prefab payloads are ignored by Git, so no restoration is
    /// required after the build.
    /// </summary>
    [MenuItem("Rebellion/Build/Build Runtime UI", false, 1)]
    public static void BuildRuntimeUI()
    {
        BuildAll();

        string[] prefabGuids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[]
            {
                "Assets/Prefabs/UI/Boot",
                "Assets/Prefabs/UI/Common",
                "Assets/Prefabs/UI/Cutscenes",
                "Assets/Prefabs/UI/MainMenu",
                "Assets/Prefabs/UI/OptionsMenu",
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
    [MenuItem("Rebellion/Build/Build Main Menu UI", false, 20)]
    public static void BuildMainMenu()
    {
        RunInAuthoringScene(() =>
        {
            RebuildOptionsMenuAndDependencies();
            MainMenuPrefabBuilder.Rebuild();
        });
        SaveAndRefresh();
    }

    /// <summary>
    /// Rebuilds the generated Options menu UI.
    /// </summary>
    [MenuItem("Rebellion/Build/Build Options Menu UI", false, 21)]
    public static void BuildOptionsMenu()
    {
        RunInAuthoringScene(RebuildOptionsMenuAndDependencies);
        SaveAndRefresh();
    }

    /// <summary>
    /// Rebuilds the generated strategy UI.
    /// </summary>
    [MenuItem("Rebellion/Build/Build Strategy UI", false, 22)]
    public static void BuildStrategy()
    {
        RunInAuthoringScene(() =>
        {
            RebuildOptionsMenuAndDependencies();
            StrategyViewPrefabBuilder.Rebuild();
        });
        SaveAndRefresh();
    }

    /// <summary>
    /// Rebuilds the boot scene and its generated prefabs.
    /// </summary>
    [MenuItem("Rebellion/Build/Build Boot UI", false, 23)]
    public static void BuildBoot()
    {
        RunInAuthoringScene(BootPrefabBuilder.Rebuild);
        SaveAndRefresh();
    }

    /// <summary>
    /// Isolates temporary prefab-authoring objects from the user's open scenes and restores editor focus afterward.
    /// </summary>
    /// <param name="build">The prefab and generated-scene work to perform.</param>
    private static void RunInAuthoringScene(Action build)
    {
        if (build == null)
            throw new ArgumentNullException(nameof(build));

        if (
            !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()
        )
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        bool startedFromUntitledScene = string.IsNullOrEmpty(activeScene.path);
        UnityEngine.Object[] selection = Selection.objects;
        bool usesBatchScene = Application.isBatchMode && startedFromUntitledScene;
        Scene authoringScene =
            usesBatchScene || startedFromUntitledScene
                ? activeScene
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string authoringScenePath = null;
        if (!usesBatchScene)
        {
            authoringScenePath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/Scenes/UIBuilderAuthoringTemp.unity"
            );
            if (!EditorSceneManager.SaveScene(authoringScene, authoringScenePath))
                throw new InvalidOperationException(
                    "Could not create the temporary UI authoring scene."
                );
            SceneManager.SetActiveScene(authoringScene);
        }
        try
        {
            build();
        }
        finally
        {
            if (usesBatchScene)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                if (!startedFromUntitledScene && activeScene.IsValid() && activeScene.isLoaded)
                    SceneManager.SetActiveScene(activeScene);
                if (
                    !startedFromUntitledScene
                    && authoringScene.IsValid()
                    && authoringScene.isLoaded
                )
                    EditorSceneManager.CloseScene(authoringScene, true);
                if (startedFromUntitledScene)
                {
                    if (File.Exists(_mainMenuScenePath))
                        EditorSceneManager.OpenScene(_mainMenuScenePath, OpenSceneMode.Single);
                    else
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                if (!string.IsNullOrEmpty(authoringScenePath))
                    AssetDatabase.DeleteAsset(authoringScenePath);
            }
            Selection.objects = selection;
        }
    }

    /// <summary>
    /// Rebuilds the shared controls before the Options menu that consumes them.
    /// </summary>
    private static void RebuildOptionsMenuAndDependencies()
    {
        CommonUIPrefabBuilder.RebuildSharedControlPrefabs();
        OptionsMenuPrefabBuilder.Rebuild();
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
            VerifyRuntimeContentBindings(root, prefabPath);
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
    /// Rejects authored content references whose runtime restoration address is incomplete.
    /// </summary>
    /// <param name="root">The generated prefab hierarchy.</param>
    /// <param name="prefabPath">The prefab path reported when validation fails.</param>
    private static void VerifyRuntimeContentBindings(GameObject root, string prefabPath)
    {
        foreach (
            ContentPressVisualBinding binding in root.GetComponentsInChildren<ContentPressVisualBinding>(
                true
            )
        )
        {
            RawImagePressVisual pressVisual = binding.GetComponent<RawImagePressVisual>();
            SerializedProperty downTexture = new SerializedObject(pressVisual).FindProperty(
                "downTexture"
            );
            if (
                IsDevelopmentContentReference(downTexture?.objectReferenceValue)
                && string.IsNullOrEmpty(binding.DownAddress)
            )
                throw new InvalidOperationException(
                    $"{prefabPath}/{binding.name} has a stripped pressed texture without a runtime address."
                );
        }

        foreach (Scrollbar scrollbar in root.GetComponentsInChildren<Scrollbar>(true))
        {
            RawImage handle = scrollbar.handleRect?.GetComponent<RawImage>();
            if (
                handle != null
                && IsDevelopmentContentReference(handle.texture)
                && handle.GetComponent<ContentTextureBinding>() == null
            )
                throw new InvalidOperationException(
                    $"{prefabPath}/{handle.name} has a stripped scrollbar texture without a runtime binding."
                );
        }
    }

    /// <summary>
    /// Determines whether an object reference points into development-only installation content.
    /// </summary>
    /// <param name="value">The serialized object reference.</param>
    /// <returns>True when the object is imported from Assets/Content.</returns>
    private static bool IsDevelopmentContentReference(UnityEngine.Object value)
    {
        return value != null
            && AssetDatabase
                .GetAssetPath(value)
                .StartsWith("Assets/Content/", StringComparison.Ordinal);
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
