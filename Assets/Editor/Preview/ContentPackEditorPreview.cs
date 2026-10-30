using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
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
        ContentPackEditorProject project = ContentPackEditorProject.CreateInFolder(
            contentRoot,
            sourceID,
            workingFolder,
            "editor-preview-" + Guid.NewGuid().ToString("N"),
            "Preview pack",
            "1.0.0"
        );
        using ContentAssets assets = project.CreateAssets();
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
            preview.BeginStaticPreview(new Rect(0, 0, 1600, 900));
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1600, 900);
            camera.aspect = 1600f / 900;
            Canvas.ForceUpdateCanvases();
            ContentPackEditorView view = root.GetComponent<ContentPackEditorView>();
            view.Initialize(new[] { project.Definition }, contentRoot);
            view.RenderProject(project, assets);
            foreach (TMPro.TMP_Text label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                label.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();
            preview.Render(true);
            Texture2D image = preview.EndStaticPreview();
            try
            {
                Directory.CreateDirectory("_screenshots/ContentEditor");
                File.WriteAllBytes("_screenshots/ContentEditor/Workspace.png", image.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }
        finally
        {
            preview.Cleanup();
            if (Directory.Exists(workingFolder))
                Directory.Delete(workingFolder, true);
        }
    }
}
