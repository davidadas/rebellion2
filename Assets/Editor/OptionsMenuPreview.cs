using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor-only utility that regenerates the Options overlay prefab and renders each of its pages
/// to a PNG so the authored layout can be reviewed without entering Play mode.
/// </summary>
public static class OptionsMenuPreview
{
    private const string _prefabPath = "Assets/Prefabs/UI/OptionsMenu/OptionsMenu.prefab";
    private const string _outputDirectory =
        "C:/Users/David/Downloads/optionsmenu_mockup";

    /// <summary>
    /// Regenerates the Strategy UI and captures a preview image of every Options page.
    /// </summary>
    public static void Capture()
    {
        UIBuilderMenu.BuildStrategy();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"OptionsMenuPreview: prefab not found at {_prefabPath}");
            return;
        }

        Directory.CreateDirectory(_outputDirectory);
        foreach (OptionsMenuTab tab in System.Enum.GetValues(typeof(OptionsMenuTab)))
            CaptureTab(prefab, tab);
    }

    /// <summary>
    /// Renders one Options page to a PNG.
    /// </summary>
    /// <param name="prefab">The Options overlay prefab.</param>
    /// <param name="tab">The page to display.</param>
    private static void CaptureTab(GameObject prefab, OptionsMenuTab tab)
    {
        const int rtWidth = 1706;
        const int rtHeight = 960;
        const float surfaceWidth = 853.33f;
        const float surfaceHeight = 480f;

        // The real strategy WindowBounds that clamp modal windows in-game (853x480 source space).
        const float boundsX = 55f;
        const float boundsY = 36f;
        const float boundsWidth = 693f;
        const float boundsHeight = 349f;

        RenderTexture renderTexture = new RenderTexture(rtWidth, rtHeight, 24);
        GameObject cameraObject = new GameObject("OptionsPreviewCamera");
        GameObject canvasObject = new GameObject("OptionsPreviewCanvas");
        GameObject instance = null;
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = surfaceHeight / 2f;
            camera.aspect = (float)rtWidth / rtHeight;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.targetTexture = renderTexture;

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(surfaceWidth, surfaceHeight);
            canvasRect.position = Vector3.zero;

            // Visualize the surface and the WindowBounds so any clipping/off-centering is obvious.
            CreatePreviewRect(canvas.transform, "Surface", 0f, 0f, surfaceWidth, surfaceHeight,
                new Color(0.06f, 0.08f, 0.11f));
            CreatePreviewRect(canvas.transform, "Bounds", boundsX, boundsY, boundsWidth, boundsHeight,
                new Color(0.10f, 0.14f, 0.19f));

            instance = Object.Instantiate(prefab, canvas.transform);
            OptionsMenuView view = instance.GetComponent<OptionsMenuView>();
            RectTransform windowRect = instance.GetComponent<RectTransform>();
            view.Render(BuildSampleData(tab));

            // Place the window exactly where GetOptionsWindowPosition centers it: on the full surface.
            float windowWidth = windowRect.sizeDelta.x;
            float windowHeight = windowRect.sizeDelta.y;
            float windowX = Mathf.Round((surfaceWidth - windowWidth) / 2f);
            float windowY = Mathf.Round((surfaceHeight - windowHeight) / 2f);
            windowRect.anchorMin = new Vector2(0f, 1f);
            windowRect.anchorMax = new Vector2(0f, 1f);
            windowRect.pivot = new Vector2(0f, 1f);
            windowRect.anchoredPosition = new Vector2(windowX, -windowY);
            instance.SetActive(true);

            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(rtWidth, rtHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, rtWidth, rtHeight), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(
                Path.Combine(_outputDirectory, $"preview_{tab}.png"),
                texture.EncodeToPNG()
            );
            Object.DestroyImmediate(texture);
            Debug.Log($"OptionsMenuPreview: captured {tab}");
        }
        finally
        {
            if (instance != null)
                Object.DestroyImmediate(instance);
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(cameraObject);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
        }
    }

    /// <summary>
    /// Creates one flat top-left-anchored rectangle used to visualize the surface and window bounds.
    /// </summary>
    /// <param name="parent">The canvas transform.</param>
    /// <param name="name">The rectangle name.</param>
    /// <param name="x">The left offset in source units.</param>
    /// <param name="y">The top offset in source units.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="color">The fill color.</param>
    private static void CreatePreviewRect(
        Transform parent,
        string name,
        float x,
        float y,
        float width,
        float height,
        Color color
    )
    {
        GameObject rectObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        rectObject.transform.SetParent(parent, false);
        Image image = rectObject.GetComponent<Image>();
        image.color = color;
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>
    /// Builds representative presentation data for a preview page.
    /// </summary>
    /// <param name="tab">The page to display.</param>
    /// <returns>The sample presentation snapshot.</returns>
    private static OptionsMenuRenderData BuildSampleData(OptionsMenuTab tab)
    {
        Dictionary<UserTacticalOption, bool> tactical =
            new Dictionary<UserTacticalOption, bool>
            {
                { UserTacticalOption.Starfield, true },
                { UserTacticalOption.Planet, true },
                { UserTacticalOption.Pyro, false },
                { UserTacticalOption.HighDetail, true },
                { UserTacticalOption.Holocube, true },
            };
        float[] volumes = { 0.85f, 0.70f, 0.90f, 0.60f, 0.80f };
        List<OptionsBindingRow> bindings = new List<OptionsBindingRow>
        {
            new OptionsBindingRow("GLOBAL", string.Empty),
            new OptionsBindingRow("Cancel Or Settings", "Esc"),
            new OptionsBindingRow("Quick Save", "F4"),
            new OptionsBindingRow("Quick Load", "F9"),
            new OptionsBindingRow("Increase Game Speed", "Ctrl+="),
            new OptionsBindingRow("Decrease Game Speed", "Ctrl+-"),
            new OptionsBindingRow("STRATEGY", string.Empty),
            new OptionsBindingRow("Multi Select", "Ctrl"),
            new OptionsBindingRow("Range Select", "Shift"),
            new OptionsBindingRow("Alternate Select", "Alt"),
            new OptionsBindingRow("WINDOW", string.Empty),
            new OptionsBindingRow("Confirm", "Enter"),
            new OptionsBindingRow("Close", "Esc"),
        };
        List<OptionsSaveSlot> slots = new List<OptionsSaveSlot>
        {
            new OptionsSaveSlot("Create New Save", string.Empty, null, true, null),
            new OptionsSaveSlot("Battle of Endor", "2026-08-10 14:32", null, false, "save1"),
            new OptionsSaveSlot("Hoth Holdout", "2026-08-09 21:07", null, false, "save2"),
            new OptionsSaveSlot("Coruscant Gambit", "2026-08-08 10:55", null, false, "save3"),
        };
        return new OptionsMenuRenderData(
            0,
            0,
            tab,
            "1920 x 1080",
            "Fullscreen",
            tactical,
            volumes,
            bindings,
            slots,
            0,
            true,
            true,
            true,
            true,
            -1,
            false
        );
    }
}
