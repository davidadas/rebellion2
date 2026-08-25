using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// Describes the transient map mode and label presented during a faction briefing.
/// </summary>
public sealed class StrategyBriefingMapPresentation
{
    public StrategyBriefingMapMode Mode { get; }

    public string Label { get; }

    public string TargetSectorInstanceID { get; }

    public string TargetPlanetInstanceID { get; }

    public string PlayerFactionInstanceID { get; }

    public string OpponentFactionInstanceID { get; }

    public bool DimBackground { get; }

    /// <summary>
    /// Creates one immutable briefing map cue.
    /// </summary>
    /// <param name="mode">The semantic map presentation mode.</param>
    /// <param name="label">The label shown above the galaxy map.</param>
    /// <param name="targetSectorInstanceID">The focused sector identifier, or null.</param>
    /// <param name="targetPlanetInstanceID">The spotlighted planet identifier, or null.</param>
    /// <param name="playerFactionInstanceID">The player faction identifier.</param>
    /// <param name="opponentFactionInstanceID">The opposing faction identifier.</param>
    /// <param name="dimBackground">Whether the galaxy artwork is dimmed behind full-brightness markers.</param>
    public StrategyBriefingMapPresentation(
        StrategyBriefingMapMode mode,
        string label,
        string targetSectorInstanceID,
        string targetPlanetInstanceID,
        string playerFactionInstanceID,
        string opponentFactionInstanceID,
        bool dimBackground = false
    )
    {
        Mode = mode;
        Label = label;
        TargetSectorInstanceID = targetSectorInstanceID;
        TargetPlanetInstanceID = targetPlanetInstanceID;
        PlayerFactionInstanceID = playerFactionInstanceID;
        OpponentFactionInstanceID = opponentFactionInstanceID;
        DimBackground = dimBackground;
    }
}

/// <summary>
/// Contains a complete immutable galaxy-map presentation snapshot.
/// </summary>
public sealed class GalaxyMapRenderData
{
    public Texture2D BackgroundTexture { get; }

    public RectInt? BackgroundBounds { get; }

    public Color BackgroundColor { get; }

    public GalaxyMapActiveFilterLabelRenderData ActiveFilterLabel { get; }

    public IReadOnlyList<GalaxyMapClusterRenderData> Clusters { get; }

    public IReadOnlyList<GalaxyMapWaypointRouteRenderData> WaypointRoutes { get; }

    /// <summary>
    /// Creates a galaxy-map presentation snapshot.
    /// </summary>
    /// <param name="backgroundTexture">The resolved galaxy background texture.</param>
    /// <param name="backgroundBounds">The optional source-space background bounds.</param>
    /// <param name="backgroundColor">The background-only color multiplier.</param>
    /// <param name="activeFilterLabel">The active galactic-information label.</param>
    /// <param name="clusters">The visible sector clusters in render order.</param>
    /// <param name="waypointRoutes">The player fleet waypoint routes.</param>
    public GalaxyMapRenderData(
        Texture2D backgroundTexture,
        RectInt? backgroundBounds,
        Color backgroundColor,
        GalaxyMapActiveFilterLabelRenderData activeFilterLabel,
        IReadOnlyList<GalaxyMapClusterRenderData> clusters,
        IReadOnlyList<GalaxyMapWaypointRouteRenderData> waypointRoutes = null
    )
    {
        BackgroundTexture = backgroundTexture;
        BackgroundBounds = backgroundBounds;
        BackgroundColor = backgroundColor;
        ActiveFilterLabel = activeFilterLabel;
        Clusters = Copy(clusters);
        WaypointRoutes = Copy(waypointRoutes);
    }

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
/// Defines one player fleet's visible waypoint route in galaxy-map source coordinates.
/// </summary>
public sealed class GalaxyMapWaypointRouteRenderData
{
    public string FleetInstanceId { get; }

    public Vector2Int Origin { get; }

    public IReadOnlyList<GalaxyMapWaypointRenderData> Waypoints { get; }

    /// <summary>
    /// Creates one immutable waypoint route.
    /// </summary>
    /// <param name="fleetInstanceId">The fleet that owns the route.</param>
    /// <param name="origin">The fleet's current route origin.</param>
    /// <param name="waypoints">The ordered waypoint markers.</param>
    public GalaxyMapWaypointRouteRenderData(
        string fleetInstanceId,
        Vector2Int origin,
        IReadOnlyList<GalaxyMapWaypointRenderData> waypoints
    )
    {
        FleetInstanceId = fleetInstanceId ?? string.Empty;
        Origin = origin;
        Waypoints = GalaxyMapRenderData.Copy(waypoints);
    }
}

/// <summary>
/// Defines one numbered destination in a projected fleet waypoint route.
/// </summary>
public readonly struct GalaxyMapWaypointRenderData
{
    public int Order { get; }

    public Vector2Int Position { get; }

    /// <summary>
    /// Creates one waypoint marker.
    /// </summary>
    /// <param name="order">The one-based route order.</param>
    /// <param name="position">The destination's galaxy-map source position.</param>
    public GalaxyMapWaypointRenderData(int order, Vector2Int position)
    {
        Order = order;
        Position = position;
    }
}

/// <summary>
/// Defines the active galactic-information label presentation.
/// </summary>
public readonly struct GalaxyMapActiveFilterLabelRenderData
{
    /// <summary>
    /// Gets whether the active filter label is visible.
    /// </summary>
    public bool Visible => !string.IsNullOrEmpty(Text);

    public string Text { get; }

    public Color Color { get; }

    public RectInt Bounds { get; }

    public int FontSize { get; }

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
}

/// <summary>
/// Defines resolved presentation for one planet-sector cluster.
/// </summary>
public sealed class GalaxyMapClusterRenderData
{
    public string SectorInstanceId { get; }

    public int SourceX { get; }

    public int SourceY { get; }

    public string Label { get; }

    public bool ShowLabel { get; }

    public IReadOnlyList<GalaxyMapStarRenderData> Stars { get; }

    /// <summary>
    /// Creates immutable planet-sector cluster presentation data.
    /// </summary>
    /// <param name="sectorInstanceId">The represented planet-sector identifier.</param>
    /// <param name="sourceX">The source-space horizontal cluster position.</param>
    /// <param name="sourceY">The source-space vertical cluster position.</param>
    /// <param name="label">The displayed sector label.</param>
    /// <param name="showLabel">Whether the label is visible.</param>
    /// <param name="stars">The rendered planet markers.</param>
    public GalaxyMapClusterRenderData(
        string sectorInstanceId,
        int sourceX,
        int sourceY,
        string label,
        bool showLabel,
        IReadOnlyList<GalaxyMapStarRenderData> stars
    )
    {
        if (string.IsNullOrEmpty(sectorInstanceId))
            throw new ArgumentException(
                "A galaxy-map cluster requires a sector identifier.",
                nameof(sectorInstanceId)
            );

        SectorInstanceId = sectorInstanceId;
        SourceX = sourceX;
        SourceY = sourceY;
        Label = label ?? string.Empty;
        ShowLabel = showLabel;
        Stars = GalaxyMapRenderData.Copy(stars);
    }
}

/// <summary>
/// Defines resolved presentation and hit-test identity for one galaxy-map planet marker.
/// </summary>
public sealed class GalaxyMapStarRenderData
{
    public string PlanetInstanceId { get; }

    public int SourceX { get; }

    public int SourceY { get; }

    public Texture2D StarTexture { get; }

    public Texture2D HeadquartersTexture { get; }

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
}
