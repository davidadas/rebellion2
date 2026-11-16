using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Content.Modio;
using Rebellion.Util.Logging;

/// <summary>
/// Owns library discovery, selection, catalog requests and installations independently of Unity views.
/// </summary>
public sealed class ContentLibrarySession : IDisposable
{
    private readonly ContentLibrary library;
    private readonly ModioConfiguration configuration;
    private readonly ModioCatalogClient client;
    private readonly ModioInstaller installer;
    private readonly Action changed;
    private readonly Action contentChanged;
    private readonly Func<bool> canReplaceContent;
    private readonly Dictionary<long, ModioMod> remote = new Dictionary<long, ModioMod>();
    private IReadOnlyList<ContentLibraryItem> installed = Array.Empty<ContentLibraryItem>();
    private ModioCatalogPage catalog = new ModioCatalogPage();
    private CancellationTokenSource operation;
    private ContentLibraryPage page;
    private string search = string.Empty;
    private string selected;
    private string status = string.Empty;
    private bool disposed;

    public bool IsBusy => operation != null;

    /// <summary>
    /// Connects a content installation to its presentation and application lifetime.
    /// </summary>
    /// <param name="contentRoot">The installed Content directory.</param>
    /// <param name="configuration">The optional public catalog credentials.</param>
    /// <param name="changed">Refreshes the library presentation.</param>
    /// <param name="contentChanged">Refreshes application-owned content discovery.</param>
    /// <param name="canReplaceContent">Whether no editing project holds the installed files.</param>
    /// <param name="client">An optional catalog transport.</param>
    public ContentLibrarySession(
        string contentRoot,
        ModioConfiguration configuration,
        Action changed,
        Action contentChanged,
        Func<bool> canReplaceContent,
        ModioCatalogClient client = null
    )
    {
        library = new ContentLibrary(contentRoot);
        this.configuration =
            configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
        this.contentChanged =
            contentChanged ?? throw new ArgumentNullException(nameof(contentChanged));
        this.canReplaceContent =
            canReplaceContent ?? throw new ArgumentNullException(nameof(canReplaceContent));
        if (configuration.IsConfigured)
        {
            try
            {
                this.client = client ?? new ModioCatalogClient(configuration);
                installer = new ModioInstaller(contentRoot, configuration.GameID);
            }
            catch (Exception exception)
                when (exception is ArgumentException or InvalidOperationException)
            {
                this.client?.Dispose();
                this.client = null;
                Report(exception);
            }
        }
    }

    /// <summary>
    /// Refreshes local metadata without making a network request.
    /// </summary>
    public void Refresh()
    {
        if (disposed || IsBusy)
            return;
        try
        {
            installed = library.Discover();
            status =
                installed.Count == 0
                    ? "No mods installed. Choose Browse to find community mods."
                    : $"{installed.Count} installed {(installed.Count == 1 ? "item" : "items")}. Application content is protected.";
        }
        catch (Exception exception)
        {
            Report(exception);
        }
        Notify();
    }

    /// <summary>
    /// Filters local rows while preserving the selected item when it remains visible.
    /// </summary>
    /// <param name="query">The search text.</param>
    public void SetSearch(string query)
    {
        search = query ?? string.Empty;
        Notify();
    }

    /// <summary>
    /// Selects one installed or published entry by stable presentation identity.
    /// </summary>
    /// <param name="key">The library row's stable key.</param>
    public void Select(string key)
    {
        selected = key;
        Notify();
    }

    /// <summary>
    /// Opens the requested collection without requiring an editing project.
    /// </summary>
    /// <param name="value">The collection to present.</param>
    /// <returns>The completion of any catalog request.</returns>
    public Task OpenAsync(ContentLibraryPage value)
    {
        if (disposed || IsBusy)
            return Task.CompletedTask;
        page = value;
        search = string.Empty;
        selected = null;
        Notify();
        return value == ContentLibraryPage.Browse ? SearchAsync(0)
            : value == ContentLibraryPage.Updates ? CheckUpdatesAsync()
            : Task.CompletedTask;
    }

    /// <summary>
    /// Loads a published catalog page using the current search text.
    /// </summary>
    /// <param name="offset">The matching entries to skip.</param>
    /// <returns>The handled catalog request.</returns>
    public Task SearchAsync(int offset)
    {
        if (page != ContentLibraryPage.Browse || !RequireCatalog())
            return Task.CompletedTask;
        string query = search;
        return RunAsync(async token =>
        {
            status = "Loading community mods…";
            Notify();
            ModioCatalogPage result = await client.BrowseAsync(query, Math.Max(0, offset), token);
            token.ThrowIfCancellationRequested();
            catalog = result;
            catalog.Mods ??= Array.Empty<ModioMod>();
            foreach (ModioMod mod in catalog.Mods)
                remote[mod.ID] = mod;
            status =
                catalog.Total == 0
                    ? "No matching community mods found."
                    : $"Showing {catalog.Offset + 1}–{catalog.Offset + catalog.Mods.Length} of {catalog.Total} community mods.";
        });
    }

    /// <summary>
    /// Moves to an adjacent public catalog page without changing the current search.
    /// </summary>
    /// <param name="direction">A negative value selects the previous page.</param>
    /// <returns>The handled catalog request.</returns>
    public Task StepAsync(int direction) =>
        SearchAsync(Math.Max(0, catalog.Offset + Math.Sign(direction) * catalog.Limit));

    /// <summary>
    /// Checks each managed installation against its current published file identity.
    /// </summary>
    /// <returns>The handled update scan.</returns>
    public Task CheckUpdatesAsync()
    {
        if (!RequireCatalog())
            return Task.CompletedTask;
        return RunAsync(async token =>
        {
            int failed = 0;
            foreach (
                ContentLibraryItem item in installed.Where(item =>
                    item.Installation?.GameID == configuration.GameID
                )
            )
            {
                status = "Checking " + item.Name + "…";
                Notify();
                try
                {
                    ModioMod mod = await client.GetModAsync(item.Installation.ModID, token);
                    token.ThrowIfCancellationRequested();
                    remote[mod.ID] = mod;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failed++;
                    GameLogger.Warning(
                        "An installed mod's update could not be checked: "
                            + Redact(exception.Message)
                    );
                }
            }
            status =
                failed == 0
                    ? "Update check complete."
                    : $"Update check complete. {failed} items could not be checked; try again later.";
        });
    }

    /// <summary>
    /// Downloads and installs the selected published mod, protecting any open editing project.
    /// </summary>
    /// <returns>The handled installation.</returns>
    public Task InstallSelectedAsync()
    {
        ContentLibraryRow row = CreatePresentation().Selected;
        if (row?.CanInstall != true || !RequireCatalog())
            return Task.CompletedTask;
        return RunAsync(async token =>
        {
            if (
                installed.Any(item =>
                    item.Installation?.ModID == row.ModID
                    && item.Installation.GameID == configuration.GameID
                ) && !canReplaceContent()
            )
                throw new IOException(
                    "Close the editing project before updating installed content."
                );
            bool downloading = true;
            Progress<double> progress = new Progress<double>(fraction =>
            {
                if (disposed || !downloading || token.IsCancellationRequested)
                    return;
                status =
                    fraction >= 1
                        ? "Checking and installing the archive…"
                        : $"Downloading {row.Name}: {fraction:P0}";
                Notify();
            });
            status = "Preparing " + row.Name + "…";
            Notify();
            try
            {
                ModioMod mod = await installer.DownloadAndInstallAsync(
                    client,
                    row.ModID,
                    progress,
                    token
                );
                if (disposed)
                    return;
                remote[mod.ID] = mod;
                installed = library.Discover();
                contentChanged();
                status = mod.Name + " is installed and available in the game.";
            }
            finally
            {
                downloading = false;
            }
        });
    }

    /// <summary>
    /// Removes a previously confirmed local entry after revalidating its identity and dependencies.
    /// </summary>
    /// <param name="key">The exact entry confirmed by the user.</param>
    public void Remove(string key)
    {
        if (disposed || IsBusy)
            return;
        try
        {
            if (!canReplaceContent())
                throw new IOException(
                    "Close the editing project before removing installed content."
                );
            ContentLibraryItem item = installed.FirstOrDefault(item => item.Directory == key);
            if (item?.IsReadOnly != false)
                throw new IOException("The selected content cannot be removed.");
            library.Remove(item.Directory);
            installed = library.Discover();
            contentChanged();
            status = item.Name + " was removed.";
        }
        catch (Exception exception)
        {
            Report(exception);
        }
        Notify();
    }

    /// <summary>
    /// Requests cancellation without abandoning an archive commit already in progress.
    /// </summary>
    public void Cancel() => operation?.Cancel();

    /// <summary>
    /// Projects cached metadata into local, catalog and update collections.
    /// </summary>
    /// <returns>The current complete presentation.</returns>
    public ContentLibraryPresentation CreatePresentation()
    {
        IEnumerable<ContentLibraryRow> source =
            page == ContentLibraryPage.Browse
                ? (catalog.Mods ?? Array.Empty<ModioMod>()).Select(mod => Project(null, mod))
                : installed.Select(item =>
                    Project(
                        item,
                        item.Installation != null
                        && remote.TryGetValue(item.Installation.ModID, out ModioMod mod)
                        && mod.GameID == item.Installation.GameID
                            ? mod
                            : null
                    )
                );
        if (page == ContentLibraryPage.Updates)
            source = source.Where(row => row.CanInstall);
        ContentLibraryRow[] rows = source
            .Where(row =>
                string.IsNullOrWhiteSpace(search)
                || (row.Name + " " + row.Summary).IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase
                ) >= 0
            )
            .ToArray();
        ContentLibraryRow chosen =
            rows.FirstOrDefault(row => row.Key == selected) ?? rows.FirstOrDefault();
        return new ContentLibraryPresentation
        {
            Page = page,
            Busy = IsBusy,
            CanBrowse = client != null,
            Search = search,
            Status = status,
            CanPrevious = page == ContentLibraryPage.Browse && catalog.Offset > 0,
            CanNext =
                page == ContentLibraryPage.Browse
                && catalog.Limit > 0
                && catalog.Offset + catalog.Limit < catalog.Total,
            Rows = rows,
            Selected = chosen,
            EmptyMessage =
                page == ContentLibraryPage.Browse && client == null
                    ? "Community browsing is unavailable in this build."
                : IsBusy ? "Loading…"
                : !string.IsNullOrEmpty(search) ? "No matching mods."
                : page == ContentLibraryPage.Browse ? "No community mods published yet."
                : page == ContentLibraryPage.Updates
                    ? "No known updates. Use Check updates to refresh."
                : "No installed content. Choose Browse to get started.",
        };
    }

    /// <summary>
    /// Cancels outstanding requests and releases the owned transport before its UI is destroyed.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        operation?.Cancel();
        client?.Dispose();
    }

    /// <summary>
    /// Combines local file identity with optional public profile metadata.
    /// </summary>
    /// <param name="item">The local package, when projecting the installed library.</param>
    /// <param name="mod">The published profile, when available.</param>
    /// <returns>The selectable row and its permitted actions.</returns>
    private ContentLibraryRow Project(ContentLibraryItem item, ModioMod mod)
    {
        ModioInstallation record =
            item?.Installation
            ?? (
                mod == null
                    ? null
                    : installed
                        .FirstOrDefault(local =>
                            local.Installation?.GameID == configuration.GameID
                            && local.Installation.ModID == mod.ID
                        )
                        ?.Installation
            );
        bool current = mod?.File != null && record?.FileID == mod.File.ID;
        bool downloadable = mod?.File != null && mod.GameID == configuration.GameID;
        string state = item?.Error;
        if (string.IsNullOrEmpty(state))
            state =
                item?.IsReadOnly == true ? "Read-only"
                : current ? "Installed"
                : downloadable && record != null ? "Update available"
                : item != null || record != null ? "Installed"
                : mod?.File == null ? "No download available"
                : "Available";
        return new ContentLibraryRow
        {
            Key = item?.Directory ?? "community:" + mod.ID,
            Name = item?.Name ?? mod?.Name ?? "Unnamed content",
            Version = item?.Version ?? mod?.File?.Version ?? "Not supplied",
            Summary =
                mod?.Summary
                ?? (item?.IsPack == true ? "Content pack" : "Content mod")
                    + (
                        string.IsNullOrEmpty(item?.BasePackID)
                            ? string.Empty
                            : " · " + item.BasePackID
                    ),
            Description = !string.IsNullOrWhiteSpace(mod?.Description)
                ? mod.Description
                : mod?.Summary
                    ?? (
                        item?.Error
                        ?? (
                            string.IsNullOrEmpty(item?.BasePackID)
                                ? "Installed content pack."
                                : "Extends content pack: " + item.BasePackID
                        )
                    ),
            Author = mod?.Author?.Name,
            ImageURL = mod?.Logo?.ThumbnailURL,
            Status = state,
            Action =
                !downloadable ? "Unavailable"
                : current ? "Installed"
                : record == null ? "Install"
                : "Update",
            CanInstall = downloadable && !current && item?.IsReadOnly != true,
            CanRemove = item?.IsReadOnly == false,
            Directory = item?.Directory,
            Size = mod?.File?.Size ?? 0,
            ModID = mod?.ID ?? record?.ModID ?? 0,
        };
    }

    /// <summary>
    /// Reports missing public catalog configuration while retaining offline library management.
    /// </summary>
    /// <returns>Whether catalog work can proceed.</returns>
    private bool RequireCatalog()
    {
        if (disposed || IsBusy)
            return false;
        if (client != null)
            return true;
        status =
            "Community browsing is unavailable in this build. Installed content is still available.";
        Notify();
        return false;
    }

    /// <summary>
    /// Runs a single owned operation and restores controls after cancellation or failure.
    /// </summary>
    /// <param name="action">The operation receiving the lifetime cancellation token.</param>
    /// <returns>The handled completion.</returns>
    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (disposed || IsBusy)
            return;
        using CancellationTokenSource current = new CancellationTokenSource();
        operation = current;
        Notify();
        try
        {
            await action(current.Token);
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested)
        {
            status = "Operation cancelled.";
        }
        catch (Exception exception)
        {
            if (!disposed)
                Report(exception);
        }
        finally
        {
            operation = null;
            Notify();
        }
    }

    /// <summary>
    /// Presents an operation failure without disclosing embedded catalog credentials.
    /// </summary>
    /// <param name="exception">The failed operation.</param>
    private void Report(Exception exception)
    {
        status = Redact(exception.GetBaseException().Message);
        GameLogger.Warning("Content library: " + status);
    }

    /// <summary>
    /// Removes the configured public API key from transport diagnostics.
    /// </summary>
    /// <param name="message">The diagnostic text.</param>
    /// <returns>The displayable diagnostic.</returns>
    private string Redact(string message) =>
        string.IsNullOrEmpty(configuration.APIKey)
            ? message
            : message.Replace(configuration.APIKey, "[redacted]");

    /// <summary>
    /// Notifies the owner only while the library remains alive.
    /// </summary>
    private void Notify()
    {
        if (!disposed)
            changed();
    }
}
