using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Owns the open authoring project and the lifetime of continuous editing gestures.
/// </summary>
internal sealed class ContentPackEditorSession : IDisposable
{
    private IDisposable mapEdit;

    internal ContentPackEditorProject Project { get; private set; }
    internal ContentPackEditorSelection Selection { get; private set; } =
        new ContentPackEditorSelection(null, null, null, "General");

    /// <summary>
    /// Replaces the active document workspace after presentation has accepted it.
    /// </summary>
    /// <param name="project">The newly opened project, or null when closing.</param>
    internal void Open(ContentPackEditorProject project)
    {
        EndMapEdit();
        Project = project;
        Selection = new ContentPackEditorSelection(null, null, null, "General");
    }

    /// <summary>
    /// Selects an authored entry and derives the available inspector context.
    /// </summary>
    /// <param name="record">The requested entry.</param>
    /// <param name="page">Its combined logical page, when applicable.</param>
    /// <param name="sector">The requested map context.</param>
    internal void Select(
        ContentPackEditorRecord record,
        ContentEditorTreeNode page,
        ContentPackEditorRecord sector
    ) =>
        Selection = ContentPackEditorProjector.BuildSelection(
            record,
            page,
            sector,
            Selection.Section
        );

    /// <summary>
    /// Remembers the explicitly selected inspector tab without changing authored content.
    /// </summary>
    /// <param name="section">The requested inspector section.</param>
    internal void SelectSection(string section) =>
        Selection = new ContentPackEditorSelection(
            Selection.Record,
            Selection.Page,
            Selection.Sector,
            section
        );

    /// <summary>
    /// Restores the preceding workspace after a replacement view failed to initialize.
    /// </summary>
    /// <param name="project">The preceding authoring project.</param>
    /// <param name="selection">Its preceding selection.</param>
    internal void Restore(ContentPackEditorProject project, ContentPackEditorSelection selection)
    {
        EndMapEdit();
        Project = project;
        Selection = selection;
    }

    /// <summary>
    /// Groups a complete map drag into one reversible project change.
    /// </summary>
    /// <param name="record">The definition being moved.</param>
    internal void BeginMapEdit(ContentPackEditorRecord record)
    {
        EndMapEdit();
        mapEdit = Project.BeginEdit("Move " + record.Name);
    }

    /// <summary>
    /// Completes the current map gesture at most once.
    /// </summary>
    internal void EndMapEdit()
    {
        IDisposable edit = mapEdit;
        mapEdit = null;
        edit?.Dispose();
    }

    /// <summary>
    /// Completes pending gestures before releasing the active project's undo resources.
    /// </summary>
    public void Dispose()
    {
        ContentPackEditorProject previous = Project;
        Open(null);
        previous?.Dispose();
    }
}

/// <summary>
/// Owns one cancellable toolkit operation and admits quitting only after it releases its work.
/// </summary>
internal sealed class ContentEditorOperation : IDisposable
{
    private readonly Action requestQuit;
    private CancellationTokenSource cancellation;
    private bool quitRequested;
    private bool disposed;

    internal bool IsRunning => cancellation != null;

    /// <summary>
    /// Connects deferred quitting to the application's existing close guard.
    /// </summary>
    /// <param name="requestQuit">The host's normal quit request.</param>
    internal ContentEditorOperation(Action requestQuit) => this.requestQuit = requestQuit;

    /// <summary>
    /// Runs one operation while the host owns its presentation and error reporting.
    /// </summary>
    /// <param name="action">The work receiving the owned cancellation token.</param>
    /// <param name="setBusy">Updates the host's busy presentation.</param>
    /// <param name="failed">Reports an operation failure.</param>
    /// <param name="canceled">Reports requested cancellation.</param>
    /// <param name="completed">Refreshes the host after the operation is released.</param>
    /// <param name="lifetime">The containing workspace's cancellation token.</param>
    /// <returns>The completion of work and host cleanup.</returns>
    internal async Task RunAsync(
        Func<CancellationToken, Task> action,
        Action<bool> setBusy,
        Action<Exception> failed,
        Action canceled = null,
        Action completed = null,
        CancellationToken lifetime = default
    )
    {
        if (IsRunning || disposed)
            return;
        using CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(
            lifetime
        );
        cancellation = source;
        try
        {
            setBusy(true);
            await action(source.Token);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            if (!disposed)
                canceled?.Invoke();
        }
        catch (Exception exception)
        {
            if (!disposed)
                failed(exception);
        }
        finally
        {
            cancellation = null;
            if (!disposed)
            {
                setBusy(false);
                completed?.Invoke();
                if (quitRequested)
                {
                    quitRequested = false;
                    requestQuit();
                }
            }
        }
    }

    /// <summary>
    /// Requests cancellation without releasing work that still needs to unwind.
    /// </summary>
    internal void Cancel() => cancellation?.Cancel();

    /// <summary>
    /// Defers closing until the current operation has finished or canceled.
    /// </summary>
    /// <returns>Whether the host can close immediately.</returns>
    internal bool CanQuit()
    {
        if (!IsRunning)
            return true;
        quitRequested = true;
        Cancel();
        return false;
    }

    /// <summary>
    /// Cancels owned work and prevents callbacks into the disposed host.
    /// </summary>
    public void Dispose()
    {
        disposed = true;
        Cancel();
    }
}
