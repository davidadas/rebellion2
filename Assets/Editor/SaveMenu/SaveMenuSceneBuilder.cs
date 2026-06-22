using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SaveMenuSceneBuilder
{
    private const string _scenePath = "Assets/Scenes/SaveMenu.unity";
    private const string _saveMenuWindowPrefabPath = "Assets/Prefabs/UI/SaveMenuWindow.prefab";
    private const string _rootName = "SaveMenuRoot";
    private const string _contentHostName = "ContentHost";

    [MenuItem("Rebellion/Save Menu/Rebuild Save Menu Scene")]
    public static void RebuildSaveMenuScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_scenePath));
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject saveMenuWindowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            _saveMenuWindowPrefabPath
        );
        if (saveMenuWindowPrefab == null)
            throw new FileNotFoundException(_saveMenuWindowPrefabPath);

        CreateCamera();
        CreateEventSystem();
        Canvas canvas = CreateCanvas();
        SaveMenuSceneController controller = CreateRoot(canvas.transform);
        RectTransform contentHost = CreateContentHost(controller.transform);
        SaveMenuWindowView saveMenuWindow = CreateSaveMenuWindow(saveMenuWindowPrefab, contentHost);

        AssignReference(controller, "contentHost", contentHost);
        AssignReference(controller, "saveMenuWindow", saveMenuWindow);

        EditorSceneManager.SaveScene(scene, _scenePath);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        SetLayerRecursively(canvasObject, LayerMask.NameToLayer("UI"));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        return canvas;
    }

    private static SaveMenuSceneController CreateRoot(Transform parent)
    {
        GameObject root = new GameObject(_rootName, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
        FillParent(root.GetComponent<RectTransform>());

        GameObject background = new GameObject(
            "BackgroundImage",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage)
        );
        background.transform.SetParent(root.transform, false);
        RawImage backgroundImage = background.GetComponent<RawImage>();
        backgroundImage.color = Color.black;
        backgroundImage.raycastTarget = true;
        FillParent(background.GetComponent<RectTransform>());

        return root.AddComponent<SaveMenuSceneController>();
    }

    private static RectTransform CreateContentHost(Transform parent)
    {
        GameObject contentHost = new GameObject(_contentHostName, typeof(RectTransform));
        contentHost.transform.SetParent(parent, false);
        SetLayerRecursively(contentHost, LayerMask.NameToLayer("UI"));
        RectTransform rect = contentHost.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static SaveMenuWindowView CreateSaveMenuWindow(
        GameObject prefab,
        RectTransform contentHost
    )
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, contentHost);
        instance.name = "SaveMenuWindow";
        SetLayerRecursively(instance, LayerMask.NameToLayer("UI"));
        SaveMenuWindowView view = instance.GetComponent<SaveMenuWindowView>();
        RectTransform rect = instance.GetComponent<RectTransform>();
        Vector2 size = GetSourceSize(prefab.transform as RectTransform);
        SetSourceRect(rect, 0, 0, size);
        contentHost.sizeDelta = size;
        return view;
    }

    private static Vector2 GetSourceSize(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        Vector2 size = rect.sizeDelta;
        if (size.x <= 0f)
            size.x = rect.rect.width;
        if (size.y <= 0f)
            size.y = rect.rect.height;

        return size;
    }

    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == _scenePath))
            return;

        EditorBuildSettings.scenes = scenes
            .Concat(new[] { new EditorBuildSettingsScene(_scenePath, true) })
            .ToArray();
    }

    private static void AssignReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FillParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetSourceRect(RectTransform rect, int x, int y, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        if (gameObject == null || layer < 0)
            return;

        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
