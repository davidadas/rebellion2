using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Rebellion.Util.Logging;
using UnityEngine;

/// <summary>
/// Owns project operations and media lifetimes for the standalone authoring workspace.
/// </summary>
public sealed class ContentPackEditorController : MonoBehaviour
{
    [SerializeField]
    private ContentPackEditorView view;
    private string contentRootPath;
    private ContentPackEditorProject project;
    private ContentPackEditorWorkspaceController workspace;
    private ContentAssets assets;
    private ContentAssets applicationAssets;
    private bool quitApproved;
    private CancellationTokenSource operationCancellation;
    private bool quitAfterOperation;
    private bool audioPreviewLoading;
    private int audioPreviewRequest;
    private string previewAudioAddress;
    private ContentEditorSettingsManager settingsManager;
    private ContentEditorFileDrops fileDrops;
    private const string _colorModePreference = "ContentEditor.ColorMode";
    private const string _alphaNoticePreference = "ContentEditor.AlphaNoticeAcknowledged";

    /// <summary>
    /// Binds application operations without starting a game runtime.
    /// </summary>
    private void Awake()
    {
        if (view == null)
            throw new MissingReferenceException("ContentPackEditorController has no view.");
        workspace = new ContentPackEditorWorkspaceController(view);
        contentRootPath = ContentPackLoader.ResolveContentRootPath();
        view.CreateRequested += CreatePack;
        view.OpenRequested += ChooseOpenPack;
        view.BrowseFolderRequested += BrowsePackFolder;
        view.ImportRequested += ImportMedia;
        view.ArtworkCutsRequested += PreviewArtworkCuts;
        view.ArtworkEffectRequested += PreviewArtworkEffect;
        view.SaveRequested += SavePack;
        view.ExportRequested += ExportPack;
        view.ValidateRequested += ValidatePack;
        view.MapLayoutRequested += ArrangeMap;
        view.UndoRequested += Undo;
        view.RedoRequested += Redo;
        view.CloseRequested += ClosePack;
        view.ExitRequested += RequestQuit;
        view.PlayRequested += PlayGame;
        view.PreviewRequested += PreviewMedia;
        view.PreviewCancelled += InvalidateMediaPreview;
        Application.wantsToQuit += CanQuit;
        fileDrops = ContentEditorFileDrops.Create(Application.platform);
    }

    /// <summary>
    /// Loads shared UI artwork and discovers installed inheritance sources.
    /// </summary>
    private void Start()
    {
        RunOperation(() =>
        {
            settingsManager = new ContentEditorSettingsManager(
                Path.Combine(UserDataPaths.Root, "content-editor-settings.json"),
                new ContentEditorSettings
                {
                    ColorMode = (ContentEditorColorMode)PlayerPrefs.GetInt(_colorModePreference, 0),
                    AlphaNoticeAcknowledged = PlayerPrefs.GetInt(_alphaNoticePreference, 0) == 1,
                }
            );
            ContentEditorSettings settings = settingsManager.Load();
            view.ApplySettings(settings);
            view.ColorModeChanged += RememberColorMode;
            view.LayoutChanged += RememberLayout;
            applicationAssets = new ContentAssets(
                new ContentFileResolver(contentRootPath, contentRootPath)
            );
            ContentBindings.Apply(view.gameObject, applicationAssets);
            view.Initialize(ContentPackLoader.DiscoverPacks(contentRootPath), contentRootPath);
            string[] arguments = Environment.GetCommandLineArgs();
            int packPathIndex = Array.IndexOf(arguments, "-packPath");
            if (packPathIndex >= 0)
            {
                if (packPathIndex + 1 >= arguments.Length)
                    throw new ArgumentException("A content-pack folder must follow -packPath.");
                OpenProject(
                    ContentPackEditorStorage.OpenFolder(
                        contentRootPath,
                        arguments[packPathIndex + 1]
                    )
                );
            }
            if (!settings.AlphaNoticeAcknowledged)
                view.ShowAlphaNotice(AcknowledgeAlphaNotice);
        });
    }

    /// <summary>
    /// Persists explicit acknowledgement without tying it to a particular application version.
    /// </summary>
    private void AcknowledgeAlphaNotice()
    {
        RunOperation(() => settingsManager.AcknowledgeAlphaNotice());
    }

    /// <summary>
    /// Routes native key events without losing repeated shortcuts between rendered frames.
    /// </summary>
    private void OnGUI()
    {
        Event key = Event.current;
        if (operationCancellation != null)
        {
            if (key.type == EventType.KeyDown && key.keyCode == KeyCode.Escape)
            {
                operationCancellation.Cancel();
                key.Use();
            }
            return;
        }
        view.HandleKeyboardShortcut(key);
    }

    /// <summary>
    /// Dispatches native file drops through the editor's current image import targets.
    /// </summary>
    private void Update()
    {
        if (fileDrops == null || !fileDrops.TryRead(out var drop))
            return;
        RunOperation(() =>
        {
            if (project == null)
                return;
            Vector2 position = drop.Position * new Vector2(Screen.width, Screen.height);
            view.TryImportDroppedImage(position, drop.Files, ImportSelectedMedia);
        });
    }

    /// <summary>
    /// Releases all subscriptions before destroying project-owned media.
    /// </summary>
    private void OnDestroy()
    {
        fileDrops?.Dispose();
        operationCancellation?.Cancel();
        Application.wantsToQuit -= CanQuit;
        if (view != null)
        {
            view.StopAudioPreview();
            view.CreateRequested -= CreatePack;
            view.OpenRequested -= ChooseOpenPack;
            view.BrowseFolderRequested -= BrowsePackFolder;
            view.ImportRequested -= ImportMedia;
            view.ArtworkCutsRequested -= PreviewArtworkCuts;
            view.ArtworkEffectRequested -= PreviewArtworkEffect;
            view.SaveRequested -= SavePack;
            view.ExportRequested -= ExportPack;
            view.ValidateRequested -= ValidatePack;
            view.MapLayoutRequested -= ArrangeMap;
            view.UndoRequested -= Undo;
            view.RedoRequested -= Redo;
            view.CloseRequested -= ClosePack;
            view.ExitRequested -= RequestQuit;
            view.ColorModeChanged -= RememberColorMode;
            view.LayoutChanged -= RememberLayout;
            view.PlayRequested -= PlayGame;
            view.PreviewRequested -= PreviewMedia;
            view.PreviewCancelled -= InvalidateMediaPreview;
        }
        workspace?.Dispose();
        assets?.Dispose();
        applicationAssets?.Dispose();
    }

    /// <summary>
    /// Resolves preview media while rejecting results from closed projects and superseded requests.
    /// </summary>
    /// <param name="field">The authored media field.</param>
    /// <param name="address">The selected content address or directory item.</param>
    private async void PreviewMedia(ContentPackEditorField field, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return;
        int request = audioPreviewRequest;
        try
        {
            if (!field.IsMediaDirectory)
                address = field.GetMediaAddress(address);
            if (field.MediaType == ContentPackEditorMediaType.Image)
            {
                Texture2D texture = assets.GetTexture(address);
                if (texture == null)
                    throw new FileNotFoundException(
                        "Image could not be opened. Check the file or use Import."
                    );
                view.ShowImagePreview(texture, address);
            }
            else if (field.MediaType == ContentPackEditorMediaType.Audio)
            {
                if (
                    previewAudioAddress == address
                    && (view.IsPreviewAudioPlaying || audioPreviewLoading)
                )
                {
                    view.StopAudioPreview();
                    return;
                }
                view.StopAudioPreview();
                previewAudioAddress = address;
                audioPreviewLoading = true;
                request = audioPreviewRequest;
                ContentAssets requestedAssets = assets;
                AudioClip clip = await assets.LoadAudioAsync(address);
                if (this != null && assets == requestedAssets && audioPreviewRequest == request)
                {
                    audioPreviewLoading = false;
                    view.PlayPreviewAudio(clip);
                }
            }
            else
                view.RenderStatus(
                    "Asset file: " + assets.ResolveFile(address, Path.GetExtension(address)),
                    false
                );
        }
        catch (Exception exception)
        {
            if (
                this != null
                && (
                    field.MediaType != ContentPackEditorMediaType.Audio
                    || request == audioPreviewRequest
                )
            )
            {
                audioPreviewLoading = false;
                ReportError(
                    exception,
                    exception is ArgumentException ? "Use Import to choose a content file." : null
                );
            }
        }
    }

    /// <summary>
    /// Invalidates an outstanding preview when its UI is stopped, rebound, or destroyed.
    /// </summary>
    private void InvalidateMediaPreview()
    {
        audioPreviewRequest++;
        previewAudioAddress = null;
        audioPreviewLoading = false;
    }

    /// <summary>
    /// Remembers only the editor appearance in the companion application's preferences.
    /// </summary>
    /// <param name="mode">The selected presentation.</param>
    private void RememberColorMode(ContentEditorColorMode mode)
    {
        RunOperation(() => settingsManager.SaveColorMode(mode));
    }

    /// <summary>
    /// Persists content zoom and the splitter position as application preferences.
    /// </summary>
    /// <param name="zoom">The content scale.</param>
    /// <param name="split">The navigation width fraction.</param>
    private void RememberLayout(float zoom, float split) =>
        RunOperation(() => settingsManager.SaveLayout(zoom, split));

    /// <summary>
    /// Launches the actual game using an isolated snapshot of the current authored content.
    /// </summary>
    /// <param name="options">The requested playable faction and generation choices.</param>
    private async void PlayGame(ContentEditorPlayOptions options)
    {
        await RunOperationAsync(async cancellationToken =>
        {
            if (project == null || !view.CommitFields())
                return;
            view.RenderStatus("Preparing test game. Press Escape to cancel.", false);
            ContentEditorPlaySession session = await ContentEditorPlaySession.PrepareAsync(
                project,
                options,
                cancellationToken
            );
            try
            {
                session.Launch(Application.dataPath, Application.platform);
            }
            catch
            {
                session.Dispose();
                throw;
            }
            view.RenderStatus(
                "Test game opened. Its saves and settings are separate from your normal games.",
                false
            );
        });
    }

    /// <summary>
    /// Creates a derived working pack after the author handles pending edits.
    /// </summary>
    /// <param name="basePackID">The installed inheritance source.</param>
    /// <param name="packID">The new identity.</param>
    /// <param name="displayName">The new display name.</param>
    /// <param name="version">The initial version.</param>
    /// <param name="folder">The new working folder.</param>
    private void CreatePack(
        string basePackID,
        string packID,
        string displayName,
        string version,
        string folder
    )
    {
        ContinueAfterChanges(() =>
            OpenProject(
                ContentPackEditorStorage.CreateInFolder(
                    contentRootPath,
                    basePackID,
                    Path.Combine(folder, packID),
                    packID,
                    displayName,
                    version
                )
            )
        );
    }

    /// <summary>
    /// Opens a working pack independently of the installation directory.
    /// </summary>
    /// <param name="folder">The folder containing pack.xml.</param>
    private void OpenFolder(string folder)
    {
        ContinueAfterChanges(() =>
            OpenProject(ContentPackEditorStorage.OpenFolder(contentRootPath, folder))
        );
    }

    /// <summary>
    /// Selects a working pack folder through the native picker before handling pending project edits.
    /// </summary>
    private void ChooseOpenPack()
    {
        RunOperation(() =>
        {
            string mods = ContentFileResolver.ResolveModsRootPath(contentRootPath);
            Directory.CreateDirectory(mods);
            string folder = ContentEditorFileDialog.OpenFolder(mods);
            if (string.IsNullOrEmpty(folder))
                return;
            if (!File.Exists(Path.Combine(folder, "pack.xml")))
                throw new ArgumentException("Choose a content pack folder.");
            OpenFolder(folder);
        });
    }

    /// <summary>
    /// Selects a creation destination without giving the dialog ownership of filesystem operations.
    /// </summary>
    /// <param name="currentFolder">The parent directory entered in the creation form.</param>
    private void BrowsePackFolder(string currentFolder)
    {
        RunOperation(() =>
        {
            string mods = ContentFileResolver.ResolveModsRootPath(contentRootPath);
            Directory.CreateDirectory(mods);
            string initial =
                currentFolder == mods ? mods
                : Directory.Exists(currentFolder) ? currentFolder
                : Path.GetDirectoryName(currentFolder);
            string folder = ContentEditorFileDialog.OpenFolder(initial);
            if (!string.IsNullOrEmpty(folder))
                view.SetPackLocation(folder);
        });
    }

    /// <summary>
    /// Imports a selected file and applies its content address through the project's normal editing API.
    /// </summary>
    /// <param name="field">The field receiving the imported content address.</param>
    private void ImportMedia(ContentPackEditorField field)
    {
        RunOperation(() =>
        {
            if (project == null || !view.CommitFields())
                return;
            string source = ContentEditorFileDialog.OpenFile(
                field.MediaType == ContentPackEditorMediaType.Image
                    ? GetImageImportDirectory()
                    : GetAssetPickerDirectory(field),
                field.MediaExtensions
            );
            if (!string.IsNullOrEmpty(source))
                ImportSelectedMedia(field, source);
        });
    }

    /// <summary>
    /// Uses the same preview and replacement flow for selected and dropped files.
    /// </summary>
    /// <param name="field">The media slot receiving the file.</param>
    /// <param name="source">The selected or dropped file.</param>
    private void ImportSelectedMedia(ContentPackEditorField field, string source)
    {
        if (
            !field.MediaExtensions.Contains(
                Path.GetExtension(source),
                StringComparer.OrdinalIgnoreCase
            )
        )
            throw new ArgumentException("Unsupported file type.");
        if (!File.Exists(source))
            throw new FileNotFoundException("The selected media file does not exist.", source);
        string current = ContentPackEditorStorage.GetReplaceableAssetPath(project, field);
        if (current == null)
        {
            ApplyMediaImport(field, source, false);
            return;
        }
        Action replace = ContentPackEditorStorage.HasMatchingMediaFormat(source, current)
            ? () => RunOperation(() => ApplyMediaImport(field, source, true))
            : null;
        view.ConfirmMediaReplacement(
            Path.GetFileName(current),
            replace,
            () => RunOperation(() => ApplyMediaImport(field, source, false))
        );
    }

    /// <summary>
    /// Opens image imports in the last source folder, falling back to the current pack.
    /// </summary>
    /// <returns>The starting directory for an image picker.</returns>
    private string GetImageImportDirectory()
    {
        string previous = settingsManager.Load().ImageImportDirectory;
        return Directory.Exists(previous) ? previous : project.PackRootPath;
    }

    /// <summary>
    /// Records the source folder without interrupting a successful image import.
    /// </summary>
    /// <param name="source">The successfully imported image.</param>
    private void RememberImageImportDirectory(string source)
    {
        try
        {
            settingsManager.SaveImageImportDirectory(Path.GetDirectoryName(source));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            GameLogger.Warning(
                "The image import folder could not be remembered: " + exception.Message
            );
        }
    }

    /// <summary>
    /// Opens non-image imports beside the asset currently resolved by the pack.
    /// </summary>
    /// <param name="field">The field being changed.</param>
    /// <returns>The current file's folder, or the pack folder for a new image.</returns>
    private string GetAssetPickerDirectory(ContentPackEditorField field)
    {
        if (!string.IsNullOrWhiteSpace(field.Value))
            try
            {
                string address = field.GetMediaAddress(field.Value);
                foreach (string extension in field.MediaExtensions)
                {
                    string file = assets.ResolveFile(address, extension);
                    if (file != null)
                        return Path.GetDirectoryName(file);
                }
            }
            catch (ArgumentException)
            {
                // An unresolved address can still be repaired through the file picker.
            }
        return project.PackRootPath;
    }

    /// <summary>
    /// Applies one selected media file either in place or as a separately addressed pack asset.
    /// </summary>
    /// <param name="field">The media field receiving the selected file.</param>
    /// <param name="source">The absolute selected file path.</param>
    /// <param name="replace">Whether to overwrite the field's current pack-owned file.</param>
    private void ApplyMediaImport(ContentPackEditorField field, string source, bool replace)
    {
        if (project == null)
            return;
        if (replace)
        {
            ContentPackEditorStorage.ReplaceAsset(project, source, field);
            if (field.MediaType == ContentPackEditorMediaType.Image)
                RememberImageImportDirectory(source);
            OpenProject(project);
            return;
        }

        ContentPackEditorField[] emptyCuts = GetFirstArtworkCutTargets(field);
        string address = ContentPackEditorStorage.ImportAsset(project, source, field);
        if (field.IsList)
            project.SetFieldValues(
                field,
                field.Values.Where(value => !string.IsNullOrWhiteSpace(value)).Append(address)
            );
        else
            project.SetField(field, address);
        if (field.MediaType == ContentPackEditorMediaType.Image)
            RememberImageImportDirectory(source);
        if (emptyCuts.Length > 0)
        {
            Vector2Int sourceSize = GetArtworkSourceSize(field);
            emptyCuts = emptyCuts
                .Where(target =>
                    ContentEditorUnitArtworkLayout.TryGetCutDimensions(
                        field,
                        target,
                        sourceSize,
                        out _
                    )
                )
                .ToArray();
        }
        workspace.RenderProject(project, assets);
        if (emptyCuts.Length > 0)
            view.ConfirmFirstArtworkCuts(
                emptyCuts.Length,
                () => PreviewArtworkCuts(field, emptyCuts)
            );
    }

    /// <summary>
    /// Finds the empty standard slots to check after importing a unit's first image.
    /// </summary>
    /// <param name="source">The field receiving the image.</param>
    /// <returns>The empty cut destinations, when the unit has no previous artwork.</returns>
    private ContentPackEditorField[] GetFirstArtworkCutTargets(ContentPackEditorField source)
    {
        if (
            !ContentEditorUnitArtworkLayout.IsCutSource(source)
            || ResolveArtworkFile(source) != null
        )
            return Array.Empty<ContentPackEditorField>();
        var others = ContentEditorUnitArtworkLayout
            .GetMembers(source.Record)
            .Where(member => member != source.MemberName)
            .Select(source.Record.GetField)
            .Where(ContentEditorUnitArtworkLayout.IsCutSource)
            .ToArray();
        return others.Any(field => ResolveArtworkFile(field) != null)
            ? Array.Empty<ContentPackEditorField>()
            : others
                .Where(field =>
                    ContentEditorUnitArtworkLayout.IsCutTarget(field)
                    && string.IsNullOrWhiteSpace(field.Value)
                )
                .ToArray();
    }

    /// <summary>
    /// Generates preview textures from the exact PNGs the author can choose to save.
    /// </summary>
    /// <param name="source">The large source image field.</param>
    /// <param name="targets">The ordinary output fields to replace with generated PNGs.</param>
    private void PreviewArtworkCuts(
        ContentPackEditorField source,
        IReadOnlyList<ContentPackEditorField> targets
    )
    {
        RunOperation(() =>
        {
            if (project == null || source == null || targets == null || targets.Count == 0)
                return;
            project.RequireOwnedRecord(source.Record);
            if (string.IsNullOrWhiteSpace(source.Value))
                throw new InvalidOperationException("Choose a large source image first.");
            string sourcePath =
                ResolveArtworkFile(source)
                ?? throw new FileNotFoundException("The selected source image could not be found.");
            Vector2Int sourceSize = GetArtworkSourceSize(source);

            var outputs = targets
                .Distinct()
                .Select(field =>
                {
                    project.RequireOwnedRecord(field.Record);
                    if (field.Record.Element != source.Record.Element)
                        throw new ArgumentException("Artwork outputs must belong to one unit.");
                    if (
                        !ContentEditorUnitArtworkLayout.TryGetCutDimensions(
                            source,
                            field,
                            sourceSize,
                            out Vector2Int dimensions
                        )
                    )
                        throw new ArgumentException(
                            "Choose compatible artwork outputs within the source resolution."
                        );
                    return new { Field = field, Dimensions = dimensions };
                })
                .ToArray();
            Vector2[] offsets = new Vector2[outputs.Length];
            bool[] selected = new bool[outputs.Length];
            Array.Fill(selected, true);
            var encyclopedia = outputs.SingleOrDefault(output =>
                ContentEditorUnitArtworkLayout.IsShipEncyclopediaCut(output.Field)
            );
            Texture2D starfield =
                encyclopedia == null
                    ? null
                    : view.GetShipEncyclopediaCutBackground(encyclopedia.Field);
            ShowCuts();

            void ShowCuts()
            {
                var placements = outputs
                    .Select((output, index) => (output.Dimensions, offsets[index]))
                    .ToArray();
                var gameplay = placements
                    .Where((_, index) => outputs[index] != encyclopedia)
                    .ToArray();
                var cuts =
                    gameplay.Length == 0
                        ? null
                        : ContentEditorArtworkCutGenerator.GeneratePlacedPngCuts(
                            sourcePath,
                            gameplay,
                            fitEntireSource: source.MemberName == "DisplayImagePath",
                            preserveVisibleSubject: true,
                            fillOpaqueSource: true,
                            preventUpscaling: true
                        );
                var images = outputs
                    .Select(
                        (output, index) =>
                            (
                                output.Field,
                                Png: output == encyclopedia
                                    ? ContentEditorArtworkCutGenerator.GenerateFramedPngCut(
                                        sourcePath,
                                        output.Dimensions,
                                        offsets[index]
                                    )
                                    : cuts[placements[index]]
                            )
                    )
                    .ToArray();
                List<(ContentPackEditorField Field, Texture2D Texture)> previews = new();
                try
                {
                    foreach (var image in images)
                    {
                        Texture2D preview = new(2, 2, TextureFormat.RGBA32, false);
                        previews.Add((image.Field, preview));
                        if (!ImageConversion.LoadImage(preview, image.Png, true))
                            throw new InvalidDataException(
                                "A generated cut could not be displayed."
                            );
                    }
                    view.ShowArtworkCuts(
                        previews,
                        selected,
                        included =>
                            RunOperation(() =>
                                ApplyGeneratedArtwork(
                                    "Generate standard artwork cuts",
                                    included
                                        .Select(index =>
                                        {
                                            var image = images[index];
                                            return
                                                outputs[index] == encyclopedia && starfield != null
                                                ? (
                                                    image.Field,
                                                    Png: ContentEditorArtworkCutGenerator.AddBackground(
                                                        image.Png,
                                                        starfield
                                                    )
                                                )
                                                : image;
                                        })
                                        .ToArray()
                                )
                            ),
                        (index, delta) =>
                            RunOperation(() =>
                            {
                                Vector2 offset = offsets[index] + delta;
                                offsets[index] = new Vector2(
                                    Mathf.Clamp(offset.x, -0.5f, 0.5f),
                                    Mathf.Clamp(offset.y, -0.5f, 0.5f)
                                );
                                ShowCuts();
                            }),
                        encyclopediaBackground: starfield
                    );
                }
                catch
                {
                    foreach (var preview in previews)
                        Destroy(preview.Texture);
                    throw;
                }
            }
        });
    }

    /// <summary>
    /// Reads the decoded pixel dimensions through the same cache used by the artwork previews.
    /// </summary>
    /// <param name="field">The field supplying the source image.</param>
    /// <returns>The source dimensions, or zero when the image could not be decoded.</returns>
    private Vector2Int GetArtworkSourceSize(ContentPackEditorField field)
    {
        Texture2D texture = assets.GetTexture(field.GetMediaAddress(field.Value));
        return texture == null ? Vector2Int.zero : new Vector2Int(texture.width, texture.height);
    }

    /// <summary>
    /// Resolves readable artwork from the pack or one of its inherited layers.
    /// </summary>
    /// <param name="field">The image field holding the source reference.</param>
    /// <returns>The file path, or null if the field has no readable source.</returns>
    private string ResolveArtworkFile(ContentPackEditorField field)
    {
        if (field == null || string.IsNullOrWhiteSpace(field.Value))
            return null;
        return ResolveArtworkAddress(field.GetMediaAddress(field.Value));
    }

    /// <summary>
    /// Resolves a pack artwork address to its readable source file.
    /// </summary>
    /// <param name="address">The pack or application image address.</param>
    /// <returns>The readable source file, when available.</returns>
    private string ResolveArtworkAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;
        return assets.ResolveFile(address, ".png")
            ?? assets.ResolveFile(address, ".jpg")
            ?? assets.ResolveFile(address, ".jpeg");
    }

    /// <summary>
    /// Resolves the authored travel artwork with the display image it was registered against.
    /// </summary>
    /// <param name="record">The unit defining the pattern.</param>
    /// <param name="member">The exact gameplay image slot being built.</param>
    /// <param name="displayMember">The matching normal artwork slot.</param>
    /// <returns>The authored pair, or an empty pattern for procedural artwork.</returns>
    private ContentEditorTravelPattern GetRegisteredTravelPattern(
        ContentPackEditorRecord record,
        string member,
        string displayMember
    )
    {
        ContentPackEditorField field = record.GetField(member);
        string authored = field.Value;
        string image =
            string.IsNullOrWhiteSpace(authored)
            || authored.Contains("/Generated/", StringComparison.Ordinal)
                ? null
                : ResolveArtworkAddress(field.GetMediaAddress(authored));
        bool inherited = image == null || field.IsInherited;
        if (image == null)
        {
            authored = field.Document.GetInheritedRecordMember(record.Element, member);
            if (
                string.IsNullOrWhiteSpace(authored)
                || authored.Contains("/Generated/", StringComparison.Ordinal)
            )
                return default;
            image = ResolveArtworkAddress(field.GetMediaAddress(authored));
            if (image == null)
                return default;
        }

        foreach (
            string name in new[]
            {
                displayMember,
                "DisplayImagePath",
                "SmallDisplayImagePath",
            }.Distinct()
        )
        {
            ContentPackEditorField subject = record.GetField(name);
            string value = inherited
                ? field.Document.GetInheritedRecordMember(record.Element, name) ?? subject.Value
                : subject.Value;
            if (string.IsNullOrWhiteSpace(value))
                continue;
            string path = ResolveArtworkAddress(subject.GetMediaAddress(value));
            if (path != null)
                return new ContentEditorTravelPattern(image, path);
        }
        return default;
    }

    /// <summary>
    /// Captures template files with the generated draft so its geometry survives pack edits.
    /// </summary>
    /// <param name="path">The resolved location of the pattern or hull.</param>
    /// <returns>The portable address of the copy stored in the pack.</returns>
    private string StoreTravelPatternSource(string path) =>
        string.IsNullOrEmpty(path)
            ? null
            : ContentPackEditorStorage.StoreArtworkLayerSource(project, path);

    /// <summary>
    /// Begins a repeatable preview of the applicable gameplay images for one unit.
    /// </summary>
    /// <param name="selectedField">The state image selected on the artwork page.</param>
    /// <param name="effect">The gameplay effect to edit or generate.</param>
    /// <param name="generate">Whether to generate the missing outputs directly on the unit page.</param>
    private void PreviewArtworkEffect(
        ContentPackEditorField selectedField,
        ContentEditorArtworkEffect effect,
        bool generate
    )
    {
        RunOperation(() =>
        {
            ContentPackEditorRecord record = selectedField.Record;
            project.RequireOwnedRecord(record);
            if (!ContentEditorArtworkEffectGenerator.TryGetStyle(record, effect, out var style))
                throw new ArgumentException(
                    "This unit has no artwork slot for that effect.",
                    nameof(effect)
                );
            string prefix = effect == ContentEditorArtworkEffect.Travel ? "InTransit" : "Damaged";
            var fields = ContentEditorUnitArtworkLayout
                .GetMembers(record)
                .ToDictionary(
                    member => member,
                    member => record.GetField(member),
                    StringComparer.Ordinal
                );
            string display = ResolveArtworkFile(fields.GetValueOrDefault("DisplayImagePath"));
            string compact = ResolveArtworkFile(fields.GetValueOrDefault("SmallDisplayImagePath"));
            string battle = ResolveArtworkFile(fields.GetValueOrDefault("BattleResultImagePath"));
            var outputs = new[]
            {
                (
                    Member: prefix + "SmallImagePath",
                    Caption: "Small",
                    SubjectMember: "SmallDisplayImagePath",
                    Source: compact ?? display,
                    MatchSource: style == ContentEditorArtworkEffectStyle.FacilityTravel
                        && compact != null
                ),
                (
                    Member: prefix + "ImagePath",
                    Caption: "Large",
                    SubjectMember: "DisplayImagePath",
                    Source: display ?? compact,
                    MatchSource: false
                ),
                (
                    Member: "BattleResult" + prefix + "ImagePath",
                    Caption: "Battle result",
                    SubjectMember: "BattleResultImagePath",
                    Source: battle ?? compact ?? display,
                    MatchSource: false
                ),
            }
                .Where(output => fields.ContainsKey(output.Member))
                .Select(output =>
                {
                    ContentPackEditorField field = fields[output.Member];
                    if (
                        !ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(
                            field,
                            out Vector2Int dimensions
                        )
                    )
                        throw new InvalidOperationException(
                            "The gameplay image has no configured output size."
                        );
                    return (
                        Field: field,
                        output.Caption,
                        output.SubjectMember,
                        output.Source,
                        Existing: ResolveArtworkFile(field),
                        Size: output.MatchSource ? (Vector2Int?)null : dimensions,
                        ExpectedSize: dimensions,
                        StretchSource: record.Kind == "CapitalShip"
                            && output.Member == prefix + "ImagePath"
                    );
                })
                .ToArray();
            if (outputs.Length == 0)
                throw new InvalidOperationException(
                    "This unit has no output for that gameplay effect."
                );

            bool shipEffects =
                style
                is ContentEditorArtworkEffectStyle.ShipTravel
                    or ContentEditorArtworkEffectStyle.Damage;
            Texture2D previewBackground =
                shipEffects ? view.GetShipEffectBackground()
                : style == ContentEditorArtworkEffectStyle.RegimentTravel
                    ? view.GetTroopTransitBackground()
                : style == ContentEditorArtworkEffectStyle.PersonnelTravel
                    ? view.GetPersonnelTransitBackground()
                : null;
            int initialFrame = Math.Max(
                0,
                Array.FindIndex(
                    outputs,
                    output => output.Field.MemberName == selectedField.MemberName
                )
            );
            int defaultSeed = ContentEditorArtworkEffectGenerator.GetSeed(record.ID, effect);
            if (outputs[initialFrame].Source == null && outputs[initialFrame].Existing == null)
                throw new InvalidOperationException(
                    "Choose a display image or a small image first."
                );
            int activeOutput = initialFrame;
            var drafts = new ContentEditorArtworkRecipe[outputs.Length];
            var savedRecipes = new string[outputs.Length];
            var frames = new ContentEditorArtworkEffectGenerator.EditableFrame[outputs.Length];
            ContentEditorArtworkEffectGenerator.EditableFrame[] currentFrames = null;
            ContentEditorTravelPattern[] travelPatterns = null;
            Dictionary<string, byte[]> pendingLayerSources = new(StringComparer.Ordinal);
            Stack<(int Output, string Recipe)> undo = new();
            Stack<(int Output, string Recipe)> redo = new();
            int selected = -1;
            try
            {
                for (int index = 0; index < outputs.Length; index++)
                {
                    var output = outputs[index];
                    if (generate && output.Existing != null)
                        continue;
                    var draft = generate
                        ? null
                        : ContentEditorArtworkRecipe.ReadForOutput(
                            ContentPackEditorStorage.ReadGeneratedLayers(project, output.Field),
                            output.Field.MemberName,
                            index,
                            outputs.Length,
                            shipEffects
                        );
                    bool initialize = draft == null;
                    draft ??= new ContentEditorArtworkRecipe
                    {
                        OutputMember = output.Field.MemberName,
                        Seed = defaultSeed,
                        UsesExisting = output.Existing != null,
                    };
                    drafts[index] = draft;
                    frames[index] = BuildFrame(draft, index);
                    bool upgradedComposition = false;
                    if (initialize)
                        CreateDefaultLayers(index);
                    else if (draft.NeedsCompositionLayerUpgrade)
                    {
                        UpgradeCompositionLayers(index);
                        upgradedComposition = true;
                    }
                    if (output.Existing != null || index != initialFrame)
                        savedRecipes[index] = upgradedComposition
                            ? null
                            : JsonUtility.ToJson(draft);
                }
                if (generate)
                {
                    Save();
                    DisposeFrames(frames);
                }
                else
                {
                    SelectDefaultLayer();
                    Refresh();
                }
            }
            catch
            {
                DisposeFrames(frames);
                throw;
            }

            static void DisposeFrames(ContentEditorArtworkEffectGenerator.EditableFrame[] values)
            {
                if (values == null)
                    return;
                foreach (var frame in values)
                    frame?.Dispose();
                Array.Clear(values, 0, values.Length);
            }

            string ExistingSource(ContentEditorArtworkRecipe draft, int index)
            {
                if (!draft.UsesExisting)
                    return null;
                return draft.HasStoredSources
                    ? ResolveArtworkAddress(draft.ExistingSources[0])
                    : outputs[index].Existing;
            }

            ContentEditorTravelPattern[] ResolveTravelPatterns()
            {
                ContentEditorTravelPattern shared = new(
                    ResolveArtworkAddress(view.GetUnitTravelBackgroundAddress()),
                    null
                );
                var candidates = project
                    .GetCatalogs()
                    .SelectMany(catalog => catalog.Records)
                    .Where(candidate =>
                        candidate.Kind == record.Kind && candidate.Element != record.Element
                    )
                    .OrderByDescending(candidate =>
                        candidate.Element.Element("BuildingType")?.Value
                        == record.Element.Element("BuildingType")?.Value
                    )
                    .ThenByDescending(candidate =>
                        candidate.Catalog.Faction == record.Catalog.Faction
                    )
                    .ToArray();
                ContentPackEditorRecord shipDonor = null;
                bool selectedDonor = false;
                return outputs
                    .Select(output =>
                    {
                        if (style == ContentEditorArtworkEffectStyle.RegimentTravel)
                            return shared;
                        string member = output.Field.MemberName;
                        ContentEditorTravelPattern exact = GetRegisteredTravelPattern(
                            record,
                            member,
                            output.SubjectMember
                        );
                        if (exact.Image == null)
                            exact = ContentEditorTravelReference.ResolveUnitPattern(
                                project,
                                record,
                                member,
                                output.SubjectMember
                            );
                        if (exact.Image != null)
                            return exact;
                        if (style == ContentEditorArtworkEffectStyle.ShipTravel)
                        {
                            if (!selectedDonor)
                            {
                                var examples = candidates
                                    .Select(candidate =>
                                        (
                                            Record: candidate,
                                            Pattern: GetRegisteredTravelPattern(
                                                candidate,
                                                "InTransitSmallImagePath",
                                                "SmallDisplayImagePath"
                                            )
                                        )
                                    )
                                    .Where(candidate => candidate.Pattern.Image != null)
                                    .ToArray();
                                var donor =
                                    ContentEditorArtworkEffectGenerator.SelectShipTravelPattern(
                                        compact ?? display,
                                        examples.Select(candidate => candidate.Pattern).ToArray()
                                    );
                                shipDonor = examples
                                    .FirstOrDefault(candidate => candidate.Pattern.Equals(donor))
                                    .Record;
                                selectedDonor = true;
                            }
                            return shipDonor == null
                                ? default
                                : GetRegisteredTravelPattern(
                                    shipDonor,
                                    member,
                                    output.SubjectMember
                                );
                        }
                        if (style == ContentEditorArtworkEffectStyle.FacilityTravel)
                        {
                            var facility = candidates
                                .Where(candidate =>
                                    ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(
                                        candidate.GetField(member),
                                        out Vector2Int dimensions
                                    )
                                    && dimensions == output.ExpectedSize
                                )
                                .Select(candidate =>
                                    GetRegisteredTravelPattern(
                                        candidate,
                                        member,
                                        output.SubjectMember
                                    )
                                )
                                .FirstOrDefault(candidate => candidate.Image != null);
                            if (facility.Image != null)
                                return facility;
                        }
                        return shared;
                    })
                    .ToArray();
            }

            ContentEditorTravelPattern GetTravelPattern(ContentEditorArtworkRecipe draft, int index)
            {
                if (draft.TravelPatterns?.Length == 1 && draft.TravelSubjects?.Length == 1)
                {
                    return new ContentEditorTravelPattern(
                        ResolveArtworkAddress(draft.TravelPatterns[0]),
                        ResolveArtworkAddress(draft.TravelSubjects[0])
                    );
                }
                return (travelPatterns ??= ResolveTravelPatterns())[index];
            }

            byte[] LoadLayerSource(string address)
            {
                if (pendingLayerSources.TryGetValue(address, out byte[] image))
                    return image;
                string path = ResolveArtworkAddress(address);
                return path == null ? null : File.ReadAllBytes(path);
            }

            ContentEditorArtworkEffectGenerator.EditableFrame BuildFrame(
                ContentEditorArtworkRecipe draft,
                int index
            )
            {
                ContentEditorArtworkEffectGenerator.EditableFrame frame;
                var output = outputs[index];
                string existing = ExistingSource(draft, index);
                bool completeTravel =
                    effect == ContentEditorArtworkEffect.Travel
                    && draft.RenderVersion >= 5
                    && !draft.UsesExisting;
                ContentEditorTravelPattern pattern =
                    completeTravel
                    || (
                        effect == ContentEditorArtworkEffect.Travel
                        && draft.RenderVersion >= 5
                        && style == ContentEditorArtworkEffectStyle.FacilityTravel
                    )
                        ? GetTravelPattern(draft, index)
                        : default;
                bool facilityCard =
                    style == ContentEditorArtworkEffectStyle.FacilityTravel
                    && output.ExpectedSize == new Vector2Int(302, 158);
                int? clearance =
                    draft.RenderVersion < 4 || !facilityCard ? 0
                    : draft.FacilityClearance >= 0 ? draft.FacilityClearance
                    : null;
                string reference =
                    clearance == null && !(output.Field.Value?.Contains("/Generated/") ?? false)
                        ? output.Existing
                    : clearance == null && pattern.Subject == output.Source ? pattern.Image
                    : null;
                try
                {
                    frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                        output.Source,
                        completeTravel ? output.ExpectedSize : output.Size,
                        style,
                        draft.Seed,
                        output.StretchSource,
                        previewBackground,
                        existing,
                        draft.Variant,
                        legacyStreaks: draft.RenderVersion == 2,
                        facilityClearance: clearance,
                        facilityReferencePath: reference,
                        completeTravel: completeTravel,
                        travelPattern: pattern,
                        preserveRegisteredTravel: completeTravel && draft.RenderVersion >= 6,
                        varyShipWakes: draft.VaryShipWakes,
                        loadLayerSource: LoadLayerSource
                    );
                }
                catch (InvalidDataException)
                    when (style == ContentEditorArtworkEffectStyle.FacilityTravel
                        && output.Source == compact
                        && display != null
                        && display != compact
                    )
                {
                    ContentEditorTravelPattern fallback =
                        pattern.Subject == null
                            ? pattern
                            : new ContentEditorTravelPattern(
                                ResolveArtworkAddress(view.GetUnitTravelBackgroundAddress()),
                                null
                            );
                    if (
                        completeTravel
                        && travelPatterns != null
                        && draft.TravelPatterns?.Length != 1
                    )
                        travelPatterns[index] = fallback;
                    frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                        display,
                        output.ExpectedSize,
                        style,
                        draft.Seed,
                        false,
                        previewBackground,
                        existing,
                        draft.Variant,
                        legacyStreaks: draft.RenderVersion == 2,
                        facilityClearance: clearance,
                        completeTravel: completeTravel,
                        travelPattern: fallback,
                        loadLayerSource: LoadLayerSource
                    );
                }
                if (draft.RenderVersion >= 4 && draft.FacilityClearance < 0)
                    draft.FacilityClearance = frame.FacilityClearance;
                return frame;
            }

            void CreateDefaultLayers(int index)
            {
                drafts[index].Layers = frames[index].CreateDefaultLayers();
                drafts[index].UpgradeCompositionLayerVersion();
            }

            void UpgradeCompositionLayers(int index)
            {
                var recipe = drafts[index];
                var frame = frames[index];
                var linked = frame
                    .CreateDefaultLayers()
                    .Where(layer => layer.Role != ContentEditorArtworkLayerRole.Effect)
                    .ToArray();
                foreach (var layer in linked)
                {
                    int insertion =
                        layer.Role == ContentEditorArtworkLayerRole.Background ? 0
                        : style
                            is ContentEditorArtworkEffectStyle.ShipTravel
                                or ContentEditorArtworkEffectStyle.Damage
                            ? recipe.Layers.FindLastIndex(item =>
                                item.Role == ContentEditorArtworkLayerRole.Background
                            ) + 1
                        : recipe.Layers.Count;
                    recipe.Layers.Insert(Mathf.Max(0, insertion), layer);
                }
                recipe.UpgradeCompositionLayerVersion();
            }

            void SelectDefaultLayer()
            {
                var layers = drafts[activeOutput].Layers;
                int facility = frames[activeOutput].FacilityLayerIndex;
                int preferred =
                    facility >= 0
                        ? layers.FindIndex(layer =>
                            layer.Role == ContentEditorArtworkLayerRole.Effect
                            && layer.Source == facility
                        )
                        : layers.FindIndex(layer =>
                            layer.Role == ContentEditorArtworkLayerRole.Effect
                        );
                selected = layers.Count == 0 ? -1 : Mathf.Max(0, preferred);
            }

            void RememberEdit()
            {
                undo.Push((activeOutput, JsonUtility.ToJson(drafts[activeOutput])));
                redo.Clear();
            }

            void RestoreEdit(
                Stack<(int Output, string Recipe)> from,
                Stack<(int Output, string Recipe)> to
            )
            {
                if (from.Count == 0)
                    return;
                var entry = from.Peek();
                int index = entry.Output;
                var current = drafts[index];
                var restored = JsonUtility.FromJson<ContentEditorArtworkRecipe>(entry.Recipe);
                if (
                    current.Seed != restored.Seed
                    || current.Variant != restored.Variant
                    || current.RenderVersion != restored.RenderVersion
                    || current.FacilityClearance != restored.FacilityClearance
                    || current.UsesExisting != restored.UsesExisting
                    || current.VaryShipWakes != restored.VaryShipWakes
                )
                {
                    var frame = BuildFrame(restored, index);
                    frames[index].Dispose();
                    frames[index] = frame;
                }
                to.Push((index, JsonUtility.ToJson(current)));
                from.Pop();
                drafts[index] = restored;
                bool switched = activeOutput != index;
                activeOutput = index;
                if (switched)
                    SelectDefaultLayer();
                else
                    selected = Mathf.Min(selected < 0 ? 0 : selected, restored.Layers.Count - 1);
                Refresh();
            }

            void MoveLayer(int index, Vector2 offset)
            {
                var recipe = drafts[activeOutput];
                if (
                    index < 0
                    || index >= recipe.Layers.Count
                    || recipe.Layers[index].Role != ContentEditorArtworkLayerRole.Effect
                    || offset.sqrMagnitude < float.Epsilon
                )
                    return;
                selected = index;
                RememberEdit();
                ContentEditorArtworkLayer layer = recipe.Layers[index];
                layer.Offset += offset;
                recipe.Layers[index] = layer;
                Refresh();
            }

            void ResizeLayer(int index, int percent)
            {
                var recipe = drafts[activeOutput];
                if (index < 0 || index >= recipe.Layers.Count)
                    return;
                ContentEditorArtworkLayer layer = recipe.Layers[index];
                if (
                    !frames[activeOutput].CanScaleLayer(layer)
                    || percent == layer.EffectiveScalePercent
                )
                    return;
                selected = index;
                RememberEdit();
                layer.ScalePercent =
                    percent == ContentEditorArtworkLayer.DefaultScalePercent ? 0 : percent;
                recipe.Layers[index] = layer;
                Refresh();
            }

            void RemoveLayer()
            {
                var recipe = drafts[activeOutput];
                if (selected < 0)
                    return;
                if (recipe.Layers[selected].Role != ContentEditorArtworkLayerRole.Effect)
                    return;
                RememberEdit();
                recipe.Layers.RemoveAt(selected);
                selected = Mathf.Min(selected, recipe.Layers.Count - 1);
                Refresh();
            }

            void AddLayer()
            {
                var recipe = drafts[activeOutput];
                RememberEdit();
                var layer = new ContentEditorArtworkLayer
                {
                    Source = -1,
                    Seed = unchecked(recipe.Seed + ++recipe.NextAddition * 7919),
                    MeasuredStreaks = true,
                    VisibleStreakGroup = true,
                };
                layer.Name = LayerName(layer, recipe.Layers.Count, frames[activeOutput], recipe);
                InsertLayer(layer);
            }

            int LayerInsertionIndex(ContentEditorArtworkRecipe recipe)
            {
                int facility = frames[activeOutput].FacilityLayerIndex;
                int foreground =
                    facility < 0 ? -1 : recipe.Layers.FindIndex(item => item.Source == facility);
                if (
                    foreground < 0
                    && style
                        is ContentEditorArtworkEffectStyle.PersonnelTravel
                            or ContentEditorArtworkEffectStyle.RegimentTravel
                )
                    foreground = recipe.Layers.FindIndex(item =>
                        item.Role == ContentEditorArtworkLayerRole.Subject
                    );
                return foreground < 0 ? recipe.Layers.Count : foreground;
            }

            void InsertLayer(ContentEditorArtworkLayer layer)
            {
                var recipe = drafts[activeOutput];
                selected = LayerInsertionIndex(recipe);
                recipe.Layers.Insert(selected, layer);
                Refresh();
            }

            void PickLayerImage()
            {
                string path = ContentEditorFileDialog.OpenFile(
                    GetImageImportDirectory(),
                    outputs[activeOutput].Field.MediaExtensions
                );
                if (!string.IsNullOrEmpty(path))
                    ImportLayer(path);
            }

            void ImportLayer(string path)
            {
                string extension = Path.GetExtension(path);
                if (
                    !outputs[activeOutput]
                        .Field.MediaExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
                )
                    throw new ArgumentException("Choose a PNG or JPEG image.");

                byte[] image = File.ReadAllBytes(path);
                string address = ContentPackEditorStorage.GetArtworkLayerSourceAddress(
                    image,
                    extension
                );
                var layer = new ContentEditorArtworkLayer
                {
                    Source = -1,
                    ImageSource = address,
                    Name = Path.GetFileNameWithoutExtension(path),
                };
                bool staged = pendingLayerSources.TryAdd(address, image);
                try
                {
                    frames[activeOutput].GetLayer(layer);
                }
                catch
                {
                    if (staged)
                        pendingLayerSources.Remove(address);
                    throw;
                }
                RememberEdit();
                InsertLayer(layer);
                RememberImageImportDirectory(path);
            }

            void DuplicateLayer()
            {
                var recipe = drafts[activeOutput];
                if (selected < 0 || selected >= recipe.Layers.Count)
                    return;
                if (recipe.Layers[selected].Role != ContentEditorArtworkLayerRole.Effect)
                    return;
                RememberEdit();
                ContentEditorArtworkLayer copy = recipe.Layers[selected];
                string name = LayerName(copy, selected, frames[activeOutput], recipe) + " copy";
                copy.Name = name;
                for (int suffix = 2; recipe.Layers.Any(layer => layer.Name == copy.Name); suffix++)
                    copy.Name = $"{name} {suffix}";
                recipe.Layers.Insert(++selected, copy);
                Refresh();
            }

            void ToggleLayer(int index)
            {
                var recipe = drafts[activeOutput];
                if (index < 0 || index >= recipe.Layers.Count)
                    return;
                if (recipe.Layers[index].Role != ContentEditorArtworkLayerRole.Effect)
                    return;
                RememberEdit();
                ContentEditorArtworkLayer layer = recipe.Layers[index];
                layer.Hidden = !layer.Hidden;
                recipe.Layers[index] = layer;
                selected = index;
                Refresh();
            }

            void ReorderLayer(int index, int target)
            {
                var recipe = drafts[activeOutput];
                if (
                    index < 0
                    || index >= recipe.Layers.Count
                    || target < 0
                    || target >= recipe.Layers.Count
                    || target == index
                )
                    return;
                RememberEdit();
                (recipe.Layers[index], recipe.Layers[target]) = (
                    recipe.Layers[target],
                    recipe.Layers[index]
                );
                selected = target;
                Refresh();
            }

            void Regenerate(bool varyShipWakes)
            {
                if (outputs[activeOutput].Source == null)
                    throw new InvalidOperationException(
                        "Choose a display image or a small image first."
                    );
                var recipe = drafts[activeOutput];
                var replacement = new ContentEditorArtworkRecipe
                {
                    OutputMember = outputs[activeOutput].Field.MemberName,
                    Seed = varyShipWakes
                        ? Guid.NewGuid().GetHashCode()
                        : unchecked(recipe.Seed + 104729),
                    VaryShipWakes = varyShipWakes,
                    Variant =
                        varyShipWakes
                        || recipe.VaryShipWakes
                        || recipe.UsesExisting
                        || style == ContentEditorArtworkEffectStyle.FacilityTravel
                            ? 0
                            : (recipe.Variant + 1) % 3,
                };
                var frame = BuildFrame(replacement, activeOutput);
                RememberEdit();
                frames[activeOutput].Dispose();
                frames[activeOutput] = frame;
                drafts[activeOutput] = replacement;
                CreateDefaultLayers(activeOutput);
                replacement.Layers.InsertRange(
                    LayerInsertionIndex(replacement),
                    recipe.Layers.Where(layer => !string.IsNullOrEmpty(layer.ImageSource))
                );
                SelectDefaultLayer();
                Refresh();
            }

            bool HasChanges(int index) =>
                drafts[index] != null
                && (
                    drafts[index].NeedsImageLayerUpgrade
                    || savedRecipes[index] != JsonUtility.ToJson(drafts[index])
                );

            void Save()
            {
                var images = new List<(ContentPackEditorField Field, byte[] Png, string Layers)>();
                HashSet<string> layerSourcesToSave = new(StringComparer.Ordinal);
                for (int index = 0; index < outputs.Length; index++)
                {
                    var draft = drafts[index];
                    if (draft == null || (!generate && !HasChanges(index)))
                        continue;
                    var output = outputs[index];
                    if (draft.UsesExisting && !draft.HasStoredSources && output.Existing != null)
                        draft.ExistingSources = new[]
                        {
                            ContentPackEditorStorage.StoreArtworkLayerSource(
                                project,
                                output.Existing
                            ),
                        };
                    if (
                        effect == ContentEditorArtworkEffect.Travel
                        && draft.RenderVersion >= 5
                        && !draft.UsesExisting
                        && draft.TravelPatterns?.Length != 1
                    )
                    {
                        var pattern = GetTravelPattern(draft, index);
                        draft.TravelPatterns = new[] { StoreTravelPatternSource(pattern.Image) };
                        draft.TravelSubjects = new[] { StoreTravelPatternSource(pattern.Subject) };
                    }
                    byte[] png = frames[index].Bake(draft.Layers);
                    draft.UpgradeImageLayerVersion();
                    images.Add((output.Field, png, JsonUtility.ToJson(draft)));
                    foreach (var layer in draft.Layers)
                        if (
                            layer.ImageSource != null
                            && pendingLayerSources.ContainsKey(layer.ImageSource)
                        )
                            layerSourcesToSave.Add(layer.ImageSource);
                }

                foreach (string address in layerSourcesToSave)
                    ContentPackEditorStorage.StoreArtworkLayerSource(
                        project,
                        address,
                        pendingLayerSources[address]
                    );
                bool persist = !project.IsDirty;
                ApplyGeneratedArtwork(
                    $"Save {effect.ToString().ToLowerInvariant()} artwork",
                    images,
                    persist
                );
                string saved = generate
                    ? $"Generated {images.Count} travel {(images.Count == 1 ? "image" : "images")}."
                    : "Artwork saved.";
                view.RenderStatus(
                    persist
                        ? saved
                        : "Artwork applied. Save pack to write the updated assignments.",
                    false
                );
            }

            string LayerName(
                ContentEditorArtworkLayer definition,
                int index,
                ContentEditorArtworkEffectGenerator.EditableFrame frame,
                ContentEditorArtworkRecipe draft
            )
            {
                if (!string.IsNullOrWhiteSpace(definition.Name))
                    return definition.Name;
                if (definition.Role == ContentEditorArtworkLayerRole.Background)
                    return "Background";
                if (definition.Role == ContentEditorArtworkLayerRole.Subject)
                    return "Unit artwork";
                int ordinal = definition.Source < 0 ? index + 1 : definition.Source + 1;
                return style switch
                {
                    ContentEditorArtworkEffectStyle.ShipTravel => $"Wake {ordinal}",
                    ContentEditorArtworkEffectStyle.Damage => $"Explosion {ordinal}",
                    ContentEditorArtworkEffectStyle.FacilityTravel
                        when definition.Source == frame.FacilityLayerIndex
                            && definition.Source >= 0 => "Facility",
                    ContentEditorArtworkEffectStyle.PersonnelTravel
                        when draft.UsesExisting && definition.Source >= 3 =>
                        $"Existing accent {definition.Source - 2}",
                    _ when draft.RenderVersion >= 6
                            && !draft.UsesExisting
                            && definition.Source == 0 => "Travel field",
                    _ => $"Streaks {ordinal}",
                };
            }

            ArtworkEffectPreviewFrame MakePreview(
                ContentEditorArtworkEffectGenerator.EditableFrame frame,
                int index,
                IReadOnlyList<ContentEditorArtworkLayer> definitions,
                ContentEditorArtworkRecipe draft,
                bool missing = false
            )
            {
                var layers = new List<ArtworkEffectLayerPreview>();
                Vector2 size = frame.Size;
                for (int layerIndex = 0; layerIndex < definitions.Count; layerIndex++)
                {
                    var definition = definitions[layerIndex];
                    var piece = frame.GetLayer(definition);
                    RectInt placement = frame.GetLayerBounds(definition, piece.Bounds);
                    Rect bounds = new(
                        (Vector2)placement.min / size,
                        (Vector2)placement.size / size
                    );
                    layers.Add(
                        new ArtworkEffectLayerPreview(
                            layerIndex,
                            LayerName(definition, layerIndex, frame, draft),
                            piece.Texture,
                            bounds,
                            definition.Role == ContentEditorArtworkLayerRole.Effect
                                ? definition.Offset
                                : default,
                            definition.Hidden
                                && definition.Role == ContentEditorArtworkLayerRole.Effect,
                            linked: definition.Role != ContentEditorArtworkLayerRole.Effect,
                            scalePercent: frame.CanScaleLayer(definition)
                                ? definition.EffectiveScalePercent
                                : null
                        )
                    );
                }
                return new ArtworkEffectPreviewFrame(
                    outputs[index].Caption,
                    frame.BaseImage,
                    layers,
                    missing
                );
            }

            IReadOnlyList<ArtworkEffectPreviewFrame> ReadCurrent()
            {
                if (currentFrames == null)
                {
                    var replacement = new ContentEditorArtworkEffectGenerator.EditableFrame[
                        outputs.Length
                    ];
                    try
                    {
                        for (int index = 0; index < outputs.Length; index++)
                        {
                            var currentDraft = CurrentDraft();
                            replacement[index] = BuildFrame(currentDraft, index);
                        }
                    }
                    catch
                    {
                        DisposeFrames(replacement);
                        throw;
                    }
                    currentFrames = replacement;
                }
                return currentFrames
                    .Select(
                        (frame, index) =>
                        {
                            bool missing = outputs[index].Existing == null;
                            IReadOnlyList<ContentEditorArtworkLayer> layers = missing
                                ? frame
                                    .CreateDefaultLayers()
                                    .Where(layer =>
                                        layer.Role != ContentEditorArtworkLayerRole.Effect
                                        || layer.Source == frame.FacilityLayerIndex
                                    )
                                    .ToArray()
                                : frame.CreateDefaultLayers();
                            return MakePreview(frame, index, layers, CurrentDraft(), missing);
                        }
                    )
                    .ToArray();
            }

            ContentEditorArtworkRecipe CurrentDraft() =>
                new() { Seed = defaultSeed, UsesExisting = true };

            void Refresh()
            {
                var previews = frames
                    .Select(
                        (frame, index) =>
                            MakePreview(frame, index, drafts[index].Layers, drafts[index])
                    )
                    .ToArray();
                bool hasChanges = Enumerable.Range(0, drafts.Length).Any(HasChanges);
                view.ShowGeneratedArtwork(
                    effect == ContentEditorArtworkEffect.Travel
                        ? "Travel artwork"
                        : "Damage artwork",
                    record.Name,
                    previews,
                    activeOutput,
                    selected,
                    hasChanges,
                    new ArtworkEffectActions
                    {
                        Select = index => selected = index,
                        OutputChanged = index =>
                        {
                            activeOutput = index;
                            SelectDefaultLayer();
                            return selected;
                        },
                        Move = (index, offset) => RunOperation(() => MoveLayer(index, offset)),
                        Resize = (index, percent) =>
                            RunOperation(() => ResizeLayer(index, percent)),
                        Toggle = index => RunOperation(() => ToggleLayer(index)),
                        Reorder = (index, target) =>
                            RunOperation(() => ReorderLayer(index, target)),
                        Add = () => RunOperation(AddLayer),
                        Import = () => RunOperation(PickLayerImage),
                        ImportDroppedImage = path => RunOperation(() => ImportLayer(path)),
                        Duplicate = () => RunOperation(DuplicateLayer),
                        Remove = () => RunOperation(RemoveLayer),
                        Rebuild = () => RunOperation(() => Regenerate(false)),
                        VaryShipWakes =
                            style == ContentEditorArtworkEffectStyle.ShipTravel
                                ? () => RunOperation(() => Regenerate(true))
                                : null,
                        AddLabel = style switch
                        {
                            ContentEditorArtworkEffectStyle.ShipTravel => "Add wake",
                            ContentEditorArtworkEffectStyle.Damage => "Add explosion",
                            _ => "Add streaks",
                        },
                        RebuildLabel =
                            effect == ContentEditorArtworkEffect.Travel
                                ? "Rebuild travel artwork"
                                : "Regenerate damage artwork",
                        Save = () => RunOperation(Save),
                        Undo =
                            undo.Count == 0
                                ? null
                                : () => RunOperation(() => RestoreEdit(undo, redo)),
                        Redo =
                            redo.Count == 0
                                ? null
                                : () => RunOperation(() => RestoreEdit(redo, undo)),
                        ReadCurrent = () =>
                        {
                            IReadOnlyList<ArtworkEffectPreviewFrame> result = null;
                            RunOperation(() => result = ReadCurrent());
                            return result;
                        },
                        Close = () =>
                        {
                            DisposeFrames(frames);
                            DisposeFrames(currentFrames);
                        },
                    }
                );
            }
        });
    }

    /// <summary>
    /// Applies standard cuts without attaching any layer metadata.
    /// </summary>
    /// <param name="description">The description of the operation in the undo history.</param>
    /// <param name="images">The previewed images and their destination fields.</param>
    private void ApplyGeneratedArtwork(
        string description,
        IReadOnlyList<(ContentPackEditorField Field, byte[] Png)> images
    ) =>
        ApplyGeneratedArtwork(
            description,
            images.Select(image => (image.Field, image.Png, (string)null)).ToArray(),
            false
        );

    /// <summary>
    /// Writes approved artwork before replacing its unit references in one undo step.
    /// </summary>
    /// <param name="description">The description of the operation in the undo history.</param>
    /// <param name="images">The previewed images and their destination fields.</param>
    /// <param name="persist">Whether the current project can be saved with this change.</param>
    private void ApplyGeneratedArtwork(
        string description,
        IReadOnlyList<(ContentPackEditorField Field, byte[] Png, string Layers)> images,
        bool persist
    )
    {
        var references = images
            .Select(image =>
            {
                string value = ContentPackEditorStorage.WriteGeneratedPng(
                    project,
                    image.Field,
                    image.Png,
                    preservePrevious: true,
                    layerRecipe: image.Layers
                );
                return (image.Field, Value: value);
            })
            .ToArray();
        using (project.BeginEdit(description))
        {
            foreach (var reference in references)
                project.SetField(reference.Field, reference.Value);
        }
        if (persist)
            ContentPackEditorStorage.Save(project);
        OpenProject(project);
    }

    /// <summary>
    /// Creates a replacement asset store before releasing the previous project.
    /// </summary>
    /// <param name="value">The project to open.</param>
    private void OpenProject(ContentPackEditorProject value)
    {
        ContentAssets replacement = ContentPackEditorStorage.CreateAssets(value);
        ContentAssets previous = assets;
        try
        {
            workspace.RenderProject(value, replacement);
        }
        catch
        {
            try
            {
                if (project != null)
                    workspace.RenderProject(project, previous);
                else
                {
                    workspace.ClearProject();
                    ContentBindings.Apply(view.gameObject, applicationAssets);
                }
            }
            finally
            {
                replacement.Dispose();
            }
            throw;
        }
        project = value;
        assets = replacement;
        previous?.Dispose();
    }

    /// <summary>
    /// Commits form edits and saves all changed documents.
    /// </summary>
    private void SavePack()
    {
        RunOperation(SaveCurrentProject);
    }

    /// <summary>
    /// Saves the project or throws before a dependent transition can occur.
    /// </summary>
    private void SaveCurrentProject()
    {
        if (project == null)
            return;
        if (!view.ValidateFields())
            throw new ArgumentException("Fix the highlighted fields before saving.");
        PersistCurrentProject();
    }

    /// <summary>
    /// Persists an already-committed project before dependent validation or transitions.
    /// </summary>
    private void PersistCurrentProject()
    {
        string previousBase = project.Definition.BasePackID;
        ContentPackEditorStorage.Save(project);
        if (previousBase != project.Definition.BasePackID)
            OpenProject(project);
        else
            view.RefreshProjectState();
        view.RenderStatus("Content pack saved.", false);
    }

    /// <summary>
    /// Saves and validates content with the production loader.
    /// </summary>
    private async void ValidatePack()
    {
        await RunOperationAsync(async cancellationToken =>
        {
            if (project == null)
                return;
            if (!view.CommitFields())
                throw new ArgumentException("Fix the highlighted fields before validating.");
            PersistCurrentProject();
            IReadOnlyList<ContentEditorValidationIssue> fieldErrors =
                ContentEditorPackValidator.GetFieldErrors(project);
            if (fieldErrors.Count > 0)
            {
                view.RenderValidationErrors(fieldErrors);
                return;
            }
            view.RenderStatus("Validating content pack. Press Escape to cancel.", false);
            IReadOnlyList<string> warnings = await ContentEditorPackValidator.ValidateAsync(
                project,
                ReportValidationProgress,
                cancellationToken
            );
            view.RenderValidationResult("Content pack validated successfully.", warnings);
        });
    }

    /// <summary>
    /// Runs map-layout calculations without blocking rendering or cancellation input.
    /// </summary>
    /// <param name="layout">The captured source geometry and requested records.</param>
    private async void ArrangeMap(ContentEditorMapLayout layout)
    {
        await RunOperationAsync(async cancellationToken =>
        {
            view.RenderStatus("Arranging map. Press Escape to cancel.", false);
            int moved = await workspace.ArrangeMapAsync(layout, cancellationToken);
            view.RenderStatus(
                moved == 0
                    ? "No layout changes needed."
                    : "Map arranged. Undo to restore the previous positions.",
                false
            );
        });
    }

    /// <summary>
    /// Exports the working snapshot without silently saving it over the source project.
    /// </summary>
    private async void ExportPack()
    {
        await RunOperationAsync(async cancellationToken =>
        {
            if (project == null || !view.CommitFields())
                return;
            string filePath = ContentEditorFileDialog.SaveFile(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                project.Definition.ID + ".zip",
                new[] { ".zip" }
            );
            if (string.IsNullOrEmpty(filePath))
                return;
            view.RenderStatus(
                "Validating and exporting content pack. Press Escape to cancel.",
                false
            );
            IReadOnlyList<string> warnings = await ContentPackEditorExporter.WriteArchiveAsync(
                project,
                filePath,
                ReportValidationProgress,
                cancellationToken
            );
            view.RenderValidationResult("Content pack exported to " + filePath, warnings);
        });
    }

    /// <summary>
    /// Restores one project edit, retaining the selected record where possible.
    /// </summary>
    private void Undo()
    {
        RunOperation(() =>
        {
            if (view.UndoText())
                return;
            if (project != null && view.CommitFields() && project.Undo())
                workspace.RenderProject(project, assets);
        });
    }

    /// <summary>
    /// Reapplies one project edit, retaining the selected record where possible.
    /// </summary>
    private void Redo()
    {
        RunOperation(() =>
        {
            if (view.RedoText())
                return;
            if (project != null && view.CommitFields() && project.Redo())
                workspace.RenderProject(project, assets);
        });
    }

    /// <summary>
    /// Closes the project after saving or explicitly discarding pending edits.
    /// </summary>
    private void ClosePack()
    {
        ContinueAfterChanges(() =>
        {
            workspace.ClearProject();
            project = null;
            assets?.Dispose();
            assets = null;
            ContentBindings.Apply(view.gameObject, applicationAssets);
        });
    }

    /// <summary>
    /// Guards project replacement without treating elapsed time as consent to discard edits.
    /// </summary>
    /// <param name="next">The operation to run after resolving pending edits.</param>
    private void ContinueAfterChanges(Action next)
    {
        RunOperation(() =>
        {
            if (!view.CommitFields())
            {
                view.ConfirmDiscard(() => RunOperation(next));
                return;
            }
            if (project?.IsDirty == true)
                view.ConfirmUnsaved(
                    () =>
                        RunOperation(() =>
                        {
                            SaveCurrentProject();
                            next();
                        }),
                    () => RunOperation(next)
                );
            else
                next();
        });
    }

    /// <summary>
    /// Routes the File menu exit through the same native window-close guard.
    /// </summary>
    private void RequestQuit()
    {
        Application.Quit();
    }

    /// <summary>
    /// Prevents native window closure until pending edits have been handled.
    /// </summary>
    /// <returns>True when the application can close.</returns>
    private bool CanQuit()
    {
        if (quitApproved)
            return true;
        if (operationCancellation != null)
        {
            quitAfterOperation = true;
            operationCancellation.Cancel();
            return false;
        }
        if (view.ConfirmPendingArtwork(RequestQuit))
            return false;
        if (!view.CommitFields())
        {
            view.ConfirmDiscard(() =>
            {
                quitApproved = true;
                Application.Quit();
            });
            return false;
        }
        if (project?.IsDirty != true)
            return true;
        view.ConfirmUnsaved(
            () =>
                RunOperation(() =>
                {
                    SaveCurrentProject();
                    quitApproved = true;
                    Application.Quit();
                }),
            () =>
            {
                quitApproved = true;
                Application.Quit();
            }
        );
        return false;
    }

    /// <summary>
    /// Reports operation errors without losing the open working project.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    private void RunOperation(Action operation)
    {
        if (operationCancellation != null)
            return;
        try
        {
            operation();
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    /// <summary>
    /// Owns a cancellable validation/export operation and restores input before any deferred quit.
    /// </summary>
    /// <param name="operation">The operation receiving its cancellation token.</param>
    /// <returns>The completion of execution and workspace cleanup.</returns>
    private async Task RunOperationAsync(Func<CancellationToken, Task> operation)
    {
        if (operationCancellation != null)
            return;
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        operationCancellation = cancellation;
        view.SetBusy(true);
        try
        {
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (this != null)
                view.RenderStatus("Operation cancelled.", false);
        }
        catch (Exception exception)
        {
            if (this != null)
                ReportError(exception);
        }
        finally
        {
            operationCancellation = null;
            if (this != null)
            {
                view.SetBusy(false);
                if (quitAfterOperation)
                {
                    quitAfterOperation = false;
                    Application.Quit();
                }
            }
        }
    }

    /// <summary>
    /// Displays the active validation check without hiding the operation's cancellation shortcut.
    /// </summary>
    /// <param name="message">The current validation stage or media address.</param>
    private void ReportValidationProgress(string message)
    {
        if (this != null)
            view.RenderStatus(message + ". Press Escape to cancel.", false);
    }

    /// <summary>
    /// Reports expected authoring errors concisely and logs unexpected failures with their stack.
    /// </summary>
    /// <param name="exception">The operation failure.</param>
    /// <param name="message">An optional user-facing explanation of the failure.</param>
    private void ReportError(Exception exception, string message = null)
    {
        ContentPackEditorField field = ContentEditorPackValidator.GetValidationField(exception);
        if (field == null && exception.InnerException != null)
            GameLogger.Warning(exception.ToString());
        if (
            field == null
            && exception is not ArgumentException
            && exception is not IOException
            && exception is not XmlException
            && exception is not UnauthorizedAccessException
        )
            GameLogger.LogException(exception);
        if (message == null && field != null)
            view.RenderFieldError(
                exception.Message,
                field,
                (exception.InnerException?.Message ?? exception.Message).Split('\n')[0]
            );
        else
            view.RenderStatus(message ?? exception.Message, true);
    }
}
