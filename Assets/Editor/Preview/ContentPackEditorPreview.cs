using System;
using System.IO;
using System.Linq;
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
            TMPro.TMP_InputField search = view.GetComponentsInChildren<TMPro.TMP_InputField>(true)
                .Single(input => input.name == "Search");
            search.SetTextWithoutNotify(officer.ID);
            search.onValueChanged.Invoke(officer.ID);
            view.GetComponentsInChildren<ContentEditorRecordRowView>()
                .Single(row => row.Node?.Record?.Key == officer.Key)
                .OnPointerDown(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                    }
                );
            foreach (ContentEditorColorMode mode in Enum.GetValues(typeof(ContentEditorColorMode)))
            {
                view.SetColorMode(mode);
                SavePreview(preview, root, "_screenshots/ContentEditor/" + mode + ".png");
            }
        }
        finally
        {
            preview.Cleanup();
            if (Directory.Exists(workingFolder))
                Directory.Delete(workingFolder, true);
        }
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
