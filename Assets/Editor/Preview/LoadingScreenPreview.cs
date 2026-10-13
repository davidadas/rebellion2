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
    private const string _sceneRootName = "LoadingScreen";

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
        SceneBuilder.Build(_scenePath, _prefabPath, _sceneRootName);
        Scene scene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Single);
        Selection.activeGameObject = FindLoadingScreenRoot(scene);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>
    /// Finds the generated loading-screen root in the preview scene.
    /// </summary>
    /// <param name="scene">The generated preview scene.</param>
    /// <returns>The loading-screen root.</returns>
    private static GameObject FindLoadingScreenRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == _sceneRootName)
                return root;
        }

        throw new MissingReferenceException(
            $"The loading-screen preview scene has no {_sceneRootName} root: {_scenePath}"
        );
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
