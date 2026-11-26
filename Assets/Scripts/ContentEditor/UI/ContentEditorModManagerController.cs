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
    private readonly ContentEditorOperation operation = new(Application.Quit);
    private CancellationTokenSource iconRequest;
    private CancellationTokenSource searchRequest;
    private ThunderstorePackage[] catalog = Array.Empty<ThunderstorePackage>();
    private IReadOnlyList<InstalledModPackage> installed = Array.Empty<InstalledModPackage>();
    private IReadOnlyList<ContentPackDefinition> packs = Array.Empty<ContentPackDefinition>();
    private string catalogDefaultPack;
    private string selection;
    private bool installedTab;
    private int versionIndex;
    private bool disposed;
    internal bool IsBusy => operation.IsRunning;
    internal event Action<string, float, bool> OperationChanged;
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
    /// <param name="packageStore">The application's shared package inventory and catalogue.</param>
    internal ContentEditorModManagerController(
        ContentEditorModManagerView view,
        string contentRoot,
        Func<string[]> protectedIDs,
        Action<string, Action> confirm,
        Action contentChanged,
        ModPackageStore packageStore = null
    )
    {
        this.view = view;
        this.contentRoot = contentRoot;
        this.protectedIDs = protectedIDs;
        this.confirm = confirm;
        this.contentChanged = contentChanged;
        store = packageStore ?? new ModPackageStore(contentRoot, client);
        view.ActivePackRequested += SetActivePack;
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
    /// Opens a library or browsing page without requiring an authoring project.
    /// </summary>
    /// <param name="showInstalled">Whether to show the installed library rather than the catalog.</param>
    internal void Show(bool showInstalled = true)
    {
        installedTab = showInstalled;
        view.Show();
        try
        {
            if (!IsBusy)
            {
                store.RecoverInterruptedInstallations();
                ReadInstalled();
            }
            if (catalog.Length == 0)
                catalog = store.Catalog.Read();
            view.Filters.SetOptions(
                store.Catalog.ReadFilters(),
                catalog.SelectMany(package => package.categories ?? Array.Empty<string>())
            );
            RenderRows(true);
            RenderDetails();
            if (!IsBusy)
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
                string selectedRelease = SelectedVersion?.version_number;
                catalog = await store.Catalog.RefreshAsync(token);
                if (selectedRelease != null && SelectedListing != null)
                    versionIndex = Mathf.Max(
                        0,
                        Array.FindIndex(
                            SelectedListing.versions,
                            version => version.version_number == selectedRelease
                        )
                    );
                view.Filters.SetOptions(
                    store.Catalog.ReadFilters(),
                    catalog.SelectMany(package => package.categories ?? Array.Empty<string>())
                );
                ThunderstoreCommunityFilters filters = await store.Catalog.RefreshFiltersAsync(
                    token
                );
                view.Filters.SetOptions(
                    filters,
                    catalog.SelectMany(package => package.categories ?? Array.Empty<string>())
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
        packs = ContentPackLoader.DiscoverPacks(contentRoot);
        catalogDefaultPack = ContentDefinitionSerializer
            .Deserialize<ContentCatalogDefinition>(Path.Combine(contentRoot, "catalog.xml"))
            .ActivePackID;
        compatibilityIssues.Clear();
        foreach (InstalledModPackage package in installed)
            compatibilityIssues.Add(package.PackageID, store.GetCompatibilityIssue(package, packs));
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
            installedTab ? string.Empty : view.Search,
            view.Filters.Read(),
            installedTab ? ContentEditorModSort.Name : view.Sort
        );
        UserSettingsManager.TryReadContentSelection(out string activePack, out _, out _);
        activePack = string.IsNullOrWhiteSpace(activePack) ? catalogDefaultPack : activePack;
        HashSet<string> managedIDs = new(
            installed.SelectMany(item => item.Content).Select(item => item.ID)
        );
        if (installedTab)
            entries = entries
                .Concat(
                    packs
                        .Where(pack => !managedIDs.Contains(pack.ID))
                        .Select(pack => new ContentEditorModEntry(null, null, includedPack: pack))
                )
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        view.RenderActivePacks(packs, activePack);
        if (!entries.Any(entry => entry.ID == selection))
        {
            selection = entries.FirstOrDefault()?.ID;
            versionIndex = 0;
        }
        entries = entries
            .Select(entry => new ContentEditorModEntry(
                entry.Listing,
                entry.Installation,
                compatibilityIssues.TryGetValue(entry.ID, out string issue) ? issue : null,
                entry.IncludedPack,
                entry.IncludedPack?.ID == activePack
                    || entry.Installation?.Content.Any(content =>
                        content.IsPack && content.ID == activePack
                    ) == true
            ))
            .ToArray();
        view.RenderRows(
            entries,
            installedTab,
            installedTab ? entries.Count : catalog.Length,
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
        ContentPackDefinition included = packs.FirstOrDefault(pack => pack.ID == selection);
        if (listing == null && current == null && included != null)
        {
            UserSettingsManager.TryReadContentSelection(out string savedPack, out _, out _);
            bool isDefault =
                included.ID
                == (string.IsNullOrWhiteSpace(savedPack) ? catalogDefaultPack : savedPack);
            view.RenderDetails(
                new ContentEditorModEntry(
                    null,
                    null,
                    includedPack: included,
                    isActivePack: isDefault
                ),
                isDefault ? "Default content pack" : "Included with the game",
                string.Empty,
                "Included with Rebellion 2.",
                "Version: " + included.Version,
                new List<string>(),
                0,
                string.Empty,
                false,
                isDefault ? "Active content pack" : "Set as default",
                !isDefault,
                false,
                false
            );
            return;
        }
        string text =
            release?.description
            ?? current?.Description
            ?? "Select a package to see its description, available versions, and installation options.";
        List<string> metadata = new();
        string issue =
            current != null
            && compatibilityIssues.TryGetValue(current.PackageID, out string compatibilityIssue)
                ? compatibilityIssue
                : string.Empty;
        if (current != null)
        {
            metadata.Add("Installed release: " + current.Version);
            foreach (
                InstalledModContent content in current.Content.Where(content =>
                    content.Version != current.Version
                )
            )
                metadata.Add("Content version: " + content.Version);
        }
        if (release != null)
        {
            metadata.Add(
                "Download size: " + (release.file_size / 1048576d).ToString("0.0") + " MB"
            );
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
        activePack = string.IsNullOrWhiteSpace(activePack) ? catalogDefaultPack : activePack;
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
                : "Set as default"
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
            string.Join(" · ", metadata),
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
            ReadInstalled();
            InstalledModPackage package = installed.Single(item =>
                item.PackageID == listing.full_name
            );
            UserSettingsManager.TryReadContentSelection(
                out string activePack,
                out _,
                out string[] disabled
            );
            if (
                IsPackageEnabled(
                    package,
                    string.IsNullOrWhiteSpace(activePack) ? catalogDefaultPack : activePack,
                    disabled
                )
            )
                EnableRequiredAddOns(package);
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
        {
            view.SetStatus(message + " · " + Mathf.RoundToInt(value * 100) + "%");
            OperationChanged?.Invoke(message, value, true);
        }
    }

    /// <summary>
    /// Updates the existing game content-selection settings without opening a game runtime.
    /// </summary>
    private void Activate()
    {
        InstalledModPackage current = SelectedInstalled;
        ContentPackDefinition included = packs.FirstOrDefault(pack => pack.ID == selection);
        string packID =
            current?.Content.FirstOrDefault(content => content.IsPack)?.ID ?? included?.ID;
        if (packID != null)
        {
            SetActivePack(packID);
            return;
        }
        if (current == null)
            return;
        try
        {
            UserSettingsManager.TryReadContentSelection(
                out string activePack,
                out _,
                out string[] disabled
            );
            string[] mods = current.Content.Select(content => content.ID).ToArray();
            if (mods.Any(disabled.Contains))
                EnableRequiredAddOns(current);
            else
            {
                activePack = string.IsNullOrWhiteSpace(activePack)
                    ? catalogDefaultPack
                    : activePack;
                InstalledModPackage dependent = installed.FirstOrDefault(candidate =>
                    candidate.PackageID != current.PackageID
                    && IsPackageEnabled(candidate, activePack, disabled)
                    && store
                        .GetRequiredPackages(candidate)
                        .Any(required => required.PackageID == current.PackageID)
                );
                if (dependent != null)
                    throw new InvalidOperationException(
                        dependent.Name
                            + " requires this add-on. Disable that package or choose another default pack first."
                    );
                UserSettingsManager.UpdateContentSelection(null, null, mods, false);
            }
            RenderRows();
            RenderDetails();
            view.SetStatus("Add-on selection saved. The game will use it when content next loads.");
        }
        catch (Exception exception)
        {
            view.SetStatus(exception.Message);
        }
    }

    /// <summary>
    /// Saves an installed pack as the startup default through the existing game settings.
    /// </summary>
    /// <param name="id">The installed pack selected by the player.</param>
    private void SetActivePack(string id)
    {
        try
        {
            ContentPackDefinition pack = packs.Single(item => item.ID == id);
            ContentPackLoader.CreatePackResolver(contentRoot, id);
            UserSettingsManager.TryReadContentSelection(out _, out _, out string[] disabled);
            string[] mods = installed
                .Where(item => IsPackageEnabled(item, id, disabled))
                .SelectMany(store.GetRequiredPackages)
                .SelectMany(item => item.Content)
                .Where(content => !content.IsPack)
                .Select(content => content.ID)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            UserSettingsManager.UpdateContentSelection(id, pack.DefaultScenarioID, mods, true);
            RenderRows();
            RenderDetails();
            view.SetStatus("Default content pack: " + pack.DisplayName);
        }
        catch (Exception exception)
        {
            view.SetStatus(exception.Message);
            RenderRows();
        }
    }

    /// <summary>
    /// Enables a package's complete dependency chain in one settings update.
    /// </summary>
    /// <param name="package">The package whose add-ons are being enabled.</param>
    private void EnableRequiredAddOns(InstalledModPackage package)
    {
        string[] mods = store
            .GetRequiredPackages(package)
            .SelectMany(item => item.Content)
            .Where(content => !content.IsPack)
            .Select(content => content.ID)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        UserSettingsManager.UpdateContentSelection(null, null, mods, true);
    }

    /// <summary>
    /// Checks whether a package supplies the selected pack or any enabled compatible add-on.
    /// </summary>
    /// <param name="package">The installed package to inspect.</param>
    /// <param name="activePack">The selected pack identity.</param>
    /// <param name="disabled">The add-on identities excluded from loading.</param>
    /// <returns>Whether this package participates in the selected content.</returns>
    private static bool IsPackageEnabled(
        InstalledModPackage package,
        string activePack,
        string[] disabled
    ) =>
        package.Content.Any(content =>
            content.IsPack
                ? content.ID == activePack
                : content.BasePackID == activePack && !disabled.Contains(content.ID)
        );

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
        string operationResult = "No active downloads";
        await operation.RunAsync(
            action,
            value =>
            {
                view.SetBusy(value, changesFiles);
                if (value && changesFiles)
                    OperationChanged?.Invoke("Preparing content", 0, true);
            },
            exception =>
            {
                operationResult = "Content operation failed. Open Library for details.";
                view.SetStatus(exception.Message);
            },
            () =>
            {
                operationResult = "Content operation canceled";
                view.SetStatus("Operation canceled. Previously installed content was retained.");
            },
            () =>
            {
                if (changesFiles)
                    OperationChanged?.Invoke(operationResult, 1, false);
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
            },
            lifetime.Token
        );
    }

    /// <summary>
    /// Hides the page while application-owned file operations retain visible global progress.
    /// </summary>
    private void Close()
    {
        iconRequest?.Cancel();
        searchRequest?.Cancel();
        view.Hide();
    }

    /// <summary>
    /// Cancels an installation while retaining its visible progress and rollback feedback.
    /// </summary>
    internal void Cancel() => operation.Cancel();

    /// <summary>
    /// Defers quitting until a running transaction has canceled or committed.
    /// </summary>
    /// <returns>Whether the host may quit immediately.</returns>
    internal bool CanQuit() => operation.CanQuit();

    /// <summary>
    /// Disconnects the view and cancels all work owned by this manager.
    /// </summary>
    public void Dispose()
    {
        disposed = true;
        operation.Dispose();
        searchRequest?.Cancel();
        searchRequest?.Dispose();
        lifetime.Cancel();
        lifetime.Dispose();
        iconRequest?.Cancel();
        iconRequest?.Dispose();
        view.ActivePackRequested -= SetActivePack;
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
