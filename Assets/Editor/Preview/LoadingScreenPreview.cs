using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens the loading screen by itself for an indefinite visual preview.
/// </summary>
[InitializeOnLoad]
public static class LoadingScreenPreview
{
    private const string _menuPath = "Rebellion/Preview/Loading Screen %#l";
    private const string _prefabPath = "Assets/Resources/UI/LoadingScreen.prefab";
    private const string _scenePath = "Assets/Scenes/LoadingScreen.unity";

    /// <summary>
    /// Initializes editor play-mode handling for the dedicated preview scene.
    /// </summary>
    static LoadingScreenPreview()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    /// <summary>
    /// Creates, opens, and plays the loading-screen preview scene.
    /// </summary>
    [MenuItem(_menuPath, false, 300)]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        CommonUIPrefabBuilder.BuildLoadingScreenPrefab();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Loading-screen prefab is missing: {_prefabPath}");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "LoadingScreen";
        if (!EditorSceneManager.SaveScene(scene, _scenePath))
            throw new IOException($"Could not save the loading-screen preview: {_scenePath}");

        Selection.activeGameObject = instance;
        EditorApplication.EnterPlaymode();
    }

    /// <summary>
    /// Loads the shuttle after the preview scene enters play mode.
    /// </summary>
    /// <param name="state">The editor play-mode transition.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (
            state != PlayModeStateChange.EnteredPlayMode
            || SceneManager.GetActiveScene().path != _scenePath
        )
            return;

        LoadingScreenView view = UnityEngine.Object.FindAnyObjectByType<LoadingScreenView>();
        if (view == null)
            throw new MissingReferenceException(
                "The loading-screen preview scene has no loading view."
            );

        view.Initialize(Path.Combine(Application.dataPath, "Content"));
    }
}
