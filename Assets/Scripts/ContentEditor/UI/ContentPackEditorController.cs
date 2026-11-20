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
    private readonly Dictionary<
        (System.Xml.Linq.XElement Unit, ContentEditorArtworkEffect Effect),
        (string References, string Layers)
    > artworkDrafts = new();
    private bool quitApproved;
    private CancellationTokenSource operationCancellation;
    private bool quitAfterOperation;
    private bool audioPreviewLoading;
    private int audioPreviewRequest;
    private string previewAudioAddress;
    private ContentEditorSettingsManager settingsManager;
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
        view.RestoreTravelArtworkRequested += RestoreTravelArtwork;
        view.ExportRequested += ExportPack;
        view.ValidateRequested += ValidatePack;
        view.QuickValidationRequested += QuickValidatePack;
        view.MapLayoutRequested += ArrangeMap;
        view.UndoRequested += Undo;
        view.RedoRequested += Redo;
        view.CloseRequested += ClosePack;
        view.ExitRequested += RequestQuit;
        view.PlayRequested += PlayGame;
        view.PreviewRequested += PreviewMedia;
        view.PreviewCancelled += InvalidateMediaPreview;
        Application.wantsToQuit += CanQuit;
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
    /// Releases all subscriptions before destroying project-owned media.
    /// </summary>
    private void OnDestroy()
    {
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
            view.RestoreTravelArtworkRequested -= RestoreTravelArtwork;
            view.ExportRequested -= ExportPack;
            view.ValidateRequested -= ValidatePack;
            view.QuickValidationRequested -= QuickValidatePack;
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
                GetAssetPickerDirectory(field),
                field.MediaExtensions
            );
            if (string.IsNullOrEmpty(source))
                return;
            if (ContentPackEditorStorage.CanReplaceAsset(project, field))
            {
                view.ConfirmMediaReplacement(
                    field.GetMediaAddress(field.Value),
                    () => RunOperation(() => ApplyMediaImport(field, source, true)),
                    () => RunOperation(() => ApplyMediaImport(field, source, false))
                );
                return;
            }
            ApplyMediaImport(field, source, false);
        });
    }

    /// <summary>
    /// Opens replacement imports beside the image currently resolved by the pack.
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
            OpenProject(project);
            return;
        }

        string address = ContentPackEditorStorage.ImportAsset(project, source, field);
        if (field.IsList)
            project.SetFieldValues(
                field,
                field.Values.Where(value => !string.IsNullOrWhiteSpace(value)).Append(address)
            );
        else
            project.SetField(field, address);
        workspace.RenderProject(project, assets);
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

            var outputs = targets
                .Distinct()
                .Select(field =>
                {
                    project.RequireOwnedRecord(field.Record);
                    if (field.Record.Element != source.Record.Element)
                        throw new ArgumentException("Artwork outputs must belong to one unit.");
                    if (
                        field == source
                        || field.MemberName == ContentEditorUnitArtworkLayout.BackgroundMember
                        || ContentEditorUnitArtworkLayout.IsStateSpecific(field.MemberName)
                        || !ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(
                            field,
                            out Vector2Int dimensions
                        )
                    )
                        throw new ArgumentException("Choose ordinary fixed-size artwork outputs.");
                    return new { Field = field, Dimensions = dimensions };
                })
                .ToArray();
            Vector2[] offsets = new Vector2[outputs.Length];
            ShowCuts();

            void ShowCuts()
            {
                var placements = outputs
                    .Select((output, index) => (output.Dimensions, offsets[index]))
                    .ToArray();
                var cuts = ContentEditorArtworkCutGenerator.GeneratePlacedPngCuts(
                    sourcePath,
                    placements,
                    fitEntireSource: source.MemberName == "DisplayImagePath",
                    preserveVisibleSubject: true
                );
                var images = outputs
                    .Select((output, index) => (output.Field, Png: cuts[placements[index]]))
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
                        () =>
                            RunOperation(() =>
                                ApplyGeneratedArtwork("Generate standard artwork cuts", images)
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
                            })
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
    private void PreviewArtworkEffect(
        ContentPackEditorField selectedField,
        ContentEditorArtworkEffect effect
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
            var key = (record.Element, effect);
            string references = string.Join("\n", outputs.Select(output => output.Field.Value));
            int initialFrame = Math.Max(
                0,
                Array.FindIndex(
                    outputs,
                    output => output.Field.MemberName == selectedField.MemberName
                )
            );
            string stored;
            if (outputs[initialFrame].Existing == null)
                stored = null;
            else if (
                artworkDrafts.TryGetValue(key, out var previous)
                && previous.References == references
            )
                stored = previous.Layers;
            else
            {
                var sidecars = outputs
                    .Select(output =>
                        ContentPackEditorStorage.ReadGeneratedLayers(project, output.Field)
                    )
                    .ToArray();
                stored = sidecars.All(data => data == sidecars[0]) ? sidecars[0] : null;
            }
            int defaultSeed = ContentEditorArtworkEffectGenerator.GetSeed(record.ID, effect);
            ContentEditorArtworkRecipe recipe =
                stored == null ? null : JsonUtility.FromJson<ContentEditorArtworkRecipe>(stored);
            if (
                recipe?.Layers == null
                || (
                    recipe.Version != ContentEditorArtworkRecipe.CurrentVersion
                    && recipe.Version != 5
                    && recipe.Version != 4
                    && recipe.Version != 3
                    && recipe.Version != 2
                    && !(recipe.Version == 1 && shipEffects)
                )
                || (recipe.HasStoredSources && recipe.ExistingSources.Length != outputs.Length)
            )
                recipe = null;
            if (outputs[initialFrame].Source == null && outputs[initialFrame].Existing == null)
                throw new InvalidOperationException(
                    "Choose a display image or a small image first."
                );
            var frames = new List<ContentEditorArtworkEffectGenerator.EditableFrame>();
            ContentEditorTravelPattern[] travelPatterns = null;
            Stack<string> undo = new();
            Stack<string> redo = new();
            int selected = -1;
            try
            {
                bool initialize = recipe == null;
                recipe ??= new ContentEditorArtworkRecipe
                {
                    Seed = defaultSeed,
                    UsesExisting = outputs[initialFrame].Existing != null,
                };
                RebuildFrames(recipe);
                if (initialize)
                    CreateDefaultLayers();
                selected = recipe.Layers.Count == 0 ? -1 : 0;
                Refresh();
            }
            catch
            {
                DisposeFrames();
                throw;
            }

            void DisposeFrames()
            {
                foreach (var frame in frames)
                    frame.Dispose();
                frames.Clear();
            }

            string ExistingSource(ContentEditorArtworkRecipe draft, int index)
            {
                if (!draft.UsesExisting)
                    return null;
                return draft.HasStoredSources
                    ? ResolveArtworkAddress(draft.ExistingSources[index])
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
                            exact = ContentEditorTravelRestoration.ResolveUnitPattern(
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
                if (
                    draft.TravelPatterns?.Length == outputs.Length
                    && draft.TravelSubjects?.Length == outputs.Length
                )
                {
                    return new ContentEditorTravelPattern(
                        ResolveArtworkAddress(draft.TravelPatterns[index]),
                        ResolveArtworkAddress(draft.TravelSubjects[index])
                    );
                }
                return (travelPatterns ??= ResolveTravelPatterns())[index];
            }

            void RebuildFrames(ContentEditorArtworkRecipe draft)
            {
                var replacement = new List<ContentEditorArtworkEffectGenerator.EditableFrame>();
                try
                {
                    for (int index = 0; index < outputs.Length; index++)
                    {
                        var output = outputs[index];
                        string existing = ExistingSource(draft, index);
                        bool completeTravel =
                            effect == ContentEditorArtworkEffect.Travel
                            && draft.Version >= 5
                            && !draft.UsesExisting;
                        ContentEditorTravelPattern pattern =
                            completeTravel
                            || (
                                effect == ContentEditorArtworkEffect.Travel
                                && draft.Version >= 5
                                && style == ContentEditorArtworkEffectStyle.FacilityTravel
                            )
                                ? GetTravelPattern(draft, index)
                                : default;
                        bool facilityCard =
                            style == ContentEditorArtworkEffectStyle.FacilityTravel
                            && output.ExpectedSize == new Vector2Int(302, 158);
                        int? clearance =
                            draft.Version < 4 || !facilityCard ? 0
                            : draft.FacilityClearance >= 0 ? draft.FacilityClearance
                            : null;
                        string reference =
                            clearance == null
                            && !(output.Field.Value?.Contains("/Generated/") ?? false)
                                ? output.Existing
                            : clearance == null && pattern.Subject == output.Source ? pattern.Image
                            : null;
                        try
                        {
                            replacement.Add(
                                ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                                    output.Source,
                                    completeTravel ? output.ExpectedSize : output.Size,
                                    style,
                                    draft.Seed,
                                    output.StretchSource,
                                    previewBackground,
                                    existing,
                                    draft.Variant,
                                    legacyStreaks: draft.Version == 2,
                                    facilityClearance: clearance,
                                    facilityReferencePath: reference,
                                    completeTravel: completeTravel,
                                    travelPattern: pattern,
                                    preserveRegisteredTravel: completeTravel && draft.Version >= 6
                                )
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
                                        ResolveArtworkAddress(
                                            view.GetUnitTravelBackgroundAddress()
                                        ),
                                        null
                                    );
                            if (
                                completeTravel
                                && travelPatterns != null
                                && draft.TravelPatterns?.Length != outputs.Length
                            )
                                travelPatterns[index] = fallback;
                            replacement.Add(
                                ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                                    display,
                                    output.ExpectedSize,
                                    style,
                                    draft.Seed,
                                    false,
                                    previewBackground,
                                    existing,
                                    draft.Variant,
                                    legacyStreaks: draft.Version == 2,
                                    facilityClearance: clearance,
                                    completeTravel: completeTravel,
                                    travelPattern: fallback
                                )
                            );
                        }
                    }
                }
                catch
                {
                    foreach (var frame in replacement)
                        frame.Dispose();
                    throw;
                }
                if (draft.Version >= 4 && draft.FacilityClearance < 0)
                    draft.FacilityClearance = replacement[0].FacilityClearance;
                DisposeFrames();
                frames.AddRange(replacement);
            }

            void CreateDefaultLayers()
            {
                int count = frames.Max(frame => frame.DefaultLayerCount);
                recipe.Layers = Enumerable
                    .Range(0, count)
                    .Select(index => new ContentEditorArtworkLayer { Source = index })
                    .ToList();
            }

            void RememberEdit()
            {
                undo.Push(JsonUtility.ToJson(recipe));
                redo.Clear();
            }

            void RestoreEdit(Stack<string> from, Stack<string> to)
            {
                if (from.Count == 0)
                    return;
                var restored = JsonUtility.FromJson<ContentEditorArtworkRecipe>(from.Peek());
                if (
                    recipe.Seed != restored.Seed
                    || recipe.Variant != restored.Variant
                    || recipe.Version != restored.Version
                    || recipe.FacilityClearance != restored.FacilityClearance
                    || recipe.UsesExisting != restored.UsesExisting
                )
                    RebuildFrames(restored);
                to.Push(JsonUtility.ToJson(recipe));
                from.Pop();
                recipe = restored;
                selected = Mathf.Min(selected < 0 ? 0 : selected, recipe.Layers.Count - 1);
                Refresh();
            }

            void MoveLayer(int index, Vector2 offset)
            {
                if (
                    index < 0
                    || index >= recipe.Layers.Count
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

            void RemoveLayer()
            {
                if (selected < 0)
                    return;
                RememberEdit();
                recipe.Layers.RemoveAt(selected);
                selected = Mathf.Min(selected, recipe.Layers.Count - 1);
                Refresh();
            }

            void AddLayer()
            {
                RememberEdit();
                var layer = new ContentEditorArtworkLayer
                {
                    Source = -1,
                    Seed = unchecked(recipe.Seed + ++recipe.NextAddition * 7919),
                    MeasuredStreaks = true,
                    VisibleStreakGroup = true,
                };
                int facility = frames[0].FacilityLayerIndex;
                int foreground =
                    facility < 0 ? -1 : recipe.Layers.FindIndex(item => item.Source == facility);
                selected = foreground < 0 ? recipe.Layers.Count : foreground;
                recipe.Layers.Insert(selected, layer);
                Refresh();
            }

            void DuplicateLayer()
            {
                if (selected < 0 || selected >= recipe.Layers.Count)
                    return;
                RememberEdit();
                ContentEditorArtworkLayer copy = recipe.Layers[selected];
                recipe.Layers.Insert(++selected, copy);
                Refresh();
            }

            void Regenerate()
            {
                if (outputs.All(output => output.Source == null))
                    throw new InvalidOperationException(
                        "Choose a display image or a small image first."
                    );
                var replacement = new ContentEditorArtworkRecipe
                {
                    Seed = unchecked(recipe.Seed + 104729),
                    Variant =
                        recipe.UsesExisting
                        || style == ContentEditorArtworkEffectStyle.FacilityTravel
                            ? 0
                            : (recipe.Variant + 1) % 3,
                };
                RebuildFrames(replacement);
                RememberEdit();
                recipe = replacement;
                CreateDefaultLayers();
                selected = recipe.Layers.Count == 0 ? -1 : 0;
                Refresh();
            }

            void Save()
            {
                if (recipe.UsesExisting && !recipe.HasStoredSources)
                    recipe.ExistingSources = outputs
                        .Select(output =>
                            output.Existing == null
                                ? null
                                : ContentPackEditorStorage.StoreArtworkLayerSource(
                                    project,
                                    output.Existing
                                )
                        )
                        .ToArray();
                if (
                    effect == ContentEditorArtworkEffect.Travel
                    && recipe.Version >= 5
                    && !recipe.UsesExisting
                    && travelPatterns != null
                    && recipe.TravelPatterns?.Length != outputs.Length
                )
                {
                    recipe.TravelPatterns = travelPatterns
                        .Select(pattern => StoreTravelPatternSource(pattern.Image))
                        .ToArray();
                    recipe.TravelSubjects = travelPatterns
                        .Select(pattern => StoreTravelPatternSource(pattern.Subject))
                        .ToArray();
                }
                string serialized = JsonUtility.ToJson(recipe);
                var images = outputs
                    .Select(
                        (output, index) => (output.Field, Png: frames[index].Bake(recipe.Layers))
                    )
                    .ToArray();
                ApplyGeneratedArtwork(
                    $"Save {effect.ToString().ToLowerInvariant()} artwork",
                    images,
                    serialized
                );
                references = string.Join("\n", outputs.Select(output => output.Field.Value));
                view.CloseArtworkDraft();
            }

            void Refresh()
            {
                var previews = frames
                    .Select(
                        (frame, frameIndex) =>
                        {
                            var layers = new List<ArtworkEffectLayerPreview>();
                            for (int index = 0; index < recipe.Layers.Count; index++)
                            {
                                var definition = recipe.Layers[index];
                                var piece = frame.GetLayer(definition);
                                if (piece.Texture == null)
                                    continue;
                                Vector2 size = frame.Size;
                                Rect bounds = new(
                                    (Vector2)piece.Bounds.min / size + definition.Offset,
                                    (Vector2)piece.Bounds.size / size
                                );
                                string name = style switch
                                {
                                    ContentEditorArtworkEffectStyle.ShipTravel =>
                                        $"Wake {index + 1}",
                                    ContentEditorArtworkEffectStyle.Damage =>
                                        $"Explosion {index + 1}",
                                    ContentEditorArtworkEffectStyle.FacilityTravel
                                        when definition.Source == frame.FacilityLayerIndex
                                            && definition.Source >= 0 => "Facility",
                                    ContentEditorArtworkEffectStyle.FacilityTravel
                                        when recipe.Version >= 6
                                            && !recipe.UsesExisting
                                            && definition.Source == 0 => "Travel field",
                                    ContentEditorArtworkEffectStyle.PersonnelTravel
                                        when recipe.UsesExisting && definition.Source >= 3 =>
                                        $"Existing accent {definition.Source - 2}",
                                    _ => $"Streaks {index + 1}",
                                };
                                layers.Add(
                                    new ArtworkEffectLayerPreview(
                                        index,
                                        name,
                                        piece.Texture,
                                        bounds
                                    )
                                );
                            }
                            return new ArtworkEffectPreviewFrame(
                                outputs[frameIndex].Caption,
                                frame.BaseImage,
                                frame.SubjectOverlay,
                                layers
                            );
                        }
                    )
                    .ToArray();
                string variation = recipe.UsesExisting
                    ? "Existing artwork"
                    : style switch
                    {
                        ContentEditorArtworkEffectStyle.ShipTravel => recipe.Variant switch
                        {
                            1 => "Long wake",
                            2 => "Wide wake",
                            _ => "Compact wake",
                        },
                        ContentEditorArtworkEffectStyle.Damage => "Generated damage",
                        _ when recipe.Version >= 5 => recipe.Variant switch
                        {
                            1 => "Shifted streak field",
                            2 => "Offset streak field",
                            _ => "Travel streaks",
                        },
                        _ => recipe.Variant switch
                        {
                            1 => "Long streaks",
                            2 => "Dense streaks",
                            _ => "Blue streaks",
                        },
                    };
                view.ShowGeneratedArtwork(
                    $"{effect} artwork for {record.Name} / {variation}",
                    previews,
                    initialFrame,
                    selected,
                    index => selected = index,
                    (index, offset) => RunOperation(() => MoveLayer(index, offset)),
                    () => RunOperation(AddLayer),
                    () => RunOperation(DuplicateLayer),
                    () => RunOperation(RemoveLayer),
                    () => RunOperation(Regenerate),
                    style switch
                    {
                        ContentEditorArtworkEffectStyle.ShipTravel => "Add wake",
                        ContentEditorArtworkEffectStyle.Damage => "Add explosion",
                        _ => "Add streaks",
                    },
                    style == ContentEditorArtworkEffectStyle.FacilityTravel
                        ? "Generate full card"
                        : "Try variant",
                    () => RunOperation(Save),
                    () => RunOperation(() => RestoreEdit(undo, redo)),
                    () => RunOperation(() => RestoreEdit(redo, undo)),
                    () =>
                    {
                        artworkDrafts[key] = (references, JsonUtility.ToJson(recipe));
                        DisposeFrames();
                    }
                );
            }
        });
    }

    /// <summary>
    /// Writes approved artwork before replacing its unit references in one undo step.
    /// </summary>
    /// <param name="description">The description of the operation in the undo history.</param>
    /// <param name="images">The previewed images and their destination fields.</param>
    /// <param name="layers">The optional editor layer data for these outputs.</param>
    private void ApplyGeneratedArtwork(
        string description,
        IReadOnlyList<(ContentPackEditorField Field, byte[] Png)> images,
        string layers = null
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
                    layerRecipe: layers
                );
                return (image.Field, Value: value);
            })
            .ToArray();
        using (project.BeginEdit(description))
        {
            foreach (var reference in references)
                project.SetField(reference.Field, reference.Value);
        }
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
        if (project != value)
            artworkDrafts.Clear();
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
    /// Restores an installed reference pack's missing transit artwork in one operation.
    /// </summary>
    private void RestoreTravelArtwork()
    {
        RunOperation(() =>
        {
            if (project == null || !view.CommitFields())
                return;
            bool clean = !project.IsDirty;
            var restored = ContentEditorTravelRestoration.Restore(project);
            if (restored.Files == 0 && restored.References == 0)
            {
                view.RenderStatus("Travel artwork is already present.", false);
                return;
            }
            if (clean && restored.References > 0)
                ContentPackEditorStorage.Save(project);
            OpenProject(project);
            string saveMessage =
                !clean && restored.References > 0
                    ? " Save pack to write the updated references."
                    : string.Empty;
            view.RenderStatus(
                $"Restored {restored.Files} travel images and {restored.References} assignments."
                    + saveMessage,
                false
            );
        });
    }

    /// <summary>
    /// Checks the current document fields and references without saving or loading gameplay.
    /// </summary>
    private void QuickValidatePack()
    {
        RunOperation(() =>
        {
            if (project == null || !view.CommitFields())
                return;
            var issues = ContentEditorPackValidator.GetFieldErrors(project);
            if (issues.Count == 0)
                view.RenderStatus(
                    "Quick validation passed. Pack fields and references are valid.",
                    false
                );
            else
                view.RenderValidationErrors(issues);
        });
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
