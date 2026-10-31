using System;

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
    /// Completes pending gestures before releasing the active project.
    /// </summary>
    public void Dispose() => Open(null);
}
