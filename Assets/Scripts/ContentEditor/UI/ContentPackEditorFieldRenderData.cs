using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Carries field display choices and queries without project mutation services.
/// </summary>
public sealed class ContentPackEditorFieldRenderData
{
    private readonly Func<IReadOnlyList<string>> getMediaFiles;
    public ContentPackEditorField Field { get; }
    public IReadOnlyDictionary<string, string> ReferenceLabels { get; }
    public IReadOnlyDictionary<string, string> MediaChoices { get; }
    public bool CanReplaceMedia { get; }
    public IReadOnlyDictionary<string, Sprite> MediaImages { get; }

    /// <summary>
    /// Captures the source field, its reference choices, and its directory query.
    /// </summary>
    /// <param name="field">The authored field being presented.</param>
    /// <param name="referenceLabels">The optional ID-to-caption choices.</param>
    /// <param name="getMediaFiles">The read-only query for current directory entries.</param>
    /// <param name="mediaChoices">The optional shared image addresses available for selection.</param>
    /// <param name="canReplaceMedia">Whether shared artwork can be replaced inside the working pack.</param>
    /// <param name="mediaImages">Optional thumbnails owned by the current content asset store.</param>
    internal ContentPackEditorFieldRenderData(
        ContentPackEditorField field,
        IReadOnlyDictionary<string, string> referenceLabels,
        Func<IReadOnlyList<string>> getMediaFiles,
        IReadOnlyDictionary<string, string> mediaChoices = null,
        bool canReplaceMedia = false,
        IReadOnlyDictionary<string, Sprite> mediaImages = null
    )
    {
        Field = field;
        ReferenceLabels = referenceLabels;
        this.getMediaFiles = getMediaFiles;
        MediaChoices = mediaChoices;
        CanReplaceMedia = canReplaceMedia;
        MediaImages = mediaImages;
    }

    /// <summary>
    /// Reads current directory entries after the author changes a media folder.
    /// </summary>
    /// <returns>The available asset addresses.</returns>
    public IReadOnlyList<string> GetMediaFiles() => getMediaFiles();
}
