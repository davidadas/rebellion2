using System.IO;
using System.Linq;
using UnityEditor;

/// <summary>
/// Installs the authored Save Menu root prefab into the existing Save Menu scene shell.
/// </summary>
public static class SaveMenuSceneBuilder
{
    private const string _scenePath = "Assets/Scenes/SaveMenu.unity";
    private const string _rootPrefabPath = "Assets/Prefabs/UI/SaveMenu/SaveMenuRoot.prefab";
    private const string _rootName = "SaveMenuRoot";
    private const string _canvasPath = "Canvas";

    /// <summary>
    /// Rebuilds the Save Menu prefab family and replaces only its scene-root instance.
    /// </summary>
    [MenuItem("Rebellion/Save Menu/Rebuild Save Menu Scene")]
    public static void RebuildSaveMenuScene()
    {
        UIAuthoringGuard.EnsureEditMode();
        if (!File.Exists(_scenePath))
            throw new FileNotFoundException(_scenePath);

        SaveMenuPrefabBuilder.RebuildAllSaveMenuPrefabs();
        SceneRootPrefabInstaller.InstallRootPrefabInScene(
            _scenePath,
            _rootPrefabPath,
            _rootName,
            _canvasPath
        );
        AddSceneToBuildSettings();
    }

    /// <summary>
    /// Adds the Save Menu scene to build settings without changing existing entries.
    /// </summary>
    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == _scenePath))
            return;

        EditorBuildSettings.scenes = scenes
            .Concat(new[] { new EditorBuildSettingsScene(_scenePath, true) })
            .ToArray();
    }
}
