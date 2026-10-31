/// <summary>
/// Describes the current inspector entry and map context independently of Unity controls.
/// </summary>
public sealed class ContentPackEditorSelection
{
    public ContentPackEditorRecord Record { get; }
    public ContentEditorTreeNode Page { get; }
    public ContentPackEditorRecord Sector { get; }
    public string Section { get; }

    /// <summary>
    /// Captures one complete inspector selection.
    /// </summary>
    /// <param name="record">The selected definition.</param>
    /// <param name="page">Its logical page, when applicable.</param>
    /// <param name="sector">The map context to display.</param>
    /// <param name="section">The selected inspector tab.</param>
    internal ContentPackEditorSelection(
        ContentPackEditorRecord record,
        ContentEditorTreeNode page,
        ContentPackEditorRecord sector,
        string section
    )
    {
        Record = record;
        Page = page;
        Sector = sector;
        Section = section;
    }
}
