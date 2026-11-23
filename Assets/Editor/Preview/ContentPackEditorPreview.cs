using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TMPro;
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
    public static void Export() => ExportWorkspace(false);

    /// <summary>
    /// Renders the publishing workflow independently of the catalog-artwork previews.
    /// </summary>
    public static void ExportPublishing() => ExportWorkspace(true);

    /// <summary>
    /// Renders the live community browser using the same controller as the toolkit.
    /// </summary>
    [MenuItem("Rebellion/Preview/Mod manager")]
    public static async void ExportModManagerAsync()
    {
        PreviewRenderUtility preview = new PreviewRenderUtility();
        ContentEditorModManagerController manager = null;
        int result = 1;
        try
        {
            UIBuilderMenu.BuildContentPackEditor();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ContentPackEditorPrefabBuilder.PrefabPath
            );
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
            root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600, 900);
            camera.aspect = 1600f / 900;
            ContentPackEditorView view = root.GetComponent<ContentPackEditorView>();
            string contentRoot = ContentPackLoader.ResolveContentRootPath();
            view.Initialize(ContentPackLoader.DiscoverPacks(contentRoot), contentRoot);
            view.SetColorMode(ContentEditorColorMode.Light);
            manager = new ContentEditorModManagerController(
                view.ModManager,
                contentRoot,
                () => Array.Empty<string>(),
                view.ConfirmModRemoval,
                () => { }
            );
            manager.Show();
            while (manager.IsBusy)
                await Task.Yield();
            RawImage icon = view
                .ModManager.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == "PackageIcon");
            DateTime iconDeadline = DateTime.UtcNow.AddSeconds(10);
            while (!icon.enabled && DateTime.UtcNow < iconDeadline)
                await Task.Yield();
            Canvas.ForceUpdateCanvases();
            foreach (
                ContentEditorColorMode mode in new[]
                {
                    ContentEditorColorMode.Dark,
                    ContentEditorColorMode.Light,
                }
            )
            {
                view.SetColorMode(mode);
                SavePreview(
                    preview,
                    root,
                    "_screenshots/ContentEditor/Mod-manager-" + mode + ".png"
                );
            }
            view.SetColorMode(ContentEditorColorMode.Dark);
            SavePreview(preview, root, "_screenshots/ContentEditor/Mod-manager.png");
            TMP_Dropdown sort = view
                .ModManager.GetComponentsInChildren<TMP_Dropdown>(true)
                .Single(dropdown => dropdown.name == "sorting");
            sort.value = (int)ContentEditorModSort.Downloads;
            SavePreview(preview, root, "_screenshots/ContentEditor/Mod-manager-downloads.png");
            TMP_Dropdown category = view
                .ModManager.GetComponentsInChildren<TMP_Dropdown>(true)
                .Single(dropdown => dropdown.name == "category");
            category.value = 1;
            SavePreview(preview, root, "_screenshots/ContentEditor/Mod-manager-category.png");
            category.value = 0;
            TMP_InputField search = view.ModManager.GetComponentInChildren<TMP_InputField>(true);
            search.text = "no such package";
            await Task.Delay(250);
            SavePreview(preview, root, "_screenshots/ContentEditor/Mod-manager-empty.png");

            Debug.Log(
                "Mod manager preview: "
                    + Path.GetFullPath("_screenshots/ContentEditor/Mod-manager.png")
            );
            result = 0;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            manager?.Dispose();
            preview.Cleanup();
            if (Application.isBatchMode)
                EditorApplication.Exit(result);
        }
    }

    /// <summary>
    /// Creates a temporary editable workspace and renders the requested editor surfaces.
    /// </summary>
    /// <param name="publishingOnly">Whether to render only the publishing workflow.</param>
    private static void ExportWorkspace(bool publishingOnly)
    {
        UIBuilderMenu.BuildContentPackEditor();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            ContentPackEditorPrefabBuilder.PrefabPath
        );
        string contentRoot = Path.GetFullPath("Assets/Content");
        string folder = Directory
            .EnumerateDirectories(Path.Combine(contentRoot, "Packs"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .First(path => File.Exists(Path.Combine(path, "pack.xml")));
        string sourceID = XDocument.Load(Path.Combine(folder, "pack.xml")).Root.Element("ID").Value;
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
            if (publishingOnly)
            {
                view.SetColorMode(ContentEditorColorMode.Light);
                RenderPublishing(preview, root, view, project);
                return;
            }
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
            ExportUnitArtwork(preview, root, view, project);
            ExportUnitArtworkKinds(preview, root, view, project);
            ExportOfficerAudio(preview, root, view, project);
            ExportFactionInterfaces(preview, root, view, project);
            ExportMapLayout(preview, root, view, workspace, project, assets);
            RenderPublishing(preview, root, view, project);
        }
        finally
        {
            preview.Cleanup();
            if (Directory.Exists(workingFolder))
                Directory.Delete(workingFolder, true);
        }
    }

    /// <summary>
    /// Renders each publishing stage using the same authored dialog as the standalone editor.
    /// </summary>
    /// <param name="preview">The active preview renderer.</param>
    /// <param name="root">The live workspace canvas.</param>
    /// <param name="view">The workspace presentation.</param>
    /// <param name="project">The temporary derived project.</param>
    private static void RenderPublishing(
        PreviewRenderUtility preview,
        GameObject root,
        ContentPackEditorView view,
        ContentPackEditorProject project
    )
    {
        view.Publishing.Show(
            project.Definition,
            new ContentEditorPublishingSettings
            {
                PackageName = "Frontier_Campaigns",
                Description = "A campaign with regional objectives and updated faction artwork.",
                Readme =
                    "# Frontier campaigns\n\nA new strategic campaign with regional objectives.",
                Changelog = "Added regional objectives and revised starting fleets.",
            }
        );
        foreach (string stage in new[] { "Listing", "Release", "Review" })
        {
            view.Publishing.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == stage + "Step")
                .onClick.Invoke();
            SavePreview(preview, root, "_screenshots/ContentEditor/Publishing-" + stage + ".png");
        }
        view.Publishing.Hide();
    }

    /// <summary>
    /// Renders the current starfighter outputs and state artwork.
    /// </summary>
    /// <param name="preview">The active preview renderer.</param>
    /// <param name="root">The live workspace canvas.</param>
    /// <param name="view">The workspace presentation.</param>
    /// <param name="project">The temporary derived project.</param>
    private static void ExportUnitArtwork(
        PreviewRenderUtility preview,
        GameObject root,
        ContentPackEditorView view,
        ContentPackEditorProject project
    )
    {
        ContentPackEditorRecord starfighter = project
            .GetCatalogs()
            .SelectMany(catalog => catalog.Records)
            .First(record => record.Kind == "Starfighter");
        view.SetColorMode(ContentEditorColorMode.Dark);
        SelectRecord(view, starfighter);
        view.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == "Tab1")
            .onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        ScrollAreaLayout artworkScroll = root.GetComponentsInChildren<ScrollAreaLayout>()
            .Single(scroll => scroll.GetComponent<ContentEditorFormView>() != null);
        RevealArtworkPanel(artworkScroll, "ArtworkPanelTemplate-Background");
        SavePreview(preview, root, "_screenshots/ContentEditor/Unit-artwork-background.png");
        RevealArtworkPanel(artworkScroll, "ArtworkPanelTemplate-Images");
        SavePreview(preview, root, "_screenshots/ContentEditor/Unit-artwork-cuts.png");
        ContentEditorCardView results = view.GetComponentsInChildren<ContentEditorCardView>()
            .Single(card => card.name == "ArtworkPanelTemplate-Images");
        RevealContent(
            artworkScroll,
            (RectTransform)
                results.GetComponentsInChildren<ContentEditorFieldView>().Last().transform
        );
        SavePreview(preview, root, "_screenshots/ContentEditor/Unit-artwork-output-files.png");
        RevealArtworkPanel(artworkScroll, "ArtworkPanelTemplate-GameplayStates");
        SavePreview(preview, root, "_screenshots/ContentEditor/Unit-artwork-states.png");
        ContentEditorCardView states = view.GetComponentsInChildren<ContentEditorCardView>()
            .Single(card => card.name == "ArtworkPanelTemplate-GameplayStates");
        RevealContent(
            artworkScroll,
            (RectTransform)states.GetComponentsInChildren<ContentEditorFieldView>().Last().transform
        );
        SavePreview(preview, root, "_screenshots/ContentEditor/Unit-artwork-state-files.png");
    }

    /// <summary>
    /// Renders the Images card for every supported unit kind to expose type-specific layout gaps.
    /// </summary>
    /// <param name="preview">The active preview renderer.</param>
    /// <param name="root">The live workspace canvas.</param>
    /// <param name="view">The workspace presentation.</param>
    /// <param name="project">The temporary derived project.</param>
    private static void ExportUnitArtworkKinds(
        PreviewRenderUtility preview,
        GameObject root,
        ContentPackEditorView view,
        ContentPackEditorProject project
    )
    {
        string[] kinds =
        {
            "Officer",
            "SpecialForce",
            "Regiment",
            "CapitalShip",
            "Starfighter",
            "Building",
        };
        foreach (string kind in kinds)
        {
            ContentPackEditorRecord unit = project
                .GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == kind)
                .OrderByDescending(record => record.Catalog.Faction == "Alliance")
                .First();
            SelectRecord(view, unit);
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Tab1")
                .onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            ScrollAreaLayout scroll = root.GetComponentsInChildren<ScrollAreaLayout>()
                .Single(item => item.GetComponent<ContentEditorFormView>() != null);
            RevealArtworkPanel(scroll, "ArtworkPanelTemplate-Images");
            SavePreview(
                preview,
                root,
                "_screenshots/ContentEditor/Unit-artwork-" + kind + "-results.png"
            );
            if (kind == "Officer")
            {
                Toggle imagesBackgroundToggle = view.GetComponentsInChildren<UICheckboxView>()
                    .Single(toggle => toggle.name == "ImagesBackgroundToggle")
                    .GetComponent<Toggle>();
                imagesBackgroundToggle.isOn = false;
                Canvas.ForceUpdateCanvases();
                RevealArtworkPanel(scroll, "ArtworkPanelTemplate-Images");
                SavePreview(
                    preview,
                    root,
                    "_screenshots/ContentEditor/Unit-artwork-Officer-results-no-background.png"
                );
                imagesBackgroundToggle.isOn = true;
                Canvas.ForceUpdateCanvases();
                RevealArtworkPanel(scroll, "ArtworkPanelTemplate-GameplayStates");
                SavePreview(
                    preview,
                    root,
                    "_screenshots/ContentEditor/Unit-artwork-Officer-states.png"
                );
                Toggle statesBackgroundToggle = view.GetComponentsInChildren<UICheckboxView>()
                    .Single(toggle => toggle.name == "GameplayStatesBackgroundToggle")
                    .GetComponent<Toggle>();
                statesBackgroundToggle.isOn = false;
                Canvas.ForceUpdateCanvases();
                RevealArtworkPanel(scroll, "ArtworkPanelTemplate-GameplayStates");
                SavePreview(
                    preview,
                    root,
                    "_screenshots/ContentEditor/Unit-artwork-Officer-states-no-background.png"
                );
                statesBackgroundToggle.isOn = true;
                Canvas.ForceUpdateCanvases();
            }
        }
    }

    /// <summary>
    /// Renders the officer voice-file editor to expose audio-row sizing and copy density.
    /// </summary>
    /// <param name="preview">The active preview renderer.</param>
    /// <param name="root">The live workspace canvas.</param>
    /// <param name="view">The workspace presentation.</param>
    /// <param name="project">The temporary derived project.</param>
    private static void ExportOfficerAudio(
        PreviewRenderUtility preview,
        GameObject root,
        ContentPackEditorView view,
        ContentPackEditorProject project
    )
    {
        ContentPackEditorRecord officer = project
            .GetCatalogs()
            .SelectMany(catalog => catalog.Records)
            .Where(record => record.Kind == "Officer")
            .OrderByDescending(record => record.Catalog.Faction == "Alliance")
            .First();
        SelectRecord(view, officer);
        SelectSection(view, "Audio");
        SavePreview(preview, root, "_screenshots/ContentEditor/Unit-audio-Officer.png");
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
    /// Renders representative faction-owned interface surfaces and the disabled-control state.
    /// </summary>
    /// <param name="preview">The active preview renderer.</param>
    /// <param name="root">The live workspace canvas.</param>
    /// <param name="view">The workspace presentation.</param>
    /// <param name="project">The temporary derived project.</param>
    private static void ExportFactionInterfaces(
        PreviewRenderUtility preview,
        GameObject root,
        ContentPackEditorView view,
        ContentPackEditorProject project
    )
    {
        ContentPackEditorRecord faction = project
            .GetCatalogs()
            .SelectMany(catalog => catalog.Records)
            .First(record =>
                record.Kind == "Faction"
                && record.Name == "Alliance"
                && record
                    .GetFields()
                    .Any(field =>
                        field.MemberName == "InstanceID" && !string.IsNullOrWhiteSpace(field.Value)
                    )
            );
        SelectRecord(view, faction);
        SelectSection(view, "Interfaces");
        TMP_Dropdown interfaceDropdown = view.GetComponentsInChildren<TMP_Dropdown>(true)
            .Single(candidate => candidate.name == "ArtworkViewDropdown");
        foreach (TMP_Dropdown.OptionData option in interfaceDropdown.options.ToArray())
        {
            SelectDropdownOption(view, "ArtworkViewDropdown", option.text);
            SavePreview(
                preview,
                root,
                "_screenshots/ContentEditor/Interface-"
                    + option.text.ToLowerInvariant().Replace(' ', '-')
                    + ".png"
            );
        }
        SelectDropdownOption(view, "ArtworkViewDropdown", "Fleet window");
        ScrollAreaLayout interfaceScroll = root.GetComponentsInChildren<ScrollAreaLayout>()
            .Single(scroll => scroll.GetComponent<ContentEditorFormView>() != null);
        ContentEditorFieldView[] fleetFields =
            view.GetComponentsInChildren<ContentEditorFieldView>();
        RevealContent(interfaceScroll, (RectTransform)fleetFields.First().transform);
        SavePreview(preview, root, "_screenshots/ContentEditor/Interface-fleet-fields-first.png");
        RevealContent(interfaceScroll, (RectTransform)fleetFields.Last().transform);
        SavePreview(preview, root, "_screenshots/ContentEditor/Interface-fleet-fields-last.png");
        SelectDropdownOption(view, "ArtworkViewDropdown", "Defense window");
        Canvas.ForceUpdateCanvases();
        SavePreview(preview, root, "_screenshots/ContentEditor/Interface-defense-disabled.png");
        ContentEditorArtworkEditTarget hoveredTarget =
            view.GetComponentsInChildren<ContentEditorArtworkEditTarget>(true)
                .Where(target => target.isActiveAndEnabled)
                .Where(target =>
                {
                    Rect rect = ((RectTransform)target.transform).rect;
                    return rect.width >= 12 && rect.height >= 12;
                })
                .OrderBy(target =>
                {
                    Rect rect = ((RectTransform)target.transform).rect;
                    return rect.width * rect.height;
                })
                .First();
        hoveredTarget.OnPointerEnter(null);
        Canvas.ForceUpdateCanvases();
        SavePreview(preview, root, "_screenshots/ContentEditor/Interface-defense-hover.png");
        hoveredTarget.OnPointerExit(null);
        SelectSection(view, "Default backgrounds");
        SavePreview(preview, root, "_screenshots/ContentEditor/Faction-default-backgrounds.png");
        SelectSection(view, "Colors");
        SavePreview(preview, root, "_screenshots/ContentEditor/Faction-colors.png");
        view.ZoomContent(-0.35f);
        SavePreview(preview, root, "_screenshots/ContentEditor/Faction-colors-minimum-zoom.png");
        view.ZoomContent(0.35f);
        ScrollAreaLayout colorScroll = root.GetComponentsInChildren<ScrollAreaLayout>()
            .Single(scroll => scroll.GetComponent<ContentEditorFormView>() != null);
        ContentEditorColorPickerView lastColor =
            view.GetComponentsInChildren<ContentEditorColorPickerView>().Last();
        RevealContent(colorScroll, (RectTransform)lastColor.transform);
        SavePreview(preview, root, "_screenshots/ContentEditor/Faction-colors-lower.png");
    }

    /// <summary>
    /// Reveals one named unit-artwork workflow panel for a dedicated visual inspection capture.
    /// </summary>
    /// <param name="scroll">The artwork form's scroll area.</param>
    /// <param name="panelName">The stable runtime panel name.</param>
    private static void RevealArtworkPanel(ScrollAreaLayout scroll, string panelName)
    {
        ContentEditorCardView panel = scroll
            .ScrollArea.ContentRoot.GetComponentsInChildren<RawImage>(true)
            .Select(image => image.GetComponent<ContentEditorCardView>())
            .Single(card => card != null && card.name == panelName);
        RevealContent(scroll, panel.RectTransform);
    }

    /// <summary>
    /// Scrolls a rendered form element to the top of its viewport when its height permits.
    /// </summary>
    /// <param name="scroll">The owning scroll area.</param>
    /// <param name="target">The content-space element to reveal.</param>
    private static void RevealContent(ScrollAreaLayout scroll, RectTransform target)
    {
        scroll.ScrollArea.VerticalNormalizedPosition = 1f;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            scroll.ScrollArea.ContentRoot,
            target
        );
        scroll.ScrollArea.RevealContentRect(
            -bounds.max.y,
            Mathf.Min(bounds.size.y, scroll.ScrollArea.ViewportHeight)
        );
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Invokes the visible inspector tab with the requested caption.
    /// </summary>
    /// <param name="view">The workspace presentation.</param>
    /// <param name="caption">The exact tab caption.</param>
    private static void SelectSection(ContentPackEditorView view, string caption)
    {
        Button button = view.GetComponentsInChildren<Button>(true)
            .Single(candidate =>
                candidate
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Any(label => label.text == caption)
            );
        button.onClick.Invoke();
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Selects a named option in one visible preview dropdown.
    /// </summary>
    /// <param name="view">The workspace presentation.</param>
    /// <param name="dropdownName">The authored dropdown object name.</param>
    /// <param name="option">The exact visible option caption.</param>
    private static void SelectDropdownOption(
        ContentPackEditorView view,
        string dropdownName,
        string option
    )
    {
        TMP_Dropdown dropdown = view.GetComponentsInChildren<TMP_Dropdown>(true)
            .Single(candidate => candidate.name == dropdownName);
        int index = dropdown.options.FindIndex(candidate => candidate.text == option);
        if (index < 0)
            throw new InvalidOperationException(
                dropdownName + " has no preview option named " + option + "."
            );
        dropdown.SetValueWithoutNotify(index);
        dropdown.onValueChanged.Invoke(index);
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Opens one definition through the workspace's visible navigation controls.
    /// </summary>
    /// <param name="view">The workspace presentation.</param>
    /// <param name="record">The definition to reveal.</param>
    private static void SelectRecord(ContentPackEditorView view, ContentPackEditorRecord record)
    {
        TMP_InputField search = view.GetComponentsInChildren<TMP_InputField>(true)
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
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
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
