using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Content.Modio;
using Rebellion.Util.Logging;

/// <summary>
/// Owns community browsing and installation work while its Options window remains open.
/// </summary>
public sealed class ModioBrowserSession : IDisposable
{
    private readonly ModioConfiguration configuration;
    private readonly ModioCatalogClient client;
    private readonly ModioInstaller installer;
    private readonly Action changed;
    private readonly Action installed;
    private CancellationTokenSource operation;
    private ModioCatalogPage page = new ModioCatalogPage();
    private ModioBrowserRow[] rows = Array.Empty<ModioBrowserRow>();
    private bool visible;
    private bool busy;
    private bool disposed;
    private string search = string.Empty;
    private string status = string.Empty;

    public bool IsVisible => visible;
    public bool IsBusy => busy;

    /// <summary>
    /// Selects the catalog transport, installation directory, and owning menu callbacks.
    /// </summary>
    /// <param name="configuration">The public catalog configuration.</param>
    /// <param name="contentRoot">The installed content directory.</param>
    /// <param name="changed">Refreshes the owning menu after state changes.</param>
    /// <param name="installed">Refreshes local content after a successful installation.</param>
    /// <param name="client">An optional catalog transport supplied by the owner.</param>
    public ModioBrowserSession(
        ModioConfiguration configuration,
        string contentRoot,
        Action changed,
        Action installed,
        ModioCatalogClient client = null
    )
    {
        this.configuration =
            configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
        this.installed = installed ?? throw new ArgumentNullException(nameof(installed));
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
                GameLogger.Warning(
                    "mod.io configuration could not be loaded: " + exception.Message
                );
            }
        }
    }

    /// <summary>
    /// Opens the browser and reads the first matching page.
    /// </summary>
    /// <returns>The completion of catalog loading.</returns>
    public Task OpenAsync()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ModioBrowserSession));
        visible = true;
        return SearchAsync(search, 0);
    }

    /// <summary>
    /// Reads a catalog page, cancelling any previous browsing request.
    /// </summary>
    /// <param name="query">The requested search text.</param>
    /// <param name="offset">The number of matching entries to skip.</param>
    /// <returns>The completion of catalog loading.</returns>
    public Task SearchAsync(string query, int offset)
    {
        if (!visible || disposed)
            return Task.CompletedTask;
        search = query ?? string.Empty;
        if (client == null)
        {
            status = "Community mod browsing is unavailable in this build.";
            NotifyChanged();
            return Task.CompletedTask;
        }
        return RunAsync(async token =>
        {
            status = "Loading community mods…";
            NotifyChanged();
            ModioCatalogPage result = await client.BrowseAsync(search, offset, token);
            token.ThrowIfCancellationRequested();
            page = result;
            page.Mods ??= Array.Empty<ModioMod>();
            RefreshRows();
            status =
                page.Total == 0
                    ? "No matching mods found."
                    : $"Showing {page.Offset + 1}–{page.Offset + page.Mods.Length} of {page.Total} mods.";
        });
    }

    /// <summary>
    /// Reads the previous or next available page for the current search.
    /// </summary>
    /// <param name="direction">A negative value selects the previous page; a positive value selects the next.</param>
    /// <returns>The completion of catalog loading.</returns>
    public Task StepAsync(int direction)
    {
        if (busy || page.Limit <= 0 || direction == 0)
            return Task.CompletedTask;
        int offset = Math.Max(0, page.Offset + Math.Sign(direction) * page.Limit);
        if (offset >= page.Total)
            return Task.CompletedTask;
        return SearchAsync(search, offset);
    }

    /// <summary>
    /// Downloads and installs the selected mod's currently published archive.
    /// </summary>
    /// <param name="index">The selected row on the current catalog page.</param>
    /// <returns>The completion of installation.</returns>
    public Task InstallAsync(int index)
    {
        if (
            busy
            || !visible
            || disposed
            || client == null
            || index < 0
            || index >= page.Mods.Length
        )
            return Task.CompletedTask;
        long modID = page.Mods[index].ID;
        return RunAsync(async token =>
        {
            ModioMod mod = await client.GetModAsync(modID, token);
            if (
                mod.GameID != configuration.GameID
                || mod.ID != modID
                || mod.File == null
                || mod.File.ModID != modID
            )
                throw new InvalidDataException(
                    "This mod has no compatible archive available to download."
                );
            string temporary = Path.Combine(
                Path.GetTempPath(),
                "reb2-modio-" + Guid.NewGuid().ToString("N") + ".zip"
            );
            try
            {
                bool downloading = true;
                Progress<double> progress = new Progress<double>(fraction =>
                {
                    if (
                        token.IsCancellationRequested
                        || !downloading
                        || !busy
                        || operation == null
                        || operation.Token != token
                    )
                        return;
                    status = $"Downloading {mod.Name}: {fraction:P0}";
                    NotifyChanged();
                });
                await client.DownloadAsync(mod.File, temporary, progress, token);
                downloading = false;
                status = "Checking and installing the mod…";
                NotifyChanged();
                await installer.InstallAsync(mod, temporary, token);
                token.ThrowIfCancellationRequested();
                page.Mods[index] = mod;
                RefreshRows();
                installed();
                status =
                    "Installed. Return to installed mods and select Apply to load the changes.";
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        });
    }

    /// <summary>
    /// Projects catalog and installed-file identities into browser rows.
    /// </summary>
    /// <returns>The current browser presentation.</returns>
    public ModioBrowserRenderData CreateRenderData()
    {
        return new ModioBrowserRenderData
        {
            Visible = visible,
            Busy = busy,
            CanSearch = client != null && !busy,
            CanGoBack = !busy && page.Offset > 0,
            CanGoForward = !busy && page.Limit > 0 && page.Offset + page.Limit < page.Total,
            Search = search,
            Status = status,
            Rows = rows,
        };
    }

    /// <summary>
    /// Reads installation records after catalog or installation changes, keeping rendering free of file access.
    /// </summary>
    private void RefreshRows()
    {
        rows = (page.Mods ?? Array.Empty<ModioMod>())
            .Select(mod =>
            {
                ModioInstallation local = installer?.GetInstalled(mod.ID);
                bool current = mod.File != null && local?.FileID == mod.File.ID;
                return new ModioBrowserRow
                {
                    Name = mod.Name,
                    Summary = mod.Summary,
                    Version = mod.File?.Version ?? string.Empty,
                    Action =
                        mod.File == null ? "Unavailable"
                        : current ? "Installed"
                        : local != null ? "Update"
                        : "Install",
                    CanInstall = mod.File != null && !current,
                };
            })
            .ToArray();
    }

    /// <summary>
    /// Cancels outstanding requests when the browser or its owning window closes.
    /// </summary>
    public void Close()
    {
        visible = false;
        operation?.Cancel();
        NotifyChanged();
    }

    /// <summary>
    /// Cancels outstanding work and releases the catalog transport.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        Close();
        client?.Dispose();
    }

    /// <summary>
    /// Refreshes a live owner without calling back after disposal.
    /// </summary>
    private void NotifyChanged()
    {
        if (!disposed)
            changed();
    }

    /// <summary>
    /// Runs one operation and reports failures without breaking the Options window.
    /// </summary>
    /// <param name="action">The cancellable browsing or installation operation.</param>
    /// <returns>The handled completion of the operation.</returns>
    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        operation?.Cancel();
        using CancellationTokenSource current = new CancellationTokenSource();
        operation = current;
        busy = true;
        NotifyChanged();
        try
        {
            await action(current.Token);
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (ReferenceEquals(operation, current) && !current.IsCancellationRequested)
            {
                status = exception.Message.Replace(configuration.APIKey, "[redacted]");
                GameLogger.Warning("mod.io: " + status);
            }
        }
        finally
        {
            if (ReferenceEquals(operation, current))
            {
                operation = null;
                busy = false;
                NotifyChanged();
            }
        }
    }
}
