using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Projects galaxy objects into ordered Finder tabs and domain-backed result rows.
/// </summary>
public sealed class FinderWindowRowBuilder
{
    private readonly IReadOnlyList<GalaxyMapSector> sectors;
    private readonly IReadOnlyList<Faction> factions;
    private readonly string playerFactionId;

    /// <summary>
    /// Creates a Finder row builder for one strategy snapshot.
    /// </summary>
    /// <param name="sectors">The visible strategy sectors.</param>
    /// <param name="factions">The game factions available for Finder tabs.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    public FinderWindowRowBuilder(
        IReadOnlyList<GalaxyMapSector> sectors,
        IReadOnlyList<Faction> factions,
        string playerFactionId
    )
    {
        this.sectors = sectors ?? throw new ArgumentNullException(nameof(sectors));
        this.factions = factions ?? Array.Empty<Faction>();
        this.playerFactionId = playerFactionId;
    }

    /// <summary>
    /// Returns the ordered rows represented by one Finder mode, panel, and tab.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <param name="tab">The active Finder tab.</param>
    /// <returns>The projected rows in display order.</returns>
    public List<FinderWindowRow> GetRows(FinderMode mode, bool panel, FinderWindowTab tab)
    {
        return mode switch
        {
            FinderMode.Systems => GetSystemFinderRows(tab),
            FinderMode.Fleets => panel ? GetShipFinderRows(tab) : GetFleetFinderRows(tab),
            FinderMode.Troops => GetTroopFinderRows(tab),
            FinderMode.Personnel => panel
                ? GetSpecialForcesFinderRows(tab)
                : GetPersonnelFinderRows(tab),
            _ => new List<FinderWindowRow>(),
        };
    }

    /// <summary>
    /// Returns tabs in their stable Finder order with the player faction first.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <returns>The ordered Finder tabs.</returns>
    internal List<FinderWindowTab> GetTabs(FinderMode mode)
    {
        return FinderWindowTabCatalog.Create(mode, factions, playerFactionId);
    }

    /// <summary>
    /// Projects planetary systems visible under one system tab.
    /// </summary>
    /// <param name="tab">The active system tab.</param>
    /// <returns>Alphabetically ordered system rows.</returns>
    private List<FinderWindowRow> GetSystemFinderRows(FinderWindowTab tab)
    {
        return sectors
            .SelectMany(sector => sector.Planets)
            .Where(planet => MatchesSystemFinderTab(planet, tab))
            .OrderBy(planet => planet.Planet.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .Select(planet => new FinderWindowRow(planet.Planet.GetDisplayName(), planet))
            .ToList();
    }

    /// <summary>
    /// Projects fleets visible under one ownership tab.
    /// </summary>
    /// <param name="tab">The active ownership tab.</param>
    /// <returns>Alphabetically ordered fleet rows.</returns>
    private List<FinderWindowRow> GetFleetFinderRows(FinderWindowTab tab)
    {
        return sectors
            .SelectMany(sector => sector.Planets)
            .SelectMany(planet =>
                planet.Planet.Fleets.Select(fleet => new FinderWindowRow(
                    fleet.GetDisplayName(),
                    planet,
                    PlanetIcon.Fleet,
                    fleet
                ))
            )
            .Where(row => MatchesFactionTab(row.OwnerFactionId, tab))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Projects capital ships visible under one ownership tab.
    /// </summary>
    /// <param name="tab">The active ownership tab.</param>
    /// <returns>Alphabetically ordered ship rows.</returns>
    private List<FinderWindowRow> GetShipFinderRows(FinderWindowTab tab)
    {
        return sectors
            .SelectMany(sector => sector.Planets)
            .SelectMany(planet =>
                planet.Planet.Fleets.SelectMany(fleet =>
                    fleet.CapitalShips.Select(ship => new FinderWindowRow(
                        ship.GetDisplayName(),
                        planet,
                        PlanetIcon.Fleet,
                        ship,
                        fleet
                    ))
                )
            )
            .Where(row => MatchesFactionTab(row.OwnerFactionId, tab))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Aggregates regiment counts by planet or carrying fleet for one faction tab.
    /// </summary>
    /// <param name="tab">The active faction tab.</param>
    /// <returns>Alphabetically ordered troop-location rows.</returns>
    private List<FinderWindowRow> GetTroopFinderRows(FinderWindowTab tab)
    {
        string ownerId = tab?.FactionInstanceId;
        if (string.IsNullOrEmpty(ownerId))
            return new List<FinderWindowRow>();

        List<FinderWindowRow> rows = new List<FinderWindowRow>();
        foreach (GalaxyMapPlanet planet in sectors.SelectMany(sector => sector.Planets))
        {
            List<Regiment> planetRegiments = planet
                .Planet.Regiments.Where(regiment =>
                    string.Equals(regiment.OwnerInstanceID, ownerId, StringComparison.Ordinal)
                )
                .ToList();
            if (planetRegiments.Count > 0)
            {
                rows.Add(
                    new FinderWindowRow(
                        planet.Planet.GetDisplayName(),
                        planet,
                        PlanetIcon.Defense,
                        planet.Planet,
                        counts: CountRegimentsByType(planetRegiments)
                    )
                );
            }

            foreach (Fleet fleet in planet.Planet.Fleets)
            {
                if (!string.Equals(fleet.OwnerInstanceID, ownerId, StringComparison.Ordinal))
                    continue;

                List<Regiment> fleetRegiments = fleet.GetRegiments().ToList();
                if (fleetRegiments.Count == 0)
                    continue;

                rows.Add(
                    new FinderWindowRow(
                        fleet.GetDisplayName(),
                        planet,
                        PlanetIcon.Fleet,
                        fleet,
                        counts: CountRegimentsByType(fleetRegiments)
                    )
                );
            }
        }

        return rows.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Projects officers and special forces across missions, fleets, and planets.
    /// </summary>
    /// <param name="tab">The active faction tab.</param>
    /// <returns>Alphabetically ordered personnel rows.</returns>
    private List<FinderWindowRow> GetPersonnelFinderRows(FinderWindowTab tab)
    {
        string ownerId = tab?.FactionInstanceId;
        if (string.IsNullOrEmpty(ownerId))
            return new List<FinderWindowRow>();

        List<FinderWindowRow> rows = new List<FinderWindowRow>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (GalaxyMapPlanet planet in sectors.SelectMany(sector => sector.Planets))
        {
            foreach (Mission mission in planet.Planet.Missions)
            {
                AddPersonnelRows(
                    rows,
                    seen,
                    planet,
                    PlanetIcon.Mission,
                    mission.MainParticipants.OfType<ISceneNode>(),
                    mission: mission
                );
                AddPersonnelRows(
                    rows,
                    seen,
                    planet,
                    PlanetIcon.Mission,
                    mission.DecoyParticipants.OfType<ISceneNode>(),
                    mission: mission
                );
            }

            foreach (Fleet fleet in planet.Planet.Fleets)
            {
                AddPersonnelRows(
                    rows,
                    seen,
                    planet,
                    PlanetIcon.Fleet,
                    fleet.GetOfficers(),
                    fleet: fleet
                );
                AddPersonnelRows(
                    rows,
                    seen,
                    planet,
                    PlanetIcon.Fleet,
                    fleet.GetSpecialForces(),
                    fleet: fleet
                );
            }

            AddPersonnelRows(rows, seen, planet, PlanetIcon.Defense, planet.Planet.Officers);
            AddPersonnelRows(rows, seen, planet, PlanetIcon.Defense, planet.Planet.SpecialForces);
        }

        return rows.Where(row =>
                string.Equals(row.Node?.OwnerInstanceID, ownerId, StringComparison.Ordinal)
            )
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Aggregates special-forces counts by planet for one faction tab.
    /// </summary>
    /// <param name="tab">The active faction tab.</param>
    /// <returns>Alphabetically ordered special-forces location rows.</returns>
    private List<FinderWindowRow> GetSpecialForcesFinderRows(FinderWindowTab tab)
    {
        string ownerId = tab?.FactionInstanceId;
        if (string.IsNullOrEmpty(ownerId))
            return new List<FinderWindowRow>();

        return sectors
            .SelectMany(sector => sector.Planets)
            .Select(planet =>
            {
                List<SpecialForces> specialForces = GetSpecialForcesOnPlanet(planet)
                    .Where(unit =>
                        string.Equals(unit.OwnerInstanceID, ownerId, StringComparison.Ordinal)
                    )
                    .ToList();
                return new FinderWindowRow(
                    planet.Planet.GetDisplayName(),
                    planet,
                    PlanetIcon.Defense,
                    planet.Planet,
                    counts: CountSpecialForcesByType(specialForces)
                );
            })
            .Where(row => row.Counts.Sum() > 0)
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Reports whether a planet belongs in the active system tab.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <param name="tab">The active system tab.</param>
    /// <returns>True when the planet belongs in the tab.</returns>
    private static bool MatchesSystemFinderTab(GalaxyMapPlanet planet, FinderWindowTab tab)
    {
        if (tab == null)
            return false;
        if (tab.IsAll)
            return true;
        if (tab.IsNeutral)
            return string.IsNullOrEmpty(planet.Planet.OwnerInstanceID) && !IsUnexplored(planet);
        if (tab.IsUnexplored)
            return IsUnexplored(planet);

        return string.Equals(
            planet.Planet.OwnerInstanceID,
            tab.FactionInstanceId,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    /// Reports whether an owner identifier belongs in the active faction tab.
    /// </summary>
    /// <param name="ownerFactionId">The owner faction identifier.</param>
    /// <param name="tab">The active faction or all-results tab.</param>
    /// <returns>True when the owner belongs in the tab.</returns>
    private static bool MatchesFactionTab(string ownerFactionId, FinderWindowTab tab)
    {
        if (tab == null)
            return false;
        if (tab.IsAll)
            return true;

        return string.Equals(ownerFactionId, tab.FactionInstanceId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reports whether no faction has visited a planet.
    /// </summary>
    /// <param name="planet">The planet to inspect.</param>
    /// <returns>True when the planet has no visiting faction identifiers.</returns>
    private static bool IsUnexplored(GalaxyMapPlanet planet)
    {
        return planet.Planet.VisitingFactionIDs == null
            || planet.Planet.VisitingFactionIDs.Count == 0;
    }

    /// <summary>
    /// Collects special forces stationed, assigned, or embarked at one planet.
    /// </summary>
    /// <param name="planet">The planet whose special forces should be collected.</param>
    /// <returns>Every special-forces unit associated with the planet.</returns>
    private static List<SpecialForces> GetSpecialForcesOnPlanet(GalaxyMapPlanet planet)
    {
        List<SpecialForces> specialForces = new List<SpecialForces>();
        specialForces.AddRange(planet.Planet.SpecialForces);
        foreach (Mission mission in planet.Planet.Missions)
        {
            specialForces.AddRange(mission.MainParticipants.OfType<SpecialForces>());
            specialForces.AddRange(mission.DecoyParticipants.OfType<SpecialForces>());
        }

        foreach (Fleet fleet in planet.Planet.Fleets)
            specialForces.AddRange(fleet.GetSpecialForces());

        return specialForces;
    }

    /// <summary>
    /// Adds unique personnel rows for one location and destination window.
    /// </summary>
    /// <param name="rows">The destination result rows.</param>
    /// <param name="seen">The personnel identifiers already projected.</param>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="targetIcon">The destination window type.</param>
    /// <param name="personnel">The personnel candidates.</param>
    /// <param name="mission">The optional containing mission.</param>
    /// <param name="fleet">The optional containing fleet.</param>
    private static void AddPersonnelRows(
        List<FinderWindowRow> rows,
        HashSet<string> seen,
        GalaxyMapPlanet planet,
        PlanetIcon targetIcon,
        IEnumerable<ISceneNode> personnel,
        Mission mission = null,
        Fleet fleet = null
    )
    {
        if (personnel == null)
            return;

        foreach (ISceneNode candidate in personnel)
        {
            if (candidate == null)
                continue;

            string key = candidate.InstanceID ?? candidate.GetDisplayName();
            if (!seen.Add(key))
                continue;

            rows.Add(
                new FinderWindowRow(
                    GetPersonnelDisplayName(candidate, planet, fleet),
                    planet,
                    targetIcon,
                    candidate,
                    fleet,
                    mission: mission
                )
            );
        }
    }

    /// <summary>
    /// Formats a personnel result with its location, status, and rank.
    /// </summary>
    /// <param name="personnel">The represented personnel node.</param>
    /// <param name="planet">The represented strategy planet.</param>
    /// <param name="fleet">The optional containing fleet.</param>
    /// <returns>The complete personnel result label.</returns>
    private static string GetPersonnelDisplayName(
        ISceneNode personnel,
        GalaxyMapPlanet planet,
        Fleet fleet = null
    )
    {
        string name = personnel?.GetDisplayName() ?? string.Empty;
        string location = GetPersonnelLocationName(personnel, planet, fleet);
        string status = GetPersonnelStatusText(personnel);
        string rank = GetPersonnelRankText(personnel);
        string display = $"{name} - {location}";

        if (!string.IsNullOrEmpty(status))
            display += $" ( {status} )";
        if (!string.IsNullOrEmpty(rank))
            display += $" ( {rank} )";

        return display;
    }

    /// <summary>
    /// Resolves the fleet or planet displayed as a personnel location.
    /// </summary>
    /// <param name="personnel">The represented personnel node.</param>
    /// <param name="planet">The projected strategy planet.</param>
    /// <param name="fleet">The optional projected fleet.</param>
    /// <returns>The displayed location name.</returns>
    private static string GetPersonnelLocationName(
        ISceneNode personnel,
        GalaxyMapPlanet planet,
        Fleet fleet = null
    )
    {
        if (fleet != null)
            return fleet.GetDisplayName();
        if (personnel?.GetParentOfType<Fleet>() is Fleet parentFleet)
            return parentFleet.GetDisplayName();
        if (personnel?.GetParentOfType<Planet>() is Planet parentPlanet)
            return parentPlanet.GetDisplayName();

        return planet?.Planet?.GetDisplayName() ?? "Unknown";
    }

    /// <summary>
    /// Resolves the current personnel status label.
    /// </summary>
    /// <param name="personnel">The represented personnel node.</param>
    /// <returns>The displayed status or an empty string.</returns>
    private static string GetPersonnelStatusText(ISceneNode personnel)
    {
        if (personnel is IMovable { Movement: not null })
            return "Enroute";

        if (personnel is Officer officer)
        {
            if (officer.IsCaptured)
                return "Captured";
            if (officer.InjuryPoints > 0)
                return "Injured";
            if (officer.IsOnMission())
                return "On Mission";
            if (officer.IsKilled)
                return "Killed";
        }
        else if (personnel is SpecialForces specialForces && specialForces.IsOnMission())
        {
            return "On Mission";
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves an officer's current rank label.
    /// </summary>
    /// <param name="personnel">The represented personnel node.</param>
    /// <returns>The displayed rank or an empty string.</returns>
    private static string GetPersonnelRankText(ISceneNode personnel)
    {
        return personnel is Officer { CurrentRank: not OfficerRank.None } officer
            ? officer.CurrentRank.ToString()
            : string.Empty;
    }

    /// <summary>
    /// Counts regiment types in stable alphabetical order.
    /// </summary>
    /// <param name="regiments">The regiments to aggregate.</param>
    /// <returns>The ordered type counts.</returns>
    private static IReadOnlyList<int> CountRegimentsByType(IReadOnlyList<Regiment> regiments)
    {
        return regiments
            .GroupBy(regiment => regiment.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Count())
            .ToList();
    }

    /// <summary>
    /// Counts special-forces types in stable alphabetical order.
    /// </summary>
    /// <param name="specialForces">The special-forces units to aggregate.</param>
    /// <returns>The ordered type counts.</returns>
    private static IReadOnlyList<int> CountSpecialForcesByType(
        IReadOnlyList<SpecialForces> specialForces
    )
    {
        return specialForces
            .GroupBy(unit => unit.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Count())
            .ToList();
    }
}
