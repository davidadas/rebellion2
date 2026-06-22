using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class BookmarkController
{
    private readonly BookmarkEntry[] bookmarks;
    private readonly UIContext uiContext;

    public BookmarkController(UIContext uiContext, int slotCount)
    {
        this.uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
        if (slotCount < 0)
            throw new ArgumentOutOfRangeException(nameof(slotCount));

        bookmarks = new BookmarkEntry[slotCount];
    }

    public List<BookmarkRenderData> BuildRenderData()
    {
        List<BookmarkRenderData> data = new List<BookmarkRenderData>(bookmarks.Length);
        foreach (BookmarkEntry bookmark in bookmarks)
        {
            data.Add(
                new BookmarkRenderData
                {
                    Active = bookmark?.Planet != null,
                    Label = GetBookmarkLabel(bookmark),
                    IconTexture = GetBookmarkIconTexture(bookmark),
                }
            );
        }

        return data;
    }

    public bool TryAdd(PlanetIcon icon, int x, int y, GalaxyMapPlanet planet)
    {
        if (planet == null || icon == PlanetIcon.None)
            return false;

        for (int i = 0; i < bookmarks.Length; i++)
        {
            if (bookmarks[i] != null)
                continue;

            bookmarks[i] = new BookmarkEntry
            {
                Icon = icon,
                X = x,
                Y = y,
                Planet = planet,
            };
            return true;
        }

        return false;
    }

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
                bookmarks[i].Planet = freshPlanet;
        }
    }

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

    private string GetBookmarkLabel(BookmarkEntry bookmark)
    {
        if (bookmark?.Planet == null)
            return string.Empty;

        return bookmark.Planet.Planet?.GetDisplayName() ?? string.Empty;
    }

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

    private Texture2D GetBookmarkFleetTexture(Rebellion.Game.Galaxy.Planet planet)
    {
        return GetBookmarkFactionTexture(
            SelectPresentFactionId(GetFleetOwnerFactionIds(planet), planet?.OwnerInstanceID),
            PlanetIcon.Fleet
        );
    }

    private Texture2D GetBookmarkMissionTexture(Rebellion.Game.Galaxy.Planet planet)
    {
        return GetBookmarkFactionTexture(
            SelectPresentFactionId(GetMissionOwnerFactionIds(planet), planet?.OwnerInstanceID),
            PlanetIcon.Mission
        );
    }

    private Texture2D GetBookmarkFactionTexture(string factionId, PlanetIcon icon)
    {
        StrategyBookmarkIcons icons = uiContext.GetTheme(factionId)?.StrategyBookmarkIcons;
        return uiContext.GetTexture(GetBookmarkImagePath(icons, icon));
    }

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

    private static List<string> GetFleetOwnerFactionIds(Rebellion.Game.Galaxy.Planet planet)
    {
        return planet
                ?.Fleets?.Select(fleet => fleet.OwnerInstanceID)
                .Where(factionId => !string.IsNullOrEmpty(factionId))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            ?? new List<string>();
    }

    private static List<string> GetMissionOwnerFactionIds(Rebellion.Game.Galaxy.Planet planet)
    {
        return planet
                ?.Missions?.Select(mission => mission.OwnerInstanceID)
                .Where(factionId => !string.IsNullOrEmpty(factionId))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            ?? new List<string>();
    }

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
