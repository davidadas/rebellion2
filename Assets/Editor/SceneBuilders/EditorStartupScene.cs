using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens the committed boot scene when Unity starts without a saved scene selected.
/// </summary>
[InitializeOnLoad]
public static class EditorStartupScene
{
    private const string _bootScenePath = "Assets/Scenes/BootScene.unity";

    static EditorStartupScene()
    {
        EditorApplication.delayCall += OpenBootSceneWhenUntitled;
    }

    /// <summary>
    /// Replaces Unity's clean untitled startup scene with the committed boot scene.
    /// </summary>
    private static void OpenBootSceneWhenUntitled()
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
            || !File.Exists(_bootScenePath)
        )
            return;

        EditorSceneManager.OpenScene(_bootScenePath, OpenSceneMode.Single);
    }
}
