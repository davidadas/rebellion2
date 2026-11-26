using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Util.Logging;

/// <summary>
/// Records reversible document edits and groups continuous gestures into one history entry.
/// </summary>
internal sealed class ContentPackEditorHistory : IDisposable
{
    private readonly List<ContentPackEditorDocument> documents = new();
    private readonly Stack<Edit> undo = new Stack<Edit>();
    private readonly Stack<Edit> redo = new Stack<Edit>();
    private readonly Dictionary<ContentPackEditorDocument, Change> pending =
        new Dictionary<ContentPackEditorDocument, Change>();
    private readonly Dictionary<string, ContentEditorAssetChange> pendingFiles = new(
        StringComparer.Ordinal
    );
    private bool restoring;
    private int depth;
    private string description;
    private ContentPackEditorProject project;
    private string snapshotDirectory;
    private bool disposed;

    public bool CanUndo => !disposed && undo.Count > 0 && depth == 0;
    public bool CanRedo => !disposed && redo.Count > 0 && depth == 0;
    public string UndoDescription => undo.Count == 0 ? string.Empty : undo.Peek().Description;
    public string RedoDescription => redo.Count == 0 ? string.Empty : redo.Peek().Description;

    /// <summary>
    /// Observes changes to the project's owned documents.
    /// </summary>
    /// <param name="documents">The documents that remain owned by this project.</param>
    internal ContentPackEditorHistory(IEnumerable<ContentPackEditorDocument> documents)
    {
        foreach (ContentPackEditorDocument document in documents)
            Track(document);
    }

    /// <summary>
    /// Includes a newly authored document in subsequent reversible edits.
    /// </summary>
    /// <param name="document">The project-owned document.</param>
    internal void Track(ContentPackEditorDocument document)
    {
        VerifyActive();
        documents.Add(document);
        document.StateChanged += Record;
    }

    /// <summary>
    /// Releases only the revisions owned by this history after its project is no longer editable.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        foreach (ContentPackEditorDocument document in documents)
            document.StateChanged -= Record;
        documents.Clear();
        undo.Clear();
        redo.Clear();
        pending.Clear();
        pendingFiles.Clear();
        if (snapshotDirectory == null)
            return;
        try
        {
            ContentEditorPaths.VerifyWritePath(project, snapshotDirectory);
            if (Directory.Exists(snapshotDirectory))
                Directory.Delete(snapshotDirectory, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            GameLogger.Warning(
                "Could not remove temporary artwork undo files: " + exception.Message
            );
        }
    }

    /// <summary>
    /// Begins an edit group that completes when its returned scope is disposed.
    /// </summary>
    /// <param name="name">The user-facing edit description.</param>
    /// <returns>The owned edit scope.</returns>
    internal IDisposable BeginEdit(string name)
    {
        VerifyActive();
        if (depth++ == 0)
            description = name;
        return new EditScope(this);
    }

    /// <summary>
    /// Restores the preceding document states without recording a new edit.
    /// </summary>
    /// <returns>True when a completed edit was undone.</returns>
    internal bool Undo()
    {
        return Restore(undo, redo, false);
    }

    /// <summary>
    /// Reapplies the last undone document states without recording a new edit.
    /// </summary>
    /// <returns>True when an undone edit was reapplied.</returns>
    internal bool Redo()
    {
        return Restore(redo, undo, true);
    }

    /// <summary>
    /// Replaces one pack-owned file as part of the current reversible edit.
    /// </summary>
    /// <param name="project">The writable project owning the file.</param>
    /// <param name="path">The file being replaced or created.</param>
    /// <param name="bytes">The complete new file contents.</param>
    internal void WriteFile(ContentPackEditorProject project, string path, byte[] bytes)
    {
        VerifyActive();
        ContentEditorPaths.VerifyWritePath(project, path);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (BeginEdit("Replace artwork"))
        {
            if (!pendingFiles.TryGetValue(path, out ContentEditorAssetChange change))
            {
                this.project = project;
                snapshotDirectory ??= Path.Combine(
                    project.PackRootPath,
                    ContentEditorAssetChange.EditorDirectory,
                    "History",
                    Guid.NewGuid().ToString("N")
                );
                change = new ContentEditorAssetChange(project, path, snapshotDirectory);
                pendingFiles.Add(path, change);
            }
            change.Write(bytes);
        }
    }

    /// <summary>
    /// Records a single field edit or folds it into the current continuous edit.
    /// </summary>
    /// <param name="document">The changed document.</param>
    /// <param name="previous">Its text before the change.</param>
    private void Record(ContentPackEditorDocument document, ContentPackEditorDocumentState previous)
    {
        if (restoring)
            return;
        if (depth > 0)
        {
            if (pending.TryGetValue(document, out Change change))
                change.After = document.CaptureState();
            else
                pending.Add(document, new Change(document, previous, document.CaptureState()));
            return;
        }
        undo.Push(
            new Edit(
                document.DisplayName,
                new[] { new Change(document, previous, document.CaptureState()) }
            )
        );
        redo.Clear();
    }

    /// <summary>
    /// Completes a grouped edit after its last enclosing scope is disposed.
    /// </summary>
    private void EndEdit()
    {
        if (disposed)
            return;
        if (--depth != 0)
            return;
        Change[] changes = pending
            .Values.Where(change => !change.Before.Matches(change.After))
            .ToArray();
        pending.Clear();
        ContentEditorAssetChange[] files = pendingFiles
            .Values.Where(change => change.HasChanges)
            .ToArray();
        pendingFiles.Clear();
        if (changes.Length == 0 && files.Length == 0)
            return;
        undo.Push(new Edit(description, changes, files));
        redo.Clear();
    }

    /// <summary>
    /// Moves one complete edit between the history stacks and restores its document text.
    /// </summary>
    /// <param name="source">The stack to consume.</param>
    /// <param name="destination">The stack receiving the consumed edit.</param>
    /// <param name="forward">Whether to apply the edit's resulting state.</param>
    /// <returns>True when the requested history operation had an edit to restore.</returns>
    private bool Restore(Stack<Edit> source, Stack<Edit> destination, bool forward)
    {
        VerifyActive();
        if (depth != 0)
            throw new InvalidOperationException(
                "Finish the current edit before using undo or redo."
            );
        if (source.Count == 0)
            return false;
        Edit edit = source.Peek();
        foreach (ContentEditorAssetChange file in edit.Files)
            file.Verify(forward);
        restoring = true;
        try
        {
            int restored = 0;
            try
            {
                foreach (ContentEditorAssetChange file in edit.Files)
                {
                    file.Restore(forward);
                    restored++;
                }
            }
            catch
            {
                for (int index = restored - 1; index >= 0; index--)
                    edit.Files[index].Restore(!forward);
                throw;
            }
            foreach (Change change in edit.Changes)
                change.Document.RestoreState(forward ? change.After : change.Before);
        }
        finally
        {
            restoring = false;
        }
        source.Pop();
        destination.Push(edit);
        return true;
    }

    /// <summary>
    /// Rejects new edits through a project whose undo resources have been released.
    /// </summary>
    private void VerifyActive()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ContentPackEditorHistory));
    }

    /// <summary>
    /// Holds the before and after states of one affected document.
    /// </summary>
    private sealed class Change
    {
        internal ContentPackEditorDocument Document { get; }
        internal ContentPackEditorDocumentState Before { get; }
        internal ContentPackEditorDocumentState After { get; set; }

        /// <summary>
        /// Captures a reversible document change.
        /// </summary>
        /// <param name="document">The affected document.</param>
        /// <param name="before">The preceding text.</param>
        /// <param name="after">The resulting text.</param>
        internal Change(
            ContentPackEditorDocument document,
            ContentPackEditorDocumentState before,
            ContentPackEditorDocumentState after
        )
        {
            Document = document;
            Before = before;
            After = after;
        }
    }

    /// <summary>
    /// Holds the documents changed by one user operation.
    /// </summary>
    private sealed class Edit
    {
        internal string Description { get; }
        internal IReadOnlyList<Change> Changes { get; }
        internal IReadOnlyList<ContentEditorAssetChange> Files { get; }

        /// <summary>
        /// Captures one completed operation.
        /// </summary>
        /// <param name="description">The operation caption.</param>
        /// <param name="changes">The affected document states.</param>
        /// <param name="files">The file revisions changed with the documents.</param>
        internal Edit(
            string description,
            IReadOnlyList<Change> changes,
            IReadOnlyList<ContentEditorAssetChange> files = null
        )
        {
            Description = description;
            Changes = changes;
            Files = files ?? Array.Empty<ContentEditorAssetChange>();
        }
    }

    /// <summary>
    /// Completes one edit scope at most once.
    /// </summary>
    private sealed class EditScope : IDisposable
    {
        private ContentPackEditorHistory history;

        /// <summary>
        /// Associates a scope with the history that owns its edit.
        /// </summary>
        /// <param name="history">The owning edit history.</param>
        internal EditScope(ContentPackEditorHistory history)
        {
            this.history = history;
        }

        /// <summary>
        /// Completes the owned scope and detaches it from the history.
        /// </summary>
        public void Dispose()
        {
            ContentPackEditorHistory owner = history;
            history = null;
            owner?.EndEdit();
        }
    }
}
