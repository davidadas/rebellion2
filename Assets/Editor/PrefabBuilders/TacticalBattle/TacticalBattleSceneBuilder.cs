using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Generates the tactical space-battle scene from code-owned defaults.
/// </summary>
public static class TacticalBattleSceneBuilder
{
    private const string _scenePath = "Assets/Scenes/TacticalBattle.unity";

    /// <summary>
    /// Rebuilds the tactical battle scene and enables it for player builds.
    /// </summary>
    public static void Rebuild()
    {
        UIAuthoringGuard.EnsureEditMode();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = TacticalBattleLaunchContext.SceneName;

        ConfigureEnvironment();
        CreateSceneController();
        CreateCamera();
        CreateLight();

        Directory.CreateDirectory(Path.GetDirectoryName(_scenePath) ?? "Assets/Scenes");
        if (!EditorSceneManager.SaveScene(scene, _scenePath, true))
            throw new IOException($"Could not generate scene: {_scenePath}");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EnableBuildScene();
    }

    /// <summary>
    /// Configures the scene's neutral space-lighting defaults.
    /// </summary>
    private static void ConfigureEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.12f, 1f);
        RenderSettings.fog = false;
        RenderSettings.skybox = null;
    }

    /// <summary>
    /// Creates the component that owns tactical scene state.
    /// </summary>
    private static void CreateSceneController()
    {
        GameObject root = new GameObject(TacticalBattleLaunchContext.SceneName);
        root.AddComponent<TacticalBattleController>();
    }

    /// <summary>
    /// Creates the tactical presentation camera.
    /// </summary>
    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("TacticalCamera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 10000f;
        camera.fieldOfView = 45f;
        cameraObject.transform.SetPositionAndRotation(
            new Vector3(0f, 80f, -240f),
            Quaternion.Euler(15f, 0f, 0f)
        );
        cameraObject.AddComponent<AudioListener>();
    }

    /// <summary>
    /// Creates the primary tactical scene light.
    /// </summary>
    private static void CreateLight()
    {
        GameObject lightObject = new GameObject("TacticalKeyLight");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1f;
        lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
    }

    /// <summary>
    /// Adds the generated scene to the enabled player-build scene list once.
    /// </summary>
    private static void EnableBuildScene()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => string.Equals(scene.path, _scenePath, StringComparison.Ordinal)))
            return;

        EditorBuildSettings.scenes = scenes
            .Concat(new[] { new EditorBuildSettingsScene(_scenePath, true) })
            .ToArray();
    }
}
