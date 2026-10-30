using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Records reversible document edits and groups continuous gestures into one history entry.
/// </summary>
internal sealed class ContentPackEditorHistory
{
    private readonly Stack<Edit> undo = new Stack<Edit>();
    private readonly Stack<Edit> redo = new Stack<Edit>();
    private readonly Dictionary<ContentPackEditorDocument, Change> pending =
        new Dictionary<ContentPackEditorDocument, Change>();
    private bool restoring;
    private int depth;
    private string description;

    public bool CanUndo => undo.Count > 0 && depth == 0;
    public bool CanRedo => redo.Count > 0 && depth == 0;
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
    internal void Track(ContentPackEditorDocument document) => document.Changed += Record;

    /// <summary>
    /// Begins an edit group that completes when its returned scope is disposed.
    /// </summary>
    /// <param name="name">The user-facing edit description.</param>
    /// <returns>The owned edit scope.</returns>
    internal IDisposable BeginEdit(string name)
    {
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
    /// Records a single field edit or folds it into the current continuous edit.
    /// </summary>
    /// <param name="document">The changed document.</param>
    /// <param name="previous">Its text before the change.</param>
    private void Record(ContentPackEditorDocument document, string previous)
    {
        if (restoring)
            return;
        if (depth > 0)
        {
            if (pending.TryGetValue(document, out Change change))
                change.After = document.Text;
            else
                pending.Add(document, new Change(document, previous, document.Text));
            return;
        }
        undo.Push(
            new Edit(document.DisplayName, new[] { new Change(document, previous, document.Text) })
        );
        redo.Clear();
    }

    /// <summary>
    /// Completes a grouped edit after its last enclosing scope is disposed.
    /// </summary>
    private void EndEdit()
    {
        if (--depth != 0)
            return;
        Change[] changes = pending.Values.Where(change => change.Before != change.After).ToArray();
        pending.Clear();
        if (changes.Length == 0)
            return;
        undo.Push(new Edit(description, changes));
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
        if (depth != 0)
            throw new InvalidOperationException(
                "Finish the current edit before using undo or redo."
            );
        if (source.Count == 0)
            return false;
        Edit edit = source.Pop();
        restoring = true;
        try
        {
            foreach (Change change in edit.Changes)
                change.Document.RestoreText(forward ? change.After : change.Before);
        }
        finally
        {
            restoring = false;
        }
        destination.Push(edit);
        return true;
    }

    /// <summary>
    /// Holds the before and after states of one affected document.
    /// </summary>
    private sealed class Change
    {
        internal ContentPackEditorDocument Document { get; }
        internal string Before { get; }
        internal string After { get; set; }

        /// <summary>
        /// Captures a reversible document change.
        /// </summary>
        /// <param name="document">The affected document.</param>
        /// <param name="before">The preceding text.</param>
        /// <param name="after">The resulting text.</param>
        internal Change(ContentPackEditorDocument document, string before, string after)
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

        /// <summary>
        /// Captures one completed operation.
        /// </summary>
        /// <param name="description">The operation caption.</param>
        /// <param name="changes">The affected document states.</param>
        internal Edit(string description, IReadOnlyList<Change> changes)
        {
            Description = description;
            Changes = changes;
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
