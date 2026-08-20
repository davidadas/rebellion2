using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.Extensions;
using UnityEngine;

/// <summary>
/// Projects visible galaxy state and faction presentation into immutable map render data.
/// </summary>
public sealed class GalaxyMapProjector
{
    private const int _defaultMarkerIndex = 0;
    private static readonly Color _dimmedBackgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);

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
    /// <param name="hoveredSectorInstanceId">The planet-sector identifier whose label is revealed.</param>
    /// <param name="briefing">The transient briefing presentation, or null.</param>
    /// <returns>The complete immutable map presentation.</returns>
    public GalaxyMapRenderData Project(
        IReadOnlyList<GalaxyMapSector> sectors,
        string playerFactionId,
        GalacticInformationFilterMode filterMode,
        string hoveredSectorInstanceId,
        StrategyBriefingMapPresentation briefing = null
    )
    {
        UIContext context = GetRequiredContext();
        hoveredSectorInstanceId = briefing?.TargetSectorInstanceID ?? hoveredSectorInstanceId;
        FactionTheme playerTheme = context.GetPlayerFactionTheme();
        GalacticInformationFilterTheme filter = ResolveFilter(playerTheme, filterMode);
        List<GalaxyMapClusterRenderData> clusters = ProjectClusters(
            sectors,
            playerFactionId,
            filter,
            hoveredSectorInstanceId,
            context,
            briefing
        );

        Texture2D backgroundTexture = context.GetTexture(playerTheme?.GalaxyBackground?.ImagePath);
        return new GalaxyMapRenderData(
            backgroundTexture,
            GetBackgroundBounds(backgroundTexture, playerTheme?.GalaxyBackground?.SourcePosition),
            briefing?.DimBackground == true ? _dimmedBackgroundColor : Color.white,
            ProjectActiveFilterLabel(
                playerTheme?.GalacticInformationDisplay,
                filter,
                briefing == null ? null
                    : string.IsNullOrWhiteSpace(briefing.Label) ? "Briefing"
                    : briefing.Label
            ),
            clusters
        );
    }

    /// <summary>
    /// Requests every small galaxy marker that can become visible as player knowledge changes.
    /// </summary>
    internal void RequestMarkerTextures()
    {
        UIContext context = GetRequiredContext();
        List<FactionTheme> themes = context.GetAllThemes();
        themes.Add(context.GetTheme(null));
        foreach (FactionTheme theme in themes)
        {
            GalaxyBackground background = theme?.GalaxyBackground;
            context.GetTexture(background?.UnexploredPlanetIconPath);
            context.GetTexture(background?.DestroyedPlanetIconPath);

            PlanetIcons icons = background?.PlanetIcons;
            context.GetTexture(icons?.Small);
            context.GetTexture(icons?.Medium);
            context.GetTexture(icons?.Large);
            context.GetTexture(icons?.XL);
            context.GetTexture(icons?.Mixed);
            context.GetTexture(theme?.PlanetOverlayTheme?.GalaxyHeadquartersImagePath);
        }
    }

    /// <summary>
    /// Projects the centered label for the active galactic-information filter.
    /// </summary>
    /// <param name="theme">The active faction's galactic-information theme.</param>
    /// <param name="filter">The active filter, or null when display is off.</param>
    /// <param name="labelOverride">The transient briefing label, or null.</param>
    /// <returns>The active filter label presentation.</returns>
    private static GalaxyMapActiveFilterLabelRenderData ProjectActiveFilterLabel(
        GalacticInformationDisplayTheme theme,
        GalacticInformationFilterTheme filter,
        string labelOverride
    )
    {
        SourceRectLayout layout = theme?.ActiveFilterLabelSourceLayout;
        string label = labelOverride ?? filter?.Label;
        if (string.IsNullOrEmpty(label) || layout == null)
            return default;

        return new GalaxyMapActiveFilterLabelRenderData(
            label,
            theme.GetActiveFilterLabelColor(),
            new RectInt(layout.X, layout.Y, layout.Width, layout.Height),
            theme.ActiveFilterLabelFontSize
        );
    }

    /// <summary>
    /// Resolves a planet sector's absolute source-space map position.
    /// </summary>
    /// <param name="sector">The represented planet sector.</param>
    /// <returns>The source-space map position, or zero for a missing sector.</returns>
    public Vector2Int GetSectorSourcePosition(PlanetSector sector)
    {
        if (sector == null)
            return Vector2Int.zero;

        UIContext context = GetRequiredContext();
        SourcePointLayout backgroundPosition = context
            .GetPlayerFactionTheme()
            ?.GalaxyBackground?.SourcePosition;
        System.Drawing.Point localPosition = sector.GetPosition();
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
    /// <param name="hoveredSectorInstanceId">The planet-sector identifier whose label is revealed.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <param name="briefing">The transient briefing presentation, or null.</param>
    /// <returns>The projected cluster presentations.</returns>
    private static List<GalaxyMapClusterRenderData> ProjectClusters(
        IReadOnlyList<GalaxyMapSector> sectors,
        string playerFactionId,
        GalacticInformationFilterTheme filter,
        string hoveredSectorInstanceId,
        UIContext context,
        StrategyBriefingMapPresentation briefing
    )
    {
        List<GalaxyMapClusterRenderData> clusters = new List<GalaxyMapClusterRenderData>();
        if (sectors == null)
            return clusters;

        foreach (GalaxyMapSector sector in sectors)
        {
            if (sector?.PlanetSector == null)
                continue;

            System.Drawing.Point sectorPosition = sector.PlanetSector.GetPosition();
            clusters.Add(
                new GalaxyMapClusterRenderData(
                    sector.PlanetSector.InstanceID,
                    sectorPosition.X,
                    sectorPosition.Y,
                    sector.PlanetSector.DisplayName,
                    string.Equals(
                        sector.PlanetSector.InstanceID,
                        hoveredSectorInstanceId,
                        StringComparison.Ordinal
                    ),
                    ProjectStars(sector, playerFactionId, filter, context, sectorPosition, briefing)
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
    /// <param name="sectorPosition">The sector's source-space map position.</param>
    /// <param name="briefing">The transient briefing presentation, or null.</param>
    /// <returns>The projected marker presentations.</returns>
    private static List<GalaxyMapStarRenderData> ProjectStars(
        GalaxyMapSector sector,
        string playerFactionId,
        GalacticInformationFilterTheme filter,
        UIContext context,
        System.Drawing.Point sectorPosition,
        StrategyBriefingMapPresentation briefing
    )
    {
        List<GalaxyMapStarRenderData> stars = new List<GalaxyMapStarRenderData>();
        foreach (GalaxyMapPlanet planet in sector.Planets)
        {
            if (planet?.Planet == null)
                continue;

            GalacticInformationMarker marker =
                briefing != null && briefing.Mode != StrategyBriefingMapMode.Default
                    ? EvaluateBriefingMarker(planet.Planet, briefing, context)
                : filter == null
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
                    planetPosition.X - sectorPosition.X,
                    planetPosition.Y - sectorPosition.Y,
                    ResolveStarTexture(
                        context,
                        planet.Planet,
                        marker,
                        briefing?.Mode == StrategyBriefingMapMode.UnexploredSystems
                    ),
                    ResolveHeadquartersTexture(context, planet.Planet)
                )
            );
        }

        return stars;
    }

    /// <summary>
    /// Evaluates the original briefing-only map modes without exposing them in the player menu.
    /// </summary>
    /// <param name="planet">The visible planet snapshot.</param>
    /// <param name="briefing">The active briefing map cue.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The briefing-specific marker presentation.</returns>
    private static GalacticInformationMarker EvaluateBriefingMarker(
        Planet planet,
        StrategyBriefingMapPresentation briefing,
        UIContext context
    )
    {
        if (briefing.Mode == StrategyBriefingMapMode.Spotlight)
        {
            bool highlighted = string.Equals(
                planet.InstanceID,
                briefing.TargetPlanetInstanceID,
                StringComparison.Ordinal
            );
            return new GalacticInformationMarker(
                highlighted ? 3 : 0,
                highlighted ? planet.OwnerInstanceID : null,
                false
            );
        }

        if (briefing.Mode == StrategyBriefingMapMode.PopularSupport)
        {
            GalacticInformationFilterTheme filter = context
                .GetPlayerFactionTheme()
                ?.GalacticInformationDisplay?.GetFilter(
                    GalacticInformationFilterMode.PopularSupport
                );
            return GalacticInformationFilterEvaluator.Evaluate(
                context.Game,
                planet,
                briefing.PlayerFactionInstanceID,
                filter
            );
        }

        if (briefing.Mode == StrategyBriefingMapMode.IdleFleets)
        {
            GalacticInformationFilterTheme filter = context
                .GetPlayerFactionTheme()
                ?.GalacticInformationDisplay?.GetFilter(GalacticInformationFilterMode.IdleFleets);
            return GalacticInformationFilterEvaluator.Evaluate(
                context.Game,
                planet,
                briefing.PlayerFactionInstanceID,
                filter
            );
        }

        string highlightedFaction = briefing.Mode switch
        {
            StrategyBriefingMapMode.PlayerLoyalty => briefing.PlayerFactionInstanceID,
            StrategyBriefingMapMode.OpponentLoyalty => briefing.OpponentFactionInstanceID,
            _ => null,
        };
        if (highlightedFaction != null)
        {
            string otherFaction = string.Equals(
                highlightedFaction,
                briefing.PlayerFactionInstanceID,
                StringComparison.Ordinal
            )
                ? briefing.OpponentFactionInstanceID
                : briefing.PlayerFactionInstanceID;
            bool highlighted =
                planet.GetPopularSupport(highlightedFaction)
                > planet.GetPopularSupport(otherFaction);
            return new GalacticInformationMarker(
                highlighted ? 3 : 0,
                highlighted ? highlightedFaction : planet.OwnerInstanceID,
                false
            );
        }

        if (briefing.Mode == StrategyBriefingMapMode.UnexploredSystems)
        {
            return new GalacticInformationMarker(
                planet.IsUnexploredView ? 3 : 0,
                planet.IsUnexploredView ? null : planet.OwnerInstanceID,
                false
            );
        }

        if (briefing.Mode == StrategyBriefingMapMode.MilitaryControl)
        {
            bool controlled = !string.IsNullOrEmpty(planet.OwnerInstanceID);
            return new GalacticInformationMarker(
                controlled ? 3 : 0,
                controlled ? planet.OwnerInstanceID : null,
                false
            );
        }

        if (briefing.Mode == StrategyBriefingMapMode.AllDefenses)
        {
            int defenseCount =
                planet.GetChildren<Regiment>().Count(IsActive)
                + planet.GetChildren<Starfighter>().Count(IsActive)
                + planet
                    .GetChildren<Building>()
                    .Count(building =>
                        building.ManufacturingStatus == ManufacturingStatus.Complete
                        && building.DefenseFacilityClass != DefenseFacilityClass.None
                    );
            return new GalacticInformationMarker(
                Math.Min(3, defenseCount),
                planet.OwnerInstanceID,
                false
            );
        }

        throw new ArgumentOutOfRangeException(
            nameof(briefing),
            briefing.Mode,
            "Unsupported briefing map mode."
        );
    }

    /// <summary>
    /// Determines whether a manufacturable unit is complete and stationary.
    /// </summary>
    /// <param name="entity">The unit or facility to inspect.</param>
    /// <returns>True when the entity contributes to an active-defense count.</returns>
    private static bool IsActive(IManufacturable entity)
    {
        return entity != null
            && entity.GetManufacturingStatus() == ManufacturingStatus.Complete
            && entity.GetTransitMovement() == null;
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
    /// <param name="highlightUnexplored">Whether unexplored planets use the briefing highlight.</param>
    /// <returns>The resolved marker texture.</returns>
    private static Texture2D ResolveStarTexture(
        UIContext context,
        Planet planet,
        GalacticInformationMarker marker,
        bool highlightUnexplored
    )
    {
        if (planet.IsUnexploredView && !highlightUnexplored)
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
