using System;
using System.Collections.Generic;

/// <summary>
/// Carries field display choices and queries without project mutation services.
/// </summary>
public sealed class ContentPackEditorFieldRenderData
{
    private readonly Func<IReadOnlyList<string>> getMediaFiles;
    public ContentPackEditorField Field { get; }
    public IReadOnlyDictionary<string, string> ReferenceLabels { get; }

    /// <summary>
    /// Captures the source field, its reference choices, and its directory query.
    /// </summary>
    /// <param name="field">The authored field being presented.</param>
    /// <param name="referenceLabels">The optional ID-to-caption choices.</param>
    /// <param name="getMediaFiles">The read-only query for current directory entries.</param>
    internal ContentPackEditorFieldRenderData(
        ContentPackEditorField field,
        IReadOnlyDictionary<string, string> referenceLabels,
        Func<IReadOnlyList<string>> getMediaFiles
    )
    {
        Field = field;
        ReferenceLabels = referenceLabels;
        this.getMediaFiles = getMediaFiles;
    }

    /// <summary>
    /// Reads current directory entries after the author changes a media folder.
    /// </summary>
    /// <returns>The available asset addresses.</returns>
    public IReadOnlyList<string> GetMediaFiles() => getMediaFiles();
}
