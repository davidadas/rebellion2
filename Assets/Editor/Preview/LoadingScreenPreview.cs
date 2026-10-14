using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens the loading screen by itself and simulates its loading-to-ready handoff.
/// </summary>
[InitializeOnLoad]
public static class LoadingScreenPreview
{
    private const string _menuPath = "Rebellion/Preview/Loading Screen %#l";
    private const string _scenePath = "Assets/Scenes/LoadingScreen.unity";
    private const string _sceneRootName = "LoadingScreen";
    private const double _readyDelaySeconds = 3d;

    private static LoadingScreenView _previewView;
    private static double _readyAt;

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

        LoadingScreenPrefabBuilder.Rebuild();
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
    /// Loads the presentation and schedules its ready state after preview play mode begins.
    /// </summary>
    /// <param name="state">The editor play-mode transition.</param>
    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            StopReadySimulation();
            return;
        }
        if (
            state != PlayModeStateChange.EnteredPlayMode
            || SceneManager.GetActiveScene().path != _scenePath
        )
            return;

        _previewView = UnityEngine.Object.FindAnyObjectByType<LoadingScreenView>();
        if (_previewView == null)
            throw new MissingReferenceException(
                "The loading-screen preview scene has no loading view."
            );

        _previewView.Initialize(Path.Combine(Application.dataPath, "Content"));
        _previewView.SetVisible(true);
        _readyAt = EditorApplication.timeSinceStartup + _readyDelaySeconds;
        EditorApplication.update -= AdvanceReadySimulation;
        EditorApplication.update += AdvanceReadySimulation;
    }

    /// <summary>
    /// Advances the standalone preview from loading to its player-controlled ready state.
    /// </summary>
    private static void AdvanceReadySimulation()
    {
        if (!EditorApplication.isPlaying || _previewView == null)
        {
            StopReadySimulation();
            return;
        }
        if (EditorApplication.timeSinceStartup < _readyAt)
            return;

        _previewView.ShowReady();
        EditorApplication.update -= AdvanceReadySimulation;
    }

    /// <summary>
    /// Clears the editor-only ready-state simulation.
    /// </summary>
    private static void StopReadySimulation()
    {
        EditorApplication.update -= AdvanceReadySimulation;
        _previewView = null;
        _readyAt = 0d;
    }
}
