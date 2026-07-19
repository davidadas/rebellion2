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

    public Texture2D Texture { get; }

    public string PrimaryText { get; }

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

    public int X { get; }

    public int Y { get; }

    public AdvisorReportMode Mode { get; }

    public Texture2D BackgroundTexture { get; }

    public Texture2D GalaxyTexture { get; }

    public string Title { get; }

    public IReadOnlyList<AdvisorReportRowRenderData> Rows { get; }
}
