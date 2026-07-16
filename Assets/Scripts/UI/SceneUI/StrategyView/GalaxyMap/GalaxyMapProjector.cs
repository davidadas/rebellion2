using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using UnityEngine;

/// <summary>
/// Projects visible galaxy state and faction presentation into immutable map render data.
/// </summary>
public sealed class GalaxyMapProjector
{
    private const int _defaultMarkerIndex = 0;

    private readonly Func<UIContext> getUIContext;

    /// <summary>
    /// Creates a galaxy-map projector backed by the current strategy UI context.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    public GalaxyMapProjector(Func<UIContext> getUIContext)
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
    }

    /// <summary>
    /// Projects the current visible sectors into a complete galaxy-map presentation snapshot.
    /// </summary>
    /// <param name="sectors">The visible sectors in render order.</param>
    /// <param name="playerFactionId">The viewing player's faction identifier.</param>
    /// <param name="filterMode">The active galactic-information filter.</param>
    /// <param name="hoveredSystemInstanceId">The planet-system identifier whose label is revealed.</param>
    /// <returns>The complete immutable map presentation.</returns>
    public GalaxyMapRenderData Project(
        IReadOnlyList<GalaxyMapSector> sectors,
        string playerFactionId,
        GalacticInformationFilterMode filterMode,
        string hoveredSystemInstanceId
    )
    {
        UIContext context = GetRequiredContext();
        FactionTheme playerTheme = context.GetPlayerFactionTheme();
        GalacticInformationFilterTheme filter = ResolveFilter(playerTheme, filterMode);
        List<GalaxyMapClusterRenderData> clusters = ProjectClusters(
            sectors,
            playerFactionId,
            filter,
            hoveredSystemInstanceId,
            context
        );

        Texture2D backgroundTexture = context.GetTexture(playerTheme?.GalaxyBackground?.ImagePath);
        return new GalaxyMapRenderData(
            backgroundTexture,
            GetBackgroundBounds(backgroundTexture, playerTheme?.GalaxyBackground?.SourcePosition),
            ProjectActiveFilterLabel(playerTheme?.GalacticInformationDisplay, filter),
            clusters
        );
    }

    /// <summary>
    /// Projects the centered label for the active galactic-information filter.
    /// </summary>
    /// <param name="theme">The active faction's galactic-information theme.</param>
    /// <param name="filter">The active filter, or null when display is off.</param>
    /// <returns>The active filter label presentation.</returns>
    private static GalaxyMapActiveFilterLabelRenderData ProjectActiveFilterLabel(
        GalacticInformationDisplayTheme theme,
        GalacticInformationFilterTheme filter
    )
    {
        SourceRectLayout layout = theme?.ActiveFilterLabelSourceLayout;
        if (filter == null || layout == null)
            return default;

        return new GalaxyMapActiveFilterLabelRenderData(
            filter.Label,
            theme.GetActiveFilterLabelColor(),
            new RectInt(layout.X, layout.Y, layout.Width, layout.Height),
            theme.ActiveFilterLabelFontSize
        );
    }

    /// <summary>
    /// Resolves a planet system's absolute source-space map position.
    /// </summary>
    /// <param name="system">The represented planet system.</param>
    /// <returns>The source-space map position, or zero for a missing system.</returns>
    public Vector2Int GetSystemSourcePosition(PlanetSystem system)
    {
        if (system == null)
            return Vector2Int.zero;

        UIContext context = GetRequiredContext();
        SourcePointLayout backgroundPosition = context
            .GetPlayerFactionTheme()
            ?.GalaxyBackground?.SourcePosition;
        System.Drawing.Point localPosition = system.GetPosition();
        return new Vector2Int(
            (backgroundPosition?.X ?? 0) + localPosition.X,
            (backgroundPosition?.Y ?? 0) + localPosition.Y
        );
    }

    /// <summary>
    /// Selects the configured marker artwork path for a marker intensity.
    /// </summary>
    /// <param name="icons">The configured marker artwork.</param>
    /// <param name="markerIndex">The zero-based marker intensity.</param>
    /// <returns>The best configured marker path for the requested intensity.</returns>
    internal static string GetPlanetIconPath(PlanetIcons icons, int markerIndex)
    {
        return markerIndex switch
        {
            0 => icons?.Small,
            1 => icons?.Medium ?? icons?.Small,
            2 => icons?.Large ?? icons?.Medium ?? icons?.Small,
            _ => icons?.XL ?? icons?.Large ?? icons?.Medium ?? icons?.Small,
        };
    }

    /// <summary>
    /// Projects every visible sector into a reusable cluster snapshot.
    /// </summary>
    /// <param name="sectors">The visible sectors in render order.</param>
    /// <param name="playerFactionId">The viewing player's faction identifier.</param>
    /// <param name="filter">The active filter configuration, or null when display is off.</param>
    /// <param name="hoveredSystemInstanceId">The planet-system identifier whose label is revealed.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The projected cluster presentations.</returns>
    private static List<GalaxyMapClusterRenderData> ProjectClusters(
        IReadOnlyList<GalaxyMapSector> sectors,
        string playerFactionId,
        GalacticInformationFilterTheme filter,
        string hoveredSystemInstanceId,
        UIContext context
    )
    {
        List<GalaxyMapClusterRenderData> clusters = new List<GalaxyMapClusterRenderData>();
        if (sectors == null)
            return clusters;

        foreach (GalaxyMapSector sector in sectors)
        {
            if (sector?.System == null)
                continue;

            System.Drawing.Point systemPosition = sector.System.GetPosition();
            clusters.Add(
                new GalaxyMapClusterRenderData(
                    sector.System.InstanceID,
                    systemPosition.X,
                    systemPosition.Y,
                    sector.System.DisplayName,
                    string.Equals(
                        sector.System.InstanceID,
                        hoveredSystemInstanceId,
                        StringComparison.Ordinal
                    ),
                    ProjectStars(sector, playerFactionId, filter, context, systemPosition)
                )
            );
        }

        return clusters;
    }

    /// <summary>
    /// Projects every visible planet in one sector into marker presentation data.
    /// </summary>
    /// <param name="sector">The visible sector.</param>
    /// <param name="playerFactionId">The viewing player's faction identifier.</param>
    /// <param name="filter">The active filter configuration, or null when display is off.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <param name="systemPosition">The sector's source-space map position.</param>
    /// <returns>The projected marker presentations.</returns>
    private static List<GalaxyMapStarRenderData> ProjectStars(
        GalaxyMapSector sector,
        string playerFactionId,
        GalacticInformationFilterTheme filter,
        UIContext context,
        System.Drawing.Point systemPosition
    )
    {
        List<GalaxyMapStarRenderData> stars = new List<GalaxyMapStarRenderData>();
        foreach (GalaxyMapPlanet planet in sector.Planets)
        {
            if (planet?.Planet == null)
                continue;

            GalacticInformationMarker marker =
                filter == null
                    ? new GalacticInformationMarker(
                        _defaultMarkerIndex,
                        planet.Planet.OwnerInstanceID,
                        false
                    )
                    : GalacticInformationFilterEvaluator.Evaluate(
                        context.Game,
                        planet.Planet,
                        playerFactionId,
                        filter
                    );
            System.Drawing.Point planetPosition = planet.Planet.GetPosition();
            stars.Add(
                new GalaxyMapStarRenderData(
                    planet.Planet.InstanceID,
                    planetPosition.X - systemPosition.X,
                    planetPosition.Y - systemPosition.Y,
                    ResolveStarTexture(context, planet.Planet, marker),
                    ResolveHeadquartersTexture(context, planet.Planet)
                )
            );
        }

        return stars;
    }

    /// <summary>
    /// Resolves the active filter configuration for the current display mode.
    /// </summary>
    /// <param name="playerTheme">The current player faction theme.</param>
    /// <param name="filterMode">The requested galactic-information filter.</param>
    /// <returns>The configured filter, or null when the display is off.</returns>
    private static GalacticInformationFilterTheme ResolveFilter(
        FactionTheme playerTheme,
        GalacticInformationFilterMode filterMode
    )
    {
        return filterMode == GalacticInformationFilterMode.DisplayOff
            ? null
            : playerTheme?.GalacticInformationDisplay?.GetFilter(filterMode);
    }

    /// <summary>
    /// Resolves the marker texture for one visible planet and evaluated filter result.
    /// </summary>
    /// <param name="context">The current strategy UI context.</param>
    /// <param name="planet">The visible planet.</param>
    /// <param name="marker">The evaluated marker result.</param>
    /// <returns>The resolved marker texture.</returns>
    private static Texture2D ResolveStarTexture(
        UIContext context,
        Planet planet,
        GalacticInformationMarker marker
    )
    {
        if (planet.IsUnexploredView)
        {
            return context.GetTexture(
                context.GetPlayerFactionTheme()?.GalaxyBackground?.UnexploredPlanetIconPath
            );
        }

        if (marker.Mixed)
        {
            return context.GetTexture(
                context.GetPlayerFactionTheme()?.GalaxyBackground?.PlanetIcons?.Mixed
            );
        }

        PlanetIcons icons = context
            .GetTheme(marker.FactionInstanceId)
            ?.GalaxyBackground?.PlanetIcons;
        return context.GetTexture(GetPlanetIconPath(icons, marker.Index));
    }

    /// <summary>
    /// Resolves the headquarters overlay shown above one visible planet marker.
    /// </summary>
    /// <param name="context">The current strategy UI context.</param>
    /// <param name="planet">The visible planet.</param>
    /// <returns>The resolved overlay texture, or null when no overlay is visible.</returns>
    private static Texture2D ResolveHeadquartersTexture(UIContext context, Planet planet)
    {
        if (
            planet.IsUnexploredView
            || !planet.IsHeadquarters
            || string.IsNullOrEmpty(planet.OwnerInstanceID)
        )
            return null;

        return context.GetTexture(
            context
                .GetTheme(planet.OwnerInstanceID)
                ?.PlanetOverlayTheme?.GalaxyHeadquartersImagePath
        );
    }

    /// <summary>
    /// Computes source-space background bounds from configured placement and resolved art.
    /// </summary>
    /// <param name="texture">The resolved background texture.</param>
    /// <param name="position">The configured source-space position.</param>
    /// <returns>The source-space bounds, or null when no texture is available.</returns>
    private static RectInt? GetBackgroundBounds(Texture2D texture, SourcePointLayout position)
    {
        if (texture == null)
            return null;

        return new RectInt(
            position?.X ?? 0,
            position?.Y ?? 0,
            UILayout.ToSourceUnits(texture.width),
            UILayout.ToSourceUnits(texture.height)
        );
    }

    /// <summary>
    /// Gets the current strategy UI context and rejects incomplete screen composition.
    /// </summary>
    /// <returns>The current strategy UI context.</returns>
    private UIContext GetRequiredContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("Galaxy-map projection requires a UI context.");
    }
}
