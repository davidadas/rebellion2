using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns strategy bookmark slots, reconciliation, restoration, and presentation projection.
/// </summary>
public sealed class BookmarkController
{
    private readonly BookmarkEntry[] bookmarks;
    private readonly UIContext uiContext;

    /// <summary>
    /// Creates bookmark storage from the active faction's authored slot layout.
    /// </summary>
    /// <param name="uiContext">The active strategy presentation context.</param>
    public BookmarkController(UIContext uiContext)
    {
        this.uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
        StrategyBookmarkLayout layout = uiContext.GetPlayerFactionTheme()?.StrategyBookmarkLayout;
        if (layout == null)
            throw new MissingReferenceException("StrategyBookmarkLayout is missing.");

        bookmarks = new BookmarkEntry[layout.GetSlotCount()];
    }

    /// <summary>
    /// Projects every bookmark slot into immutable presentation data.
    /// </summary>
    /// <returns>The bookmark presentations in stable slot order.</returns>
    public IReadOnlyList<BookmarkRenderData> BuildRenderData()
    {
        List<BookmarkRenderData> data = new List<BookmarkRenderData>(bookmarks.Length);
        foreach (BookmarkEntry bookmark in bookmarks)
        {
            data.Add(
                new BookmarkRenderData(
                    bookmark?.Planet != null,
                    GetBookmarkLabel(bookmark),
                    GetBookmarkIconTexture(bookmark)
                )
            );
        }

        return data;
    }

    /// <summary>
    /// Adds one bookmark to the first available authored slot.
    /// </summary>
    /// <param name="icon">The bookmarked feature category.</param>
    /// <param name="x">The source-space horizontal window coordinate.</param>
    /// <param name="y">The source-space vertical window coordinate.</param>
    /// <param name="planet">The bookmarked galaxy-map planet.</param>
    /// <returns>True when an available slot accepted the bookmark.</returns>
    public bool TryAdd(PlanetIcon icon, int x, int y, GalaxyMapPlanet planet)
    {
        if (planet == null || icon == PlanetIcon.None)
            return false;

        for (int i = 0; i < bookmarks.Length; i++)
        {
            if (bookmarks[i] != null)
                continue;

            bookmarks[i] = new BookmarkEntry(icon, x, y, planet);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes and returns the bookmark stored in one slot.
    /// </summary>
    /// <param name="index">The zero-based authored slot index.</param>
    /// <param name="bookmark">Receives the removed bookmark.</param>
    /// <returns>True when the slot contained a valid bookmark.</returns>
    public bool TryTake(int index, out BookmarkEntry bookmark)
    {
        bookmark = null;
        if (index < 0 || index >= bookmarks.Length)
            return false;

        bookmark = bookmarks[index];
        if (bookmark?.Planet == null)
            return false;

        bookmarks[index] = null;
        return true;
    }

    /// <summary>
    /// Removes and returns a bookmark matching one planet and feature category.
    /// </summary>
    /// <param name="planet">The bookmarked galaxy-map planet.</param>
    /// <param name="icon">The bookmarked feature category.</param>
    /// <returns>The removed bookmark, or null when no slot matches.</returns>
    public BookmarkEntry Take(GalaxyMapPlanet planet, PlanetIcon icon)
    {
        for (int i = 0; i < bookmarks.Length; i++)
        {
            BookmarkEntry bookmark = bookmarks[i];
            if (bookmark?.Planet != planet || bookmark.Icon != icon)
                continue;

            bookmarks[i] = null;
            return bookmark;
        }

        return null;
    }

    /// <summary>
    /// Replaces stale bookmarked planet projections from the latest visible galaxy snapshot.
    /// </summary>
    /// <param name="sectors">The latest visible galaxy sectors.</param>
    public void ReconcilePlanets(IReadOnlyList<GalaxyMapSector> sectors)
    {
        if (sectors == null)
            return;

        Dictionary<string, GalaxyMapPlanet> planetsById = BuildPlanetLookup(sectors);
        for (int i = 0; i < bookmarks.Length; i++)
        {
            string planetId = bookmarks[i]?.Planet?.Planet?.InstanceID;
            if (planetId == null)
                continue;

            if (planetsById.TryGetValue(planetId, out GalaxyMapPlanet freshPlanet))
                bookmarks[i].ReconcilePlanet(freshPlanet);
        }
    }

    /// <summary>
    /// Builds a stable planet lookup from the latest visible galaxy sectors.
    /// </summary>
    /// <param name="sectors">The visible galaxy sectors.</param>
    /// <returns>Galaxy-map planets keyed by persistent planet identifier.</returns>
    private static Dictionary<string, GalaxyMapPlanet> BuildPlanetLookup(
        IReadOnlyList<GalaxyMapSector> sectors
    )
    {
        Dictionary<string, GalaxyMapPlanet> planetsById = new Dictionary<string, GalaxyMapPlanet>();
        foreach (GalaxyMapSector sector in sectors)
        {
            if (sector?.Planets == null)
                continue;

            foreach (GalaxyMapPlanet planet in sector.Planets)
            {
                string planetId = planet?.Planet?.InstanceID;
                if (planetId != null)
                    planetsById[planetId] = planet;
            }
        }

        return planetsById;
    }

    /// <summary>
    /// Gets one bookmark's displayed planet name.
    /// </summary>
    /// <param name="bookmark">The bookmark to project.</param>
    /// <returns>The displayed planet name, or an empty string.</returns>
    private string GetBookmarkLabel(BookmarkEntry bookmark)
    {
        if (bookmark?.Planet == null)
            return string.Empty;

        return bookmark.Planet.Planet?.GetDisplayName() ?? string.Empty;
    }

    /// <summary>
    /// Resolves one bookmark's feature icon from current domain state and faction theme.
    /// </summary>
    /// <param name="bookmark">The bookmark to project.</param>
    /// <returns>The resolved icon texture, or null when the feature is absent.</returns>
    private Texture2D GetBookmarkIconTexture(BookmarkEntry bookmark)
    {
        if (bookmark?.Planet == null)
            return null;

        PlanetIcon icon = bookmark.Icon;
        Rebellion.Game.Galaxy.Planet planet = bookmark.Planet.Planet;
        return icon switch
        {
            PlanetIcon.Facility => HasFacilities(planet)
                ? GetBookmarkFactionTexture(planet?.OwnerInstanceID, icon)
                : null,
            PlanetIcon.Defense => HasDefenses(planet)
                ? GetBookmarkFactionTexture(planet?.OwnerInstanceID, icon)
                : null,
            PlanetIcon.Fleet => GetBookmarkFleetTexture(planet),
            PlanetIcon.Mission => GetBookmarkMissionTexture(planet),
            _ => null,
        };
    }

    /// <summary>
    /// Resolves a fleet bookmark icon from the factions currently present at the planet.
    /// </summary>
    /// <param name="planet">The bookmarked planet.</param>
    /// <returns>The resolved fleet bookmark texture.</returns>
    private Texture2D GetBookmarkFleetTexture(Rebellion.Game.Galaxy.Planet planet)
    {
        return GetBookmarkFactionTexture(
            SelectPresentFactionId(GetFleetOwnerFactionIds(planet), planet?.OwnerInstanceID),
            PlanetIcon.Fleet
        );
    }

    /// <summary>
    /// Resolves a mission bookmark icon from the factions currently active at the planet.
    /// </summary>
    /// <param name="planet">The bookmarked planet.</param>
    /// <returns>The resolved mission bookmark texture.</returns>
    private Texture2D GetBookmarkMissionTexture(Rebellion.Game.Galaxy.Planet planet)
    {
        return GetBookmarkFactionTexture(
            SelectPresentFactionId(GetMissionOwnerFactionIds(planet), planet?.OwnerInstanceID),
            PlanetIcon.Mission
        );
    }

    /// <summary>
    /// Resolves one faction's themed bookmark icon for a feature category.
    /// </summary>
    /// <param name="factionId">The faction presentation identifier.</param>
    /// <param name="icon">The feature category.</param>
    /// <returns>The resolved bookmark texture.</returns>
    private Texture2D GetBookmarkFactionTexture(string factionId, PlanetIcon icon)
    {
        StrategyBookmarkIcons icons = uiContext.GetTheme(factionId)?.StrategyBookmarkIcons;
        return uiContext.GetTexture(GetBookmarkImagePath(icons, icon));
    }

    /// <summary>
    /// Selects a bookmark image path from one themed icon collection.
    /// </summary>
    /// <param name="icons">The faction bookmark icons.</param>
    /// <param name="icon">The feature category.</param>
    /// <returns>The selected image path.</returns>
    private static string GetBookmarkImagePath(StrategyBookmarkIcons icons, PlanetIcon icon)
    {
        return icon switch
        {
            PlanetIcon.Facility => icons?.FacilityImagePath,
            PlanetIcon.Defense => icons?.DefenseImagePath,
            PlanetIcon.Fleet => icons?.FleetImagePath,
            PlanetIcon.Mission => icons?.MissionImagePath,
            _ => null,
        };
    }

    /// <summary>
    /// Reports whether one planet currently contains a bookmarkable production facility.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <returns>True when at least one supported facility exists.</returns>
    private static bool HasFacilities(Rebellion.Game.Galaxy.Planet planet)
    {
        return planet?.Buildings?.Any(building =>
                building.GetBuildingType()
                    is Rebellion.Game.Units.BuildingType.Mine
                        or Rebellion.Game.Units.BuildingType.Refinery
                        or Rebellion.Game.Units.BuildingType.Shipyard
                        or Rebellion.Game.Units.BuildingType.TrainingFacility
                        or Rebellion.Game.Units.BuildingType.ConstructionFacility
            ) == true;
    }

    /// <summary>
    /// Reports whether one planet currently contains a bookmarkable defensive unit.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <returns>True when at least one supported defense exists.</returns>
    private static bool HasDefenses(Rebellion.Game.Galaxy.Planet planet)
    {
        return planet != null
            && (
                planet.Buildings.Count(building =>
                    building.GetBuildingType()
                        is Rebellion.Game.Units.BuildingType.Defense
                            or Rebellion.Game.Units.BuildingType.Weapon
                ) > 0
                || planet.Regiments.Count > 0
                || planet.Starfighters.Count > 0
            );
    }

    /// <summary>
    /// Gets the distinct fleet-owner faction identifiers currently present at one planet.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <returns>The faction identifiers in domain order.</returns>
    private static List<string> GetFleetOwnerFactionIds(Rebellion.Game.Galaxy.Planet planet)
    {
        return planet
                ?.Fleets?.Select(fleet => fleet.OwnerInstanceID)
                .Where(factionId => !string.IsNullOrEmpty(factionId))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            ?? new List<string>();
    }

    /// <summary>
    /// Gets the distinct mission-owner faction identifiers currently present at one planet.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <returns>The faction identifiers in domain order.</returns>
    private static List<string> GetMissionOwnerFactionIds(Rebellion.Game.Galaxy.Planet planet)
    {
        return planet
                ?.Missions?.Select(mission => mission.OwnerInstanceID)
                .Where(factionId => !string.IsNullOrEmpty(factionId))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            ?? new List<string>();
    }

    /// <summary>
    /// Selects the represented faction when one or both factions are present.
    /// </summary>
    /// <param name="presentFactionIds">The faction identifiers currently present.</param>
    /// <param name="ownerFactionId">The planet owner faction identifier.</param>
    /// <returns>The faction identifier whose bookmark art should be shown.</returns>
    private static string SelectPresentFactionId(
        IReadOnlyList<string> presentFactionIds,
        string ownerFactionId
    )
    {
        if (presentFactionIds == null || presentFactionIds.Count == 0)
            return null;

        if (presentFactionIds.Count == 1)
            return presentFactionIds[0];

        return presentFactionIds.FirstOrDefault(factionId =>
                !string.Equals(factionId, ownerFactionId, StringComparison.Ordinal)
            ) ?? presentFactionIds[0];
    }
}
