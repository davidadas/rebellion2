using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reviews and exports native multiplayer proposal prefabs through an isolated Unity camera.
/// </summary>
public sealed class MultiplayerProposalPreviewWindow : EditorWindow
{
    [SerializeField]
    private MultiplayerProposalLayout _layout;

    [SerializeField]
    private MultiplayerProposalPage _page;

    [SerializeField]
    private MultiplayerProposalTheme _theme;

    private Texture2D _rendered;

    /// <summary>
    /// Opens the native Unity proposal viewer without replacing the current scene.
    /// </summary>
    [MenuItem("Rebellion/Preview/Multiplayer proposals")]
    public static void Open()
    {
        MultiplayerProposalPreviewWindow window = GetWindow<MultiplayerProposalPreviewWindow>();
        window.titleContent = new GUIContent("Multiplayer proposals");
        window.minSize = new Vector2(1000, 780);
        window.position = new Rect(80, 80, 1440, 1040);
        window.Show();
        window.Focus();
    }

    /// <summary>
    /// Releases the owned screenshot when this native editor window closes or reloads.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnDisable()
    {
        if (_rendered != null)
            DestroyImmediate(_rendered);
    }

    /// <summary>
    /// Presents native layout selectors above the camera-rendered Unity canvas.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnGUI()
    {
        EditorGUILayout.Space(7);
        EditorGUILayout.LabelField("Native Unity multiplayer proposals", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Existing shared controls and faction artwork. Sample lobby data.",
            EditorStyles.miniLabel
        );
        EditorGUI.BeginChangeCheck();
        _layout = (MultiplayerProposalLayout)
            GUILayout.Toolbar(
                (int)_layout,
                new[] { "1  Wide console", "2  Split briefing", "3  Player stations" },
                GUILayout.Height(28)
            );
        EditorGUILayout.BeginHorizontal();
        _page = (MultiplayerProposalPage)
            GUILayout.Toolbar(
                (int)_page,
                new[] { "Server browser", "Create game", "Game lobby" },
                GUILayout.Height(25)
            );
        _theme = (MultiplayerProposalTheme)EditorGUILayout.EnumPopup(_theme, GUILayout.Width(160));
        EditorGUILayout.EndHorizontal();
        bool changed = EditorGUI.EndChangeCheck();
        if ((changed || _rendered == null) && !EditorApplication.isCompiling)
            RenderSelection();
        Rect available = GUILayoutUtility.GetRect(
            100,
            10000,
            100,
            10000,
            GUILayout.ExpandHeight(true)
        );
        if (_rendered != null)
            GUI.DrawTexture(available, _rendered, ScaleMode.ScaleToFit);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Open native prefab", GUILayout.Height(27)))
            OpenPrefab();
        if (GUILayout.Button("Export all proposal images", GUILayout.Height(27)))
            ExportAll();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(
            "These are Unity UI layout proposals; launching and chat are not connected to a live multiplayer session.",
            EditorStyles.miniLabel
        );
    }

    /// <summary>
    /// Rebuilds the selected native canvas and replaces this window's rendered preview.
    /// </summary>
    private void RenderSelection()
    {
        if (_rendered != null)
            DestroyImmediate(_rendered);
        _rendered = Render(_layout, _page, _theme, false);
        Repaint();
    }

    /// <summary>
    /// Opens the selected generated prefab for direct inspection in Unity's prefab editor.
    /// </summary>
    private void OpenPrefab()
    {
        GameObject root = MultiplayerProposalBuilder.Build(_layout, _page, _theme);
        try
        {
            string path = MultiplayerProposalBuilder.SavePrefab(root, _layout, _page, _theme);
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Exports all three screens and additional faction-themed lobby comparisons as native renders.
    /// </summary>
    public static void ExportAll()
    {
        string directory = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Design", "NativeMultiplayerProposals")
        );
        Directory.CreateDirectory(directory);
        foreach (
            MultiplayerProposalLayout selectedLayout in Enum.GetValues(
                typeof(MultiplayerProposalLayout)
            )
        )
        {
            foreach (
                MultiplayerProposalPage selectedPage in Enum.GetValues(
                    typeof(MultiplayerProposalPage)
                )
            )
                Export(
                    selectedLayout,
                    selectedPage,
                    MultiplayerProposalTheme.Application,
                    directory
                );
            Export(
                selectedLayout,
                MultiplayerProposalPage.Lobby,
                MultiplayerProposalTheme.Empire,
                directory
            );
            Export(
                selectedLayout,
                MultiplayerProposalPage.Lobby,
                MultiplayerProposalTheme.Alliance,
                directory
            );
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Native multiplayer proposals exported to " + directory);
    }

    /// <summary>
    /// Saves one camera-rendered native UI screenshot and its corresponding editable prefab.
    /// </summary>
    /// <param name="selectedLayout">The layout arrangement.</param>
    /// <param name="selectedPage">The screen to capture.</param>
    /// <param name="selectedTheme">The existing artwork family.</param>
    /// <param name="directory">The screenshot output directory.</param>
    private static void Export(
        MultiplayerProposalLayout selectedLayout,
        MultiplayerProposalPage selectedPage,
        MultiplayerProposalTheme selectedTheme,
        string directory
    )
    {
        Texture2D texture = Render(selectedLayout, selectedPage, selectedTheme, true);
        try
        {
            File.WriteAllBytes(
                Path.Combine(directory, $"{selectedLayout}-{selectedPage}-{selectedTheme}.png"),
                texture.EncodeToPNG()
            );
        }
        finally
        {
            DestroyImmediate(texture);
        }
    }

    /// <summary>
    /// Renders actual UGUI components in an isolated preview scene using the project's HDRP camera setup.
    /// </summary>
    /// <param name="selectedLayout">The layout arrangement.</param>
    /// <param name="selectedPage">The screen to capture.</param>
    /// <param name="selectedTheme">The existing artwork family.</param>
    /// <param name="savePrefab">Whether to persist the inspectable native hierarchy.</param>
    /// <returns>The caller-owned screenshot texture.</returns>
    private static Texture2D Render(
        MultiplayerProposalLayout selectedLayout,
        MultiplayerProposalPage selectedPage,
        MultiplayerProposalTheme selectedTheme,
        bool savePrefab
    )
    {
        PreviewRenderUtility preview = new PreviewRenderUtility();
        try
        {
            Camera camera = preview.camera;
            camera.cameraType = CameraType.Game;
            camera.orthographic = true;
            camera.orthographicSize = MultiplayerProposalBuilder.Height / 2f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.05f);
            camera.transform.position = new Vector3(
                MultiplayerProposalBuilder.Width / 2f,
                -MultiplayerProposalBuilder.Height / 2f,
                -1000f
            );
            camera.transform.rotation = Quaternion.identity;
            HdrpRenderTargetBuilder.ConfigureCamera(camera);
            GameObject root = MultiplayerProposalBuilder.Build(
                selectedLayout,
                selectedPage,
                selectedTheme
            );
            preview.AddSingleGO(root);
            if (savePrefab)
                MultiplayerProposalBuilder.SavePrefab(
                    root,
                    selectedLayout,
                    selectedPage,
                    selectedTheme
                );
            preview.BeginStaticPreview(new Rect(0, 0, 1800, 1260));
            camera.aspect =
                (float)MultiplayerProposalBuilder.Width / MultiplayerProposalBuilder.Height;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;
            UnityEngine.UI.CanvasScaler scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                MultiplayerProposalBuilder.Width,
                MultiplayerProposalBuilder.Height
            );
            scaler.matchWidthOrHeight = 0.5f;
            Canvas.ForceUpdateCanvases();
            preview.Render(true);
            return preview.EndStaticPreview();
        }
        finally
        {
            preview.Cleanup();
        }
    }
}
