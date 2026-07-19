using System.IO;
using UnityEditor;

/// <summary>
/// Installs the authored main-menu root prefab in the MainMenu scene.
/// </summary>
public static class MainMenuSceneBuilder
{
    private const string _scenePath = "Assets/Scenes/MainMenu.unity";
    private const string _prefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";
    private const string _sceneInstanceName = "MainMenuRoot";

    /// <summary>
    /// Rebuilds view bindings and installs the authored main-menu root in its scene.
    /// </summary>
    [MenuItem("Rebellion/Main Menu/Install Main Menu Root Prefab In Scene")]
    public static void InstallMainMenuRootPrefabInScene()
    {
        UIAuthoringGuard.EnsureEditMode();
        MainMenuPrefabAuthoring.RebuildMainMenuViewBindings();

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(_prefabPath) == null)
            throw new FileNotFoundException(_prefabPath);

        SceneRootPrefabInstaller.InstallRootPrefabInScene(
            _scenePath,
            _prefabPath,
            _sceneInstanceName,
            null
        );
    }
}
