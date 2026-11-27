using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// Remembers working folders and loads published packages for the explicitly connected teams.
/// </summary>
internal sealed class ContentEditorProjectsController : IDisposable
{
    private readonly ContentEditorProjectsView view;
    private readonly ContentEditorSettingsManager settings;
    private readonly ModPackageStore store;
    private readonly Func<ContentPackEditorProject> current;
    private readonly Func<string[]> teams;
    private readonly Action<string> open;
    private readonly Action<string> publish;
    private readonly Action connect;
    private readonly ThunderstoreClient client = new();
    private CancellationTokenSource request;
    private string teamKey = string.Empty;
    private ThunderstorePackage[] packages = Array.Empty<ThunderstorePackage>();
    private bool disposed;

    /// <summary>
    /// Connects project navigation without taking ownership of the editor or its credentials.
    /// </summary>
    /// <param name="view">The projects overview.</param>
    /// <param name="settings">The shared local preferences.</param>
    /// <param name="store">The shared inventory identifying managed and local content folders.</param>
    /// <param name="current">The currently edited project.</param>
    /// <param name="teams">The already authenticated team names.</param>
    /// <param name="open">The guarded project-opening action.</param>
    /// <param name="publish">The guarded project-publishing action.</param>
    /// <param name="connect">The explicit connection action.</param>
    internal ContentEditorProjectsController(
        ContentEditorProjectsView view,
        ContentEditorSettingsManager settings,
        ModPackageStore store,
        Func<ContentPackEditorProject> current,
        Func<string[]> teams,
        Action<string> open,
        Action<string> publish,
        Action connect
    )
    {
        this.view = view;
        this.settings = settings;
        this.store = store;
        this.current = current;
        this.teams = teams;
        this.open = open;
        this.publish = publish;
        this.connect = connect;
        view.OpenRequested += open;
        view.PublishRequested += publish;
        view.ConnectRequested += connect;
        view.PackagePageRequested += Application.OpenURL;
        view.RefreshRequested += Show;
        view.RenderPublished(packages, false);
    }

    /// <summary>
    /// Retains successfully opened working folders independently of installed packages.
    /// </summary>
    /// <param name="project">The successfully opened project.</param>
    internal void Remember(ContentPackEditorProject project)
    {
        if (project.IsInstalled)
            return;
        try
        {
            settings.RememberProject(project.ProjectRootPath);
            view.ShowLocalStatus(string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            view.ShowLocalStatus(
                "Couldn’t remember this folder. Your open project is still available.",
                true
            );
        }
        RefreshLocal();
    }

    /// <summary>
    /// Refreshes lightweight local metadata without loading inherited game documents.
    /// </summary>
    internal void RefreshLocal()
    {
        view.SetCurrentProject(current());
        string[] discovered;
        try
        {
            discovered = store.GetUnmanagedProjectFolders();
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
                        or UnauthorizedAccessException
                        or ArgumentException
            )
        {
            discovered = Array.Empty<string>();
            view.ShowLocalStatus(
                "Couldn’t scan local working folders. Remembered projects are still available.",
                true
            );
        }
        StringComparer comparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        view.RenderLocal(
            settings
                .Load()
                .ProjectFolders.Concat(discovered)
                .Distinct(comparer)
                .Select(ContentEditorLocalProject.Read)
                .ToArray()
        );
    }

    /// <summary>
    /// Clears the previous team's listings when the explicit account connection changes.
    /// </summary>
    internal void ConnectionChanged()
    {
        string next = string.Join("\n", teams());
        if (teamKey == next)
            return;
        teamKey = next;
        request?.Cancel();
        packages = Array.Empty<ThunderstorePackage>();
        view.SetBusy(false);
        view.RenderPublished(packages, teams().Length > 0);
        if (view.gameObject.activeInHierarchy)
            Show();
    }

    /// <summary>
    /// Shows local folders immediately and refreshes published metadata without reading stored credentials.
    /// </summary>
    internal async void Show()
    {
        if (disposed)
            return;
        RefreshLocal();
        string[] owners = teams().ToArray();
        if (owners.Length == 0)
        {
            request?.Cancel();
            packages = Array.Empty<ThunderstorePackage>();
            view.RenderPublished(packages, false);
            return;
        }
        request?.Cancel();
        using CancellationTokenSource cancellation = new();
        request = cancellation;
        view.SetBusy(true);
        try
        {
            ThunderstorePackage[][] results = await System.Threading.Tasks.Task.WhenAll(
                owners.Select(team => client.GetTeamPackagesAsync(team, cancellation.Token))
            );
            if (disposed || cancellation.IsCancellationRequested || !owners.SequenceEqual(teams()))
                return;
            packages = results
                .SelectMany(items => items)
                .GroupBy(package => package.full_name, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(package => package.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            view.RenderPublished(packages, true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A replacement query or account change owns the next visible result.
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
                        or ArgumentException
                        or InvalidOperationException
            )
        {
            if (!disposed && !cancellation.IsCancellationRequested)
                view.ShowPublishedStatus(
                    "Couldn’t load published packages. Refresh to retry. " + exception.Message,
                    true
                );
        }
        finally
        {
            if (ReferenceEquals(request, cancellation))
            {
                request = null;
                if (!disposed)
                    view.SetBusy(false);
            }
        }
    }

    /// <summary>
    /// Cancels outstanding metadata reads and releases the overview's subscriptions.
    /// </summary>
    public void Dispose()
    {
        disposed = true;
        request?.Cancel();
        view.OpenRequested -= open;
        view.PublishRequested -= publish;
        view.ConnectRequested -= connect;
        view.PackagePageRequested -= Application.OpenURL;
        view.RefreshRequested -= Show;
    }
}
