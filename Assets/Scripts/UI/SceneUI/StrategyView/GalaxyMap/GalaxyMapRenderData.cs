using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// Contains a complete immutable galaxy-map presentation snapshot.
/// </summary>
public sealed class GalaxyMapRenderData
{
    /// <summary>
    /// Creates a galaxy-map presentation snapshot.
    /// </summary>
    /// <param name="backgroundTexture">The resolved galaxy background texture.</param>
    /// <param name="backgroundBounds">The optional source-space background bounds.</param>
    /// <param name="activeFilterLabel">The active galactic-information label.</param>
    /// <param name="clusters">The visible system clusters in render order.</param>
    public GalaxyMapRenderData(
        Texture2D backgroundTexture,
        RectInt? backgroundBounds,
        GalaxyMapActiveFilterLabelRenderData activeFilterLabel,
        IReadOnlyList<GalaxyMapClusterRenderData> clusters
    )
    {
        BackgroundTexture = backgroundTexture;
        BackgroundBounds = backgroundBounds;
        ActiveFilterLabel = activeFilterLabel;
        Clusters = Copy(clusters);
    }

    /// <summary>
    /// Gets the resolved galaxy background texture.
    /// </summary>
    public Texture2D BackgroundTexture { get; }

    /// <summary>
    /// Gets the optional source-space background bounds.
    /// </summary>
    public RectInt? BackgroundBounds { get; }

    /// <summary>
    /// Gets the active galactic-information label presentation.
    /// </summary>
    public GalaxyMapActiveFilterLabelRenderData ActiveFilterLabel { get; }

    /// <summary>
    /// Gets the visible system clusters in render order.
    /// </summary>
    public IReadOnlyList<GalaxyMapClusterRenderData> Clusters { get; }

    /// <summary>
    /// Copies a collection into an isolated read-only snapshot.
    /// </summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <returns>An isolated read-only copy.</returns>
    internal static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<T>();

        T[] copy = new T[source.Count];
        for (int i = 0; i < source.Count; i++)
            copy[i] = source[i];

        return new ReadOnlyCollection<T>(copy);
    }
}

/// <summary>
/// Defines the active galactic-information label presentation.
/// </summary>
public readonly struct GalaxyMapActiveFilterLabelRenderData
{
    /// <summary>
    /// Creates active galactic-information label presentation data.
    /// </summary>
    /// <param name="text">The displayed filter name.</param>
    /// <param name="color">The faction presentation color.</param>
    /// <param name="bounds">The source-space label bounds.</param>
    /// <param name="fontSize">The source-space font size.</param>
    public GalaxyMapActiveFilterLabelRenderData(
        string text,
        Color color,
        RectInt bounds,
        int fontSize
    )
    {
        Text = text ?? string.Empty;
        Color = color;
        Bounds = bounds;
        FontSize = fontSize;
    }

    /// <summary>
    /// Gets whether the active filter label is visible.
    /// </summary>
    public bool Visible => !string.IsNullOrEmpty(Text);

    /// <summary>
    /// Gets the displayed filter name.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the faction presentation color.
    /// </summary>
    public Color Color { get; }

    /// <summary>
    /// Gets the source-space label bounds.
    /// </summary>
    public RectInt Bounds { get; }

    /// <summary>
    /// Gets the source-space font size.
    /// </summary>
    public int FontSize { get; }
}

/// <summary>
/// Defines resolved presentation for one planet-system cluster.
/// </summary>
public sealed class GalaxyMapClusterRenderData
{
    /// <summary>
    /// Creates immutable planet-system cluster presentation data.
    /// </summary>
    /// <param name="systemInstanceId">The represented planet-system identifier.</param>
    /// <param name="sourceX">The source-space horizontal cluster position.</param>
    /// <param name="sourceY">The source-space vertical cluster position.</param>
    /// <param name="label">The displayed system label.</param>
    /// <param name="showLabel">Whether the label is visible.</param>
    /// <param name="stars">The rendered planet markers.</param>
    public GalaxyMapClusterRenderData(
        string systemInstanceId,
        int sourceX,
        int sourceY,
        string label,
        bool showLabel,
        IReadOnlyList<GalaxyMapStarRenderData> stars
    )
    {
        if (string.IsNullOrEmpty(systemInstanceId))
            throw new ArgumentException(
                "A galaxy-map cluster requires a system identifier.",
                nameof(systemInstanceId)
            );

        SystemInstanceId = systemInstanceId;
        SourceX = sourceX;
        SourceY = sourceY;
        Label = label ?? string.Empty;
        ShowLabel = showLabel;
        Stars = GalaxyMapRenderData.Copy(stars);
    }

    /// <summary>
    /// Gets the represented planet-system identifier.
    /// </summary>
    public string SystemInstanceId { get; }

    /// <summary>
    /// Gets the source-space horizontal cluster position.
    /// </summary>
    public int SourceX { get; }

    /// <summary>
    /// Gets the source-space vertical cluster position.
    /// </summary>
    public int SourceY { get; }

    /// <summary>
    /// Gets the displayed system label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets whether the system label is visible.
    /// </summary>
    public bool ShowLabel { get; }

    /// <summary>
    /// Gets the rendered planet markers.
    /// </summary>
    public IReadOnlyList<GalaxyMapStarRenderData> Stars { get; }
}

/// <summary>
/// Defines resolved presentation and hit-test identity for one galaxy-map planet marker.
/// </summary>
public sealed class GalaxyMapStarRenderData
{
    /// <summary>
    /// Creates immutable planet-marker presentation data.
    /// </summary>
    /// <param name="planetInstanceId">The represented planet identifier.</param>
    /// <param name="sourceX">The horizontal marker offset within its cluster.</param>
    /// <param name="sourceY">The vertical marker offset within its cluster.</param>
    /// <param name="starTexture">The resolved star-marker texture.</param>
    /// <param name="headquartersTexture">The optional resolved headquarters overlay.</param>
    public GalaxyMapStarRenderData(
        string planetInstanceId,
        int sourceX,
        int sourceY,
        Texture2D starTexture,
        Texture2D headquartersTexture
    )
    {
        PlanetInstanceId = planetInstanceId ?? string.Empty;
        SourceX = sourceX;
        SourceY = sourceY;
        StarTexture = starTexture;
        HeadquartersTexture = headquartersTexture;
    }

    /// <summary>
    /// Gets the represented planet identifier.
    /// </summary>
    public string PlanetInstanceId { get; }

    /// <summary>
    /// Gets the horizontal marker offset within its cluster.
    /// </summary>
    public int SourceX { get; }

    /// <summary>
    /// Gets the vertical marker offset within its cluster.
    /// </summary>
    public int SourceY { get; }

    /// <summary>
    /// Gets the resolved star-marker texture.
    /// </summary>
    public Texture2D StarTexture { get; }

    /// <summary>
    /// Gets the optional resolved headquarters overlay.
    /// </summary>
    public Texture2D HeadquartersTexture { get; }
}
