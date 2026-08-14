using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens the main menu when Unity starts without a saved scene selected.
/// </summary>
[InitializeOnLoad]
public static class EditorStartupScene
{
    private const string _mainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    static EditorStartupScene()
    {
        EditorApplication.delayCall += OpenMainMenuWhenUntitled;
    }

    /// <summary>
    /// Replaces Unity's clean untitled startup scene with the main-menu scene.
    /// </summary>
    private static void OpenMainMenuWhenUntitled()
    {
        if (
            EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
        )
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (
            !activeScene.IsValid()
            || !string.IsNullOrEmpty(activeScene.path)
            || activeScene.isDirty
            || !File.Exists(_mainMenuScenePath)
        )
            return;

        EditorSceneManager.OpenScene(_mainMenuScenePath, OpenSceneMode.Single);
    }
}
