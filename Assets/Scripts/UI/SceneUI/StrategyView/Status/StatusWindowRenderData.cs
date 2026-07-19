using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contains immutable presentation data for one status detail row.
/// </summary>
public sealed class StatusWindowRowRenderData
{
    /// <summary>
    /// Creates one status detail row.
    /// </summary>
    /// <param name="left">The left-column text.</param>
    /// <param name="right">The right-column text.</param>
    public StatusWindowRowRenderData(string left, string right)
    {
        Left = left ?? string.Empty;
        Right = right ?? string.Empty;
    }

    public string Left { get; }

    public string Right { get; }
}

/// <summary>
/// Contains immutable presentation data for one status window.
/// </summary>
public sealed class StatusWindowRenderData
{
    /// <summary>
    /// Creates a complete status-window presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="backgroundTexture">The themed window background.</param>
    /// <param name="centerImage">Whether status images are centered in their authored slot.</param>
    /// <param name="infoDisabled">Whether the Encyclopedia command is unavailable.</param>
    /// <param name="header">The status title.</param>
    /// <param name="imageTextures">The status images in stacking order.</param>
    /// <param name="label">The displayed item label.</param>
    /// <param name="rows">The displayed detail rows.</param>
    public StatusWindowRenderData(
        int x,
        int y,
        Texture2D backgroundTexture,
        bool centerImage,
        bool infoDisabled,
        string header,
        IReadOnlyList<Texture2D> imageTextures,
        string label,
        IReadOnlyList<StatusWindowRowRenderData> rows
    )
    {
        X = x;
        Y = y;
        BackgroundTexture = backgroundTexture;
        CenterImage = centerImage;
        InfoDisabled = infoDisabled;
        Header = header ?? string.Empty;
        ImageTextures = Copy(imageTextures, nameof(imageTextures));
        Label = label ?? string.Empty;
        Rows = Copy(rows, nameof(rows));
    }

    public int X { get; }

    public int Y { get; }

    public Texture2D BackgroundTexture { get; }

    public bool CenterImage { get; }

    public bool InfoDisabled { get; }

    public string Header { get; }

    public IReadOnlyList<Texture2D> ImageTextures { get; }

    public string Label { get; }

    public IReadOnlyList<StatusWindowRowRenderData> Rows { get; }

    /// <summary>
    /// Copies a required presentation collection into a read-only snapshot.
    /// </summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="items">The source collection.</param>
    /// <param name="parameterName">The source parameter name.</param>
    /// <returns>The read-only collection snapshot.</returns>
    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> items, string parameterName)
    {
        return new List<T>(items ?? throw new ArgumentNullException(parameterName)).AsReadOnly();
    }
}
