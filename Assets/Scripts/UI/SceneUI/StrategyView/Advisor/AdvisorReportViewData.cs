using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contains immutable presentation data for one advisor-report row.
/// </summary>
public sealed class AdvisorReportRowRenderData
{
    /// <summary>
    /// Creates one advisor-report row presentation.
    /// </summary>
    /// <param name="texture">The optional row image.</param>
    /// <param name="primaryText">The primary row text.</param>
    /// <param name="secondaryText">The secondary row text.</param>
    public AdvisorReportRowRenderData(Texture2D texture, string primaryText, string secondaryText)
    {
        Texture = texture;
        PrimaryText = primaryText ?? string.Empty;
        SecondaryText = secondaryText ?? string.Empty;
    }

    /// <summary>
    /// Gets the optional row texture.
    /// </summary>
    public Texture2D Texture { get; }

    /// <summary>
    /// Gets the primary row text.
    /// </summary>
    public string PrimaryText { get; }

    /// <summary>
    /// Gets the secondary row text.
    /// </summary>
    public string SecondaryText { get; }
}

/// <summary>
/// Contains immutable presentation data for an advisor-report window.
/// </summary>
public sealed class AdvisorReportWindowRenderData
{
    /// <summary>
    /// Creates a complete advisor-report presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="mode">The report mode.</param>
    /// <param name="backgroundTexture">The window background texture.</param>
    /// <param name="galaxyTexture">The optional galaxy texture.</param>
    /// <param name="title">The report title.</param>
    /// <param name="rows">The projected report rows.</param>
    public AdvisorReportWindowRenderData(
        int x,
        int y,
        AdvisorReportMode mode,
        Texture2D backgroundTexture,
        Texture2D galaxyTexture,
        string title,
        IReadOnlyList<AdvisorReportRowRenderData> rows
    )
    {
        X = x;
        Y = y;
        Mode = mode;
        BackgroundTexture = backgroundTexture;
        GalaxyTexture = galaxyTexture;
        Title = title ?? string.Empty;
        if (rows == null)
            throw new ArgumentNullException(nameof(rows));

        List<AdvisorReportRowRenderData> rowSnapshot = new List<AdvisorReportRowRenderData>(
            rows.Count
        );
        foreach (AdvisorReportRowRenderData row in rows)
        {
            rowSnapshot.Add(
                row ?? throw new ArgumentException("Rows cannot contain null.", nameof(rows))
            );
        }

        Rows = rowSnapshot.AsReadOnly();
    }

    /// <summary>
    /// Gets the source-space horizontal position.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the source-space vertical position.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the report mode selecting the authored row presentation.
    /// </summary>
    public AdvisorReportMode Mode { get; }

    /// <summary>
    /// Gets the window background texture.
    /// </summary>
    public Texture2D BackgroundTexture { get; }

    /// <summary>
    /// Gets the optional galaxy texture.
    /// </summary>
    public Texture2D GalaxyTexture { get; }

    /// <summary>
    /// Gets the report title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the immutable report-row snapshot.
    /// </summary>
    public IReadOnlyList<AdvisorReportRowRenderData> Rows { get; }
}
