using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Coordinates community downloads and installed content independently of project editing.
/// </summary>
internal sealed class ContentEditorModManagerController : IDisposable
{
    private readonly ContentEditorModManagerView view;
    private readonly ThunderstoreClient client = new();
    private readonly ModPackageStore store;
    private readonly string contentRoot;
    private readonly Func<string[]> protectedIDs;
    private readonly Action<string, Action> confirm;
    private readonly Action contentChanged;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<string, string> compatibilityIssues = new(StringComparer.Ordinal);
    private CancellationTokenSource operation;
    private CancellationTokenSource iconRequest;
    private CancellationTokenSource searchRequest;
    private ThunderstorePackage[] catalog = Array.Empty<ThunderstorePackage>();
    private IReadOnlyList<InstalledModPackage> installed = Array.Empty<InstalledModPackage>();
    private string selection;
    private bool installedTab;
    private int versionIndex;
    private bool quitAfterOperation;
    private bool disposed;
    internal bool IsBusy => operation != null;
    private ThunderstorePackage SelectedListing =>
        catalog.FirstOrDefault(package => package.full_name == selection);
    private InstalledModPackage SelectedInstalled =>
        installed.FirstOrDefault(package => package.PackageID == selection);
    private ThunderstorePackageVersion SelectedVersion =>
        SelectedListing?.versions.ElementAtOrDefault(versionIndex);

    /// <summary>
    /// Connects the manager to its content root and project ownership guard.
    /// </summary>
    /// <param name="view">The authored manager screen.</param>
    /// <param name="contentRoot">The installed content root shared with the game.</param>
    /// <param name="protectedIDs">Native content currently used by the editor.</param>
    /// <param name="confirm">The host's existing confirmation dialog.</param>
    /// <param name="contentChanged">Refreshes installed inheritance sources after changes.</param>
    internal ContentEditorModManagerController(
        ContentEditorModManagerView view,
        string contentRoot,
        Func<string[]> protectedIDs,
        Action<string, Action> confirm,
        Action contentChanged
    )
    {
        this.view = view;
        this.contentRoot = contentRoot;
        this.protectedIDs = protectedIDs;
        this.confirm = confirm;
        this.contentChanged = contentChanged;
        store = new ModPackageStore(contentRoot, client);
        view.TabRequested += SelectTab;
        view.RefreshRequested += Refresh;
        view.ImportRequested += Import;
        view.CloseRequested += Close;
        view.CancelRequested += Cancel;
        view.InstallRequested += Install;
        view.QuickInstallRequested += InstallLatest;
        view.ActivateRequested += Activate;
        view.UninstallRequested += Uninstall;
        view.ListingRequested += OpenListing;
        view.FolderRequested += OpenFolder;
        view.SearchChanged += Search;
        view.SelectionChanged += Select;
        view.VersionChanged += SelectVersion;
        view.CriteriaChanged += ChangeCriteria;
        view.IconsRequested += LoadIcons;
    }

    /// <summary>
    /// Opens immediately and refreshes the community catalog without requiring a project.
    /// </summary>
    internal void Show()
    {
        view.Show();
        try
        {
            store.RecoverInterruptedInstallations();
            ReadInstalled();
            if (catalog.Length == 0 && File.Exists(store.CatalogPath))
                catalog = ThunderstoreClient.ParseCatalog(File.ReadAllText(store.CatalogPath));
            view.SetCategories(
                catalog.SelectMany(package => package.categories ?? Array.Empty<string>())
            );
            RenderRows(true);
            RenderDetails();
            Refresh();
        }
        catch (Exception exception)
        {
            view.SetStatus(exception.Message);
        }
    }

    /// <summary>
    /// Refreshes the public catalog while retaining usable offline data after a failure.
    /// </summary>
    private void Refresh() =>
        Run(
            async token =>
            {
                view.SetStatus("Checking Thunderstore…");
                var result = await client.GetCatalogAsync(token);
                string selectedRelease = SelectedVersion?.version_number;
                catalog = result.Packages;
                store.SaveCatalog(result.Json);
                view.SetCategories(
                    catalog.SelectMany(package => package.categories ?? Array.Empty<string>())
                );
                if (selectedRelease != null && SelectedListing != null)
                    versionIndex = Mathf.Max(
                        0,
                        Array.FindIndex(
                            SelectedListing.versions,
                            version => version.version_number == selectedRelease
                        )
                    );
                view.SetStatus(string.Empty);
            },
            false
        );

    /// <summary>
    /// Assesses the installed inventory once after refreshes and file operations, keeping search free of disk reads.
    /// </summary>
    private void ReadInstalled()
    {
        installed = store.GetInstalled();
        compatibilityIssues.Clear();
        foreach (InstalledModPackage package in installed)
            compatibilityIssues.Add(package.PackageID, store.GetCompatibilityIssue(package));
    }

    /// <summary>
    /// Switches between community listings and installed packages.
    /// </summary>
    /// <param name="value">Whether to show installed content.</param>
    private void SelectTab(bool value)
    {
        installedTab = value;
        RenderRows(true);
        RenderDetails();
    }

    /// <summary>
    /// Filters package names, authors, and descriptions.
    /// </summary>
    /// <param name="value">The current search text.</param>
    private async void Search(string value)
    {
        searchRequest?.Cancel();
        searchRequest?.Dispose();
        searchRequest = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        CancellationToken token = searchRequest.Token;
        try
        {
            await Task.Delay(150, token);
            if (!disposed && !token.IsCancellationRequested && view.Search == value)
            {
                RenderRows(true);
                RenderDetails();
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Applies filter and ordering choices without delaying deliberate selector changes.
    /// </summary>
    private void ChangeCriteria()
    {
        searchRequest?.Cancel();
        RenderRows(true);
        RenderDetails();
    }

    /// <summary>
    /// Selects one package and its latest release.
    /// </summary>
    /// <param name="id">The stable distribution identity.</param>
    private void Select(string id)
    {
        if (selection != id)
        {
            selection = id;
            versionIndex = 0;
            RenderRows();
        }
        RenderDetails();
    }

    /// <summary>
    /// Selects an exact release for install, update, or rollback.
    /// </summary>
    /// <param name="index">The selected distribution version index.</param>
    private void SelectVersion(int index)
    {
        versionIndex = index;
        RenderDetails();
    }

    /// <summary>
    /// Queries the catalog while preserving stable selection and ordinary scroll position.
    /// </summary>
    /// <param name="resetScroll">Whether changed criteria require showing the first result.</param>
    private void RenderRows(bool resetScroll = false)
    {
        IReadOnlyList<ContentEditorModEntry> entries = ContentEditorModCatalog.Find(
            catalog,
            installed,
            installedTab,
            view.Search,
            view.Category,
            view.Filter,
            view.Sort
        );
        if (!entries.Any(entry => entry.ID == selection))
        {
            selection = entries.FirstOrDefault()?.ID;
            versionIndex = 0;
        }
        entries = entries
            .Select(entry =>
                compatibilityIssues.TryGetValue(entry.ID, out string issue)
                    ? new ContentEditorModEntry(entry.Listing, entry.Installation, issue)
                    : entry
            )
            .ToArray();
        view.RenderRows(
            entries,
            installedTab,
            installedTab ? installed.Count : catalog.Length,
            resetScroll
        );
    }

    /// <summary>
    /// Displays native pack versions separately from distribution release versions.
    /// </summary>
    private void RenderDetails()
    {
        ThunderstorePackage listing = SelectedListing;
        if (listing != null)
            versionIndex = Mathf.Clamp(versionIndex, 0, listing.versions.Length - 1);
        ThunderstorePackageVersion release = SelectedVersion;
        InstalledModPackage current = SelectedInstalled;
        string text =
            release?.description
            ?? current?.Description
            ?? "Select a package to see its description, available versions, and installation options.";
        string issue =
            current != null
            && compatibilityIssues.TryGetValue(current.PackageID, out string compatibilityIssue)
                ? compatibilityIssue
                : string.Empty;
        if (current != null)
        {
            text += "\n\nInstalled release: " + current.Version;
            foreach (
                InstalledModContent content in current.Content.Where(content =>
                    content.Version != current.Version
                )
            )
                text += "\nContent version: " + content.Version;
        }
        if (release != null)
        {
            text += "\n\nDownload size: " + (release.file_size / 1048576d).ToString("0.0") + " MB";
            if (release.dependencies.Length > 0)
                text += "\n\nRequired packages:\n" + string.Join("\n", release.dependencies);
            if (listing.is_deprecated)
                text += "\n\nThe author has marked this package as deprecated.";
        }
        UserSettingsManager.TryReadContentSelection(
            out string activePack,
            out _,
            out string[] disabled
        );
        bool pack = current?.Content.Any(content => content.IsPack) == true;
        bool enabled =
            current
                ?.Content.Where(content => !content.IsPack)
                .All(content => !disabled.Contains(content.ID)) == true;
        bool selected =
            pack && current.Content.Any(content => content.IsPack && content.ID == activePack);
        if (pack)
            text += selected
                ? "\n\nUsed by default when starting the game."
                : "\n\nSet as active to use this pack by default.";
        string activateLabel = pack
            ? selected
                ? "Active content pack"
                : "Set as active pack"
            : enabled
                ? "Disable add-on"
                : "Enable add-on";
        bool inUse = current?.Content.Any(content => protectedIDs().Contains(content.ID)) == true;
        if (inUse)
            text +=
                "\n\nUsed by the open project. Close the project before replacing or removing these files.";
        string stateText =
            current == null
                ? listing?.is_deprecated == true
                    ? "Deprecated"
                    : "Available"
                : !string.IsNullOrEmpty(issue)
                    ? "Needs attention"
                    : pack
                        ? selected
                            ? "Active content pack"
                            : "Installed"
                        : enabled
                            ? "Enabled"
                            : "Disabled";
        view.RenderDetails(
            listing == null && current == null ? null : new ContentEditorModEntry(listing, current),
            stateText,
            issue,
            text,
            listing?.versions.Select(item => item.version_number).ToList() ?? new List<string>(),
            versionIndex,
            current == null ? "Install"
                : current.Version == release?.version_number ? "Reinstall"
                : "Install version " + release?.version_number,
            release != null && !inUse,
            activateLabel,
            current != null && string.IsNullOrEmpty(issue) && !selected,
            current != null && !inUse,
            !string.IsNullOrEmpty(listing?.package_url ?? current?.Website)
        );
    }

    /// <summary>
    /// Loads uncached artwork for visible cards and the detail panel, canceling obsolete requests.
    /// </summary>
    /// <param name="urls">The currently visible public icon URLs.</param>
    private async void LoadIcons(string[] urls)
    {
        iconRequest?.Cancel();
        iconRequest?.Dispose();
        iconRequest = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        CancellationToken token = iconRequest.Token;
        string[] missing = urls.Where(url => !view.HasIcon(url)).ToArray();
        for (int index = 0; index < missing.Length && !token.IsCancellationRequested; index += 4)
            await Task.WhenAll(
                missing.Skip(index).Take(4).Select(url => LoadIconAsync(url, token))
            );
    }

    /// <summary>
    /// Retrieves one card image without allowing canceled or disposed views to receive it.
    /// </summary>
    /// <param name="url">The exact public icon URL.</param>
    /// <param name="token">The visible-image request lifetime.</param>
    /// <returns>The completion of this artwork request.</returns>
    private async Task LoadIconAsync(string url, CancellationToken token)
    {
        try
        {
            byte[] bytes = await client.GetIconAsync(url, token);
            if (!disposed && !token.IsCancellationRequested)
                view.SetIcon(url, bytes);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogWarning("The mod listing icon could not be loaded: " + exception.Message);
        }
    }

    /// <summary>
    /// Installs the exact selected release and its dependencies.
    /// </summary>
    private void Install()
    {
        ThunderstorePackage listing = SelectedListing;
        ThunderstorePackageVersion version = SelectedVersion;
        if (listing == null || version == null)
            return;
        Run(async token =>
        {
            await store.InstallAsync(listing, version, catalog, protectedIDs(), Progress, token);
            contentChanged();
            view.SetStatus(listing.name + " " + version.version_number + " installed.");
        });
    }

    /// <summary>
    /// Installs the latest online version from a browse result without another confirmation step.
    /// </summary>
    /// <param name="id">The chosen package identity.</param>
    private void InstallLatest(string id)
    {
        if (IsBusy || disposed)
            return;
        selection = id;
        versionIndex = 0;
        RenderDetails();
        Install();
    }

    /// <summary>
    /// Imports a ZIP selected through the toolkit's existing native dialog.
    /// </summary>
    private void Import()
    {
        string path;
        try
        {
            path = ContentEditorFileDialog.OpenFile(
                ContentFileResolver.ResolveModsRootPath(contentRoot),
                new[] { ".zip" }
            );
        }
        catch (Exception exception)
        {
            view.SetStatus(exception.Message);
            return;
        }
        if (string.IsNullOrEmpty(path))
            return;
        Run(async token =>
        {
            await store.InstallLocalAsync(path, protectedIDs(), Progress, token);
            installedTab = true;
            contentChanged();
            view.SetStatus("Mod installed from file.");
        });
    }

    /// <summary>
    /// Reports download and staging progress without blocking rendering.
    /// </summary>
    /// <param name="message">The operation stage.</param>
    /// <param name="value">The completed download fraction.</param>
    private void Progress(string message, float value)
    {
        if (!disposed)
            view.SetStatus(message + " · " + Mathf.RoundToInt(value * 100) + "%");
    }

    /// <summary>
    /// Updates the existing game content-selection settings without opening a game runtime.
    /// </summary>
    private void Activate()
    {
        try
        {
            InstalledModPackage current = SelectedInstalled;
            if (current == null)
                return;
            UserSettingsManager.TryReadContentSelection(out _, out _, out string[] disabled);
            string[] mods = current
                .Content.Where(content => !content.IsPack)
                .Select(content => content.ID)
                .ToArray();
            bool enable = mods.Any(disabled.Contains);
            InstalledModContent pack = current.Content.FirstOrDefault(content => content.IsPack);
            ContentPackDefinition definition =
                pack == null
                    ? null
                    : ContentPackLoader
                        .DiscoverPacks(contentRoot)
                        .FirstOrDefault(item => item.ID == pack.ID);
            UserSettingsManager.UpdateContentSelection(
                pack?.ID,
                definition?.DefaultScenarioID,
                mods,
                pack != null || enable
            );
            RenderDetails();
            view.SetStatus(
                pack != null
                    ? "Active content pack saved. This is the default for the next game startup."
                    : "Add-on selection saved. The game will use it when content next loads."
            );
        }
        catch (Exception exception)
        {
            view.SetStatus(exception.Message);
        }
    }

    /// <summary>
    /// Confirms removal while keeping the exact selected package stable across the prompt.
    /// </summary>
    private void Uninstall()
    {
        InstalledModPackage current = SelectedInstalled;
        if (current == null)
            return;
        confirm(
            "Uninstall "
                + current.Name
                + "?\n\nIts files will be retained in the mod manager's recovery folder. Existing saves may require this content.",
            () =>
                Run(token =>
                {
                    token.ThrowIfCancellationRequested();
                    store.Uninstall(current, protectedIDs());
                    UserSettingsManager.TryReadContentSelection(
                        out string activePack,
                        out _,
                        out _
                    );
                    if (current.Content.Any(content => content.IsPack && content.ID == activePack))
                        UserSettingsManager.UpdateContentSelection(
                            string.Empty,
                            string.Empty,
                            Array.Empty<string>(),
                            true
                        );
                    contentChanged();
                    view.SetStatus(current.Name + " uninstalled.");
                    return Task.CompletedTask;
                })
        );
    }

    /// <summary>
    /// Opens the selected package's public listing through an HTTPS-only browser link.
    /// </summary>
    private void OpenListing()
    {
        string url = SelectedListing?.package_url ?? SelectedInstalled?.Website;
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.Scheme == Uri.UriSchemeHttps)
            Application.OpenURL(uri.AbsoluteUri);
    }

    /// <summary>
    /// Reveals installed content using the toolkit's existing folder command.
    /// </summary>
    private void OpenFolder()
    {
        InstalledModContent content = SelectedInstalled?.Content.FirstOrDefault();
        if (content != null)
            Application.OpenURL(
                new Uri(
                    Path.Combine(
                        ContentFileResolver.ResolveModsRootPath(contentRoot),
                        content.Folder
                    )
                ).AbsoluteUri
            );
    }

    /// <summary>
    /// Runs one cancellable operation and keeps errors in their owning screen.
    /// </summary>
    /// <param name="action">The download or filesystem operation.</param>
    /// <param name="changesFiles">Whether package files are being changed and browsing must pause.</param>
    private async void Run(Func<CancellationToken, Task> action, bool changesFiles = true)
    {
        if (IsBusy || disposed)
            return;
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        view.SetBusy(true, changesFiles);
        try
        {
            await action(operation.Token);
        }
        catch (OperationCanceledException)
        {
            if (!disposed)
                view.SetStatus("Operation canceled. Previously installed content was retained.");
        }
        catch (Exception exception)
        {
            if (!disposed)
                view.SetStatus(exception.Message);
        }
        finally
        {
            operation.Dispose();
            operation = null;
            if (!disposed)
            {
                view.SetBusy(false);
                try
                {
                    ReadInstalled();
                    RenderRows();
                    RenderDetails();
                }
                catch (Exception exception)
                {
                    view.SetStatus(exception.Message);
                }
                if (quitAfterOperation)
                    Application.Quit();
            }
        }
    }

    /// <summary>
    /// Cancels work before hiding the screen so no download continues invisibly.
    /// </summary>
    private void Close()
    {
        operation?.Cancel();
        iconRequest?.Cancel();
        searchRequest?.Cancel();
        view.Hide();
    }

    /// <summary>
    /// Cancels an installation while retaining its visible progress and rollback feedback.
    /// </summary>
    private void Cancel() => operation?.Cancel();

    /// <summary>
    /// Defers quitting until a running transaction has canceled or committed.
    /// </summary>
    /// <returns>Whether the host may quit immediately.</returns>
    internal bool CanQuit()
    {
        if (!IsBusy)
            return true;
        quitAfterOperation = true;
        operation.Cancel();
        return false;
    }

    /// <summary>
    /// Disconnects the view and cancels all work owned by this manager.
    /// </summary>
    public void Dispose()
    {
        disposed = true;
        searchRequest?.Cancel();
        searchRequest?.Dispose();
        lifetime.Cancel();
        lifetime.Dispose();
        iconRequest?.Cancel();
        iconRequest?.Dispose();
        view.TabRequested -= SelectTab;
        view.RefreshRequested -= Refresh;
        view.ImportRequested -= Import;
        view.CloseRequested -= Close;
        view.CancelRequested -= Cancel;
        view.InstallRequested -= Install;
        view.QuickInstallRequested -= InstallLatest;
        view.ActivateRequested -= Activate;
        view.UninstallRequested -= Uninstall;
        view.ListingRequested -= OpenListing;
        view.FolderRequested -= OpenFolder;
        view.SearchChanged -= Search;
        view.SelectionChanged -= Select;
        view.VersionChanged -= SelectVersion;
        view.CriteriaChanged -= ChangeCriteria;
        view.IconsRequested -= LoadIcons;
    }
}
