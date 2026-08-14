using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Generates the boot scene and the runtime cutscene presentation assets it consumes.
/// </summary>
public static class BootPrefabBuilder
{
    private const int _cutsceneHeight = 1944;
    private const int _cutsceneWidth = 3840;
    private const string _bootPrefabPath = "Assets/Prefabs/UI/Boot/BootRoot.prefab";
    private const string _bootScenePath = "Assets/Scenes/BootScene.unity";
    private const string _cutscenePrefabPath = "Assets/Prefabs/UI/Cutscenes/CutscenePlayer.prefab";
    private const string _cutsceneRenderTexturePath =
        "Assets/Prefabs/UI/Cutscenes/CutsceneRenderTexture.renderTexture";

    /// <summary>
    /// Rebuilds the complete boot asset graph from code.
    /// </summary>
    public static void Rebuild()
    {
        RenderTexture renderTexture = BuildRenderTexture();
        GameObject cutscenePrefab = BuildCutscenePrefab(renderTexture);
        BuildBootPrefab(cutscenePrefab);
        SceneBuilder.Build(_bootScenePath, _bootPrefabPath, "BootRoot");
    }

    /// <summary>
    /// Creates or updates the render target used by cutscene video playback.
    /// </summary>
    private static RenderTexture BuildRenderTexture()
    {
        EnsureDirectory(_cutsceneRenderTexturePath);
        RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
            _cutsceneRenderTexturePath
        );
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(
                _cutsceneWidth,
                _cutsceneHeight,
                24,
                RenderTextureFormat.ARGB32
            )
            {
                name = "CutsceneRenderTexture",
            };
            AssetDatabase.CreateAsset(renderTexture, _cutsceneRenderTexturePath);
        }

        renderTexture.Release();
        renderTexture.width = _cutsceneWidth;
        renderTexture.height = _cutsceneHeight;
        renderTexture.antiAliasing = 1;
        renderTexture.useMipMap = false;
        renderTexture.autoGenerateMips = false;
        renderTexture.filterMode = FilterMode.Bilinear;
        renderTexture.wrapMode = TextureWrapMode.Clamp;
        EditorUtility.SetDirty(renderTexture);
        return renderTexture;
    }

    /// <summary>
    /// Authors the self-contained overlay used for one cutscene playback request.
    /// </summary>
    private static GameObject BuildCutscenePrefab(RenderTexture renderTexture)
    {
        GameObject root = new GameObject(
            "CutscenePlayer",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(VideoPlayer),
            typeof(AudioSource),
            typeof(CutscenePlayer)
        );
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            FillParent(rootRect);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            VideoPlayer videoPlayer = root.GetComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.skipOnDrop = true;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            videoPlayer.targetTexture = renderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

            AudioSource audioSource = root.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;

            GameObject screenObject = new GameObject(
                "VideoScreenImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage)
            );
            RectTransform screenRect = screenObject.GetComponent<RectTransform>();
            screenRect.SetParent(rootRect, false);
            FillParent(screenRect);
            RawImage screen = screenObject.GetComponent<RawImage>();
            screen.texture = renderTexture;
            screen.color = Color.white;
            screen.raycastTarget = true;

            CutscenePlayer player = root.GetComponent<CutscenePlayer>();
            AssignReference(player, "screen", screen);
            AssignReference(player, "videoPlayer", videoPlayer);
            AssignReference(player, "audioSource", audioSource);

            return SavePrefab(root, _cutscenePrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Authors the complete root used by the generated boot scene.
    /// </summary>
    private static void BuildBootPrefab(GameObject cutscenePrefab)
    {
        GameObject root = new GameObject("BootRoot");
        try
        {
            GameObject controllers = new GameObject("Controllers");
            controllers.transform.SetParent(root.transform, false);
            BootController bootController = controllers.AddComponent<BootController>();
            AssignReference(bootController, "cutscenePlayerPrefab", cutscenePrefab);

            GameObject cameraObject = new GameObject(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener)
            );
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.depth = -1f;

            GameObject eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule)
            );
            eventSystem.transform.SetParent(root.transform, false);

            SavePrefab(root, _bootPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Assigns one private serialized object reference.
    /// </summary>
    private static void AssignReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                $"{target.GetType().Name}.{propertyName} is not serialized."
            );

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Makes a generated rectangle cover its parent canvas.
    /// </summary>
    private static void FillParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// Saves one generated prefab and verifies the result.
    /// </summary>
    private static GameObject SavePrefab(GameObject root, string path)
    {
        EnsureDirectory(path);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        if (!success || saved == null)
            throw new IOException($"Could not generate prefab: {path}");

        return saved;
    }

    /// <summary>
    /// Creates the destination directory for a generated asset when needed.
    /// </summary>
    private static void EnsureDirectory(string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException($"Asset path has no directory: {assetPath}");

        Directory.CreateDirectory(directory);
    }
}
