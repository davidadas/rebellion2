using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Renders the authored workspace using real catalog data for visual verification.
/// </summary>
public static class ContentPackEditorPreview
{
    /// <summary>
    /// Rebuilds and renders the functional inspector without modifying its source content.
    /// </summary>
    public static void Export()
    {
        UIBuilderMenu.BuildContentPackEditor();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            ContentPackEditorPrefabBuilder.PrefabPath
        );
        string contentRoot = Path.GetFullPath("Assets/Content");
        string folder = Directory
            .EnumerateDirectories(Path.Combine(contentRoot, "Packs"))
            .OrderBy(path => path, System.StringComparer.Ordinal)
            .First(path => File.Exists(Path.Combine(path, "pack.xml")));
        string sourceID = System
            .Xml.Linq.XDocument.Load(Path.Combine(folder, "pack.xml"))
            .Root.Element("ID")
            .Value;
        string workingFolder = Path.Combine(
            Path.GetTempPath(),
            "editor-preview-" + Guid.NewGuid().ToString("N")
        );
        ContentPackEditorProject project = ContentPackEditorStorage.CreateInFolder(
            contentRoot,
            sourceID,
            workingFolder,
            "editor-preview-" + Guid.NewGuid().ToString("N"),
            "Preview pack",
            "1.0.0"
        );
        using ContentAssets assets = ContentPackEditorStorage.CreateAssets(project);
        PreviewRenderUtility preview = new PreviewRenderUtility();
        try
        {
            Camera camera = preview.camera;
            camera.cameraType = CameraType.Game;
            camera.orthographic = true;
            camera.orthographicSize = 450;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ContentEditorControlBuilder.Background;
            camera.transform.position = new Vector3(800, -450, -1000);
            camera.transform.rotation = Quaternion.identity;
            HdrpRenderTargetBuilder.ConfigureCamera(camera);
            GameObject root = Object.Instantiate(prefab.transform.Find("Canvas").gameObject);
            preview.AddSingleGO(root);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1600, 900);
            camera.aspect = 1600f / 900;
            Canvas.ForceUpdateCanvases();
            ContentPackEditorView view = root.GetComponent<ContentPackEditorView>();
            using ContentPackEditorWorkspaceController workspace =
                new ContentPackEditorWorkspaceController(view);
            view.Initialize(new[] { project.Definition }, contentRoot);
            workspace.RenderProject(project, assets);
            ContentPackEditorRecord officer = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .First(record => record.Kind == "Officer");
            SelectRecord(view, officer);
            foreach (ContentEditorColorMode mode in Enum.GetValues(typeof(ContentEditorColorMode)))
            {
                view.SetColorMode(mode);
                SavePreview(preview, root, "_screenshots/ContentEditor/" + mode + ".png");
            }
            ExportMapLayout(preview, root, view, workspace, project, assets);
        }
        finally
        {
            preview.Cleanup();
            if (Directory.Exists(workingFolder))
                Directory.Delete(workingFolder, true);
        }
    }

    /// <summary>
    /// Renders before and after layouts in a temporary derived project without saving its documents.
    /// </summary>
    /// <param name="preview">The active preview renderer.</param>
    /// <param name="root">The live workspace canvas.</param>
    /// <param name="view">The workspace presentation.</param>
    /// <param name="workspace">The bound workspace controller.</param>
    /// <param name="project">The temporary derived project.</param>
    /// <param name="assets">Its borrowed asset store.</param>
    private static void ExportMapLayout(
        PreviewRenderUtility preview,
        GameObject root,
        ContentPackEditorView view,
        ContentPackEditorWorkspaceController workspace,
        ContentPackEditorProject project,
        ContentAssets assets
    )
    {
        ContentPackEditorRecord sector = project
            .GetCatalogs()
            .First(catalog => catalog.IsMap)
            .Records.First(record => record.Planets.Count >= 2);
        ContentPackEditorRecord first = sector.Planets[0];
        project.Move(sector.Planets[1], first.X, first.Y);
        workspace.RenderProject(project, assets);
        view.SetColorMode(ContentEditorColorMode.Dark);
        SelectRecord(view, sector);
        Canvas.ForceUpdateCanvases();
        SavePreview(preview, root, "_screenshots/ContentEditor/Sector-before.png");
        ContentEditorMapLayout layout = null;
        Action<ContentEditorMapLayout> capture = requested => layout = requested;
        view.MapLayoutRequested += capture;
        try
        {
            view.GetComponentsInChildren<Button>()
                .Single(button => button.name == "ArrangeMap")
                .onClick.Invoke();
        }
        finally
        {
            view.MapLayoutRequested -= capture;
        }
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        Vector2Int[] positions = RectangleLayout.Arrange(
            layout.Items,
            layout.Bounds,
            layout.Gap,
            layout.CoordinateScale,
            timeout.Token
        );
        using (project.BeginEdit("Auto arrange map"))
            for (int i = 0; i < positions.Length; i++)
                if (positions[i] != layout.Items[i].Position)
                    project.Move(
                        layout.Records[i],
                        checked(positions[i].x + layout.Origin.x),
                        checked(positions[i].y + layout.Origin.y)
                    );
        workspace.RenderProject(project, assets);
        SavePreview(preview, root, "_screenshots/ContentEditor/Sector-after.png");
    }

    /// <summary>
    /// Opens one definition through the workspace's visible navigation controls.
    /// </summary>
    /// <param name="view">The workspace presentation.</param>
    /// <param name="record">The definition to reveal.</param>
    private static void SelectRecord(ContentPackEditorView view, ContentPackEditorRecord record)
    {
        TMPro.TMP_InputField search = view.GetComponentsInChildren<TMPro.TMP_InputField>(true)
            .Single(input => input.name == "Search");
        search.SetTextWithoutNotify(record.ID);
        search.onValueChanged.Invoke(record.ID);
        view.GetComponentsInChildren<ContentEditorRecordRowView>()
            .Single(row => row.Node?.Record?.Key == record.Key)
            .OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
    }

    /// <summary>
    /// Captures the current authored controls after rebuilding their text meshes and layout.
    /// </summary>
    /// <param name="preview">The active rendering context.</param>
    /// <param name="root">The live workspace canvas.</param>
    /// <param name="path">The ignored image output path.</param>
    private static void SavePreview(PreviewRenderUtility preview, GameObject root, string path)
    {
        preview.BeginStaticPreview(new Rect(0, 0, 1600, 900));
        foreach (TMPro.TMP_Text label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
            label.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
        preview.Render(true);
        Texture2D image = preview.EndStaticPreview();
        try
        {
            Directory.CreateDirectory("_screenshots/ContentEditor");
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(image);
        }
    }
}
