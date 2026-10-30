using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        view.SaveRequested += SavePack;
        view.ExportRequested += ExportPack;
        view.ValidateRequested += ValidatePack;
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
                System.IO.Path.Combine(UserDataPaths.Root, "content-editor-settings.json"),
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
            view.SaveRequested -= SavePack;
            view.ExportRequested -= ExportPack;
            view.ValidateRequested -= ValidatePack;
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
                    throw new System.IO.FileNotFoundException(
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
                    "Asset file: "
                        + assets.ResolveFile(address, System.IO.Path.GetExtension(address)),
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
                project.PackRootPath,
                field.MediaExtensions
            );
            if (string.IsNullOrEmpty(source))
                return;
            string address = ContentPackEditorStorage.ImportAsset(project, source, field);
            if (field.IsList)
                project.SetFieldValues(
                    field,
                    field.Values.Where(value => !string.IsNullOrWhiteSpace(value)).Append(address)
                );
            else
                project.SetField(field, address);
            workspace.RenderProject(project, assets);
        });
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
        if (!view.CommitFields())
            throw new ArgumentException("Fix the highlighted fields before saving.");
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
            SaveCurrentProject();
            view.RenderStatus("Validating content pack. Press Escape to cancel.", false);
            await ContentEditorPackValidator.ValidateAsync(
                project,
                ReportValidationProgress,
                cancellationToken
            );
            view.RenderStatus("Content pack validated successfully.", false);
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
            await ContentPackEditorExporter.WriteArchiveAsync(
                project,
                filePath,
                ReportValidationProgress,
                cancellationToken
            );
            view.RenderStatus("Content pack exported to " + filePath, false);
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
        if (exception.InnerException != null)
            Debug.LogWarning(exception.ToString());
        if (
            exception is not ArgumentException
            && exception is not System.IO.IOException
            && exception is not System.Xml.XmlException
            && exception is not UnauthorizedAccessException
        )
            Debug.LogException(exception);
        view.RenderStatus(message ?? exception.Message, true);
    }
}
