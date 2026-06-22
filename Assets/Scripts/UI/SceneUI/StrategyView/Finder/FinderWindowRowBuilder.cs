using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

public sealed class FinderWindowRowBuilder
{
    private readonly IReadOnlyList<GalaxyMapSector> sectors;
    private readonly IReadOnlyList<Faction> factions;

    public FinderWindowRowBuilder(
        IReadOnlyList<GalaxyMapSector> sectors,
        IReadOnlyList<Faction> factions
    )
    {
        this.sectors = sectors ?? throw new ArgumentNullException(nameof(sectors));
        this.factions = factions ?? Array.Empty<Faction>();
    }

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

    public FinderWindowRenderData CreateRenderData(
        FinderWindowView view,
        UIWindow window,
        bool useUpperButtonLayout
    )
    {
        bool panel = view.GetPanel(false);
        FinderMode mode = view.Mode;
        List<FinderWindowTab> tabs = GetTabs(mode, panel);
        int activeTab = view.GetActiveTab(0, tabs.Count);
        FinderWindowTab tab = activeTab >= 0 && activeTab < tabs.Count ? tabs[activeTab] : null;
        List<FinderWindowRow> rows = GetRows(mode, panel, tab);
        int selectedIndex = view.GetSelectedIndex(-1, rows.Count);
        List<FinderWindowSourceRow> sourceRows = rows.Select(row => new FinderWindowSourceRow(row))
            .ToList();
        return new FinderWindowRenderData
        {
            X = window.X,
            Y = window.Y,
            Title = FinderWindowView.GetWindowTitle(mode, panel),
            Label = FinderWindowView.GetWindowLabel(mode, panel),
            Mode = mode,
            ActiveTab = activeTab,
            Panel = panel,
            UseUpperButtonLayout = useUpperButtonLayout,
            SelectedIndex = selectedIndex,
            SourceRows = sourceRows,
            Tabs = tabs,
        };
    }

    private List<FinderWindowTab> GetTabs(FinderMode mode, bool panel)
    {
        List<FinderWindowTab> factionTabs = GetFactionTabs();
        List<FinderWindowTab> tabs = new List<FinderWindowTab>();

        if (mode is FinderMode.Systems or FinderMode.Fleets)
            tabs.Add(FinderWindowTab.All());

        tabs.AddRange(factionTabs);

        if (mode == FinderMode.Systems)
        {
            tabs.Add(FinderWindowTab.Neutral());
            tabs.Add(FinderWindowTab.Unexplored());
        }

        return tabs;
    }

    private List<FinderWindowTab> GetFactionTabs()
    {
        return factions
            .Where(faction => faction != null && !IsNonPlayableFactionId(faction.InstanceID))
            .Select(faction =>
                FinderWindowTab.Faction(faction.InstanceID, faction.GetDisplayName())
            )
            .ToList();
    }

    private List<FinderWindowRow> GetSystemFinderRows(FinderWindowTab tab)
    {
        return sectors
            .SelectMany(sector => sector.Planets)
            .Where(planet => MatchesSystemFinderTab(planet, tab))
            .OrderBy(planet => planet.Planet.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .Select(planet => new FinderWindowRow(planet.Planet.GetDisplayName(), planet))
            .ToList();
    }

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
                rows.Add(
                    new FinderWindowRow(
                        planet.Planet.GetDisplayName(),
                        planet,
                        PlanetIcon.Defense,
                        planet.Planet,
                        counts: CountRegimentsByType(planetRegiments)
                    )
                );

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

    private List<FinderWindowRow> GetPersonnelFinderRows(FinderWindowTab tab)
    {
        string ownerId = tab?.FactionInstanceId;
        if (string.IsNullOrEmpty(ownerId))
            return new List<FinderWindowRow>();

        return sectors
            .SelectMany(sector => sector.Planets)
            .SelectMany(planet =>
                GetPersonnelOnPlanet(planet)
                    .Select(personnel => new FinderWindowRow(
                        personnel.GetDisplayName(),
                        planet,
                        PlanetIcon.Defense,
                        personnel
                    ))
            )
            .Where(row =>
                string.Equals(row.Node?.OwnerInstanceID, ownerId, StringComparison.Ordinal)
            )
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

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

    private static bool MatchesFactionTab(string ownerFactionId, FinderWindowTab tab)
    {
        if (tab == null)
            return false;
        if (tab.IsAll)
            return true;

        return string.Equals(ownerFactionId, tab.FactionInstanceId, StringComparison.Ordinal);
    }

    private static bool IsUnexplored(GalaxyMapPlanet planet)
    {
        return planet.Planet.VisitingFactionIDs == null
            || planet.Planet.VisitingFactionIDs.Count == 0;
    }

    private static bool IsNonPlayableFactionId(string factionId)
    {
        return string.IsNullOrEmpty(factionId)
            || string.Equals(factionId, "DEFAULT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(factionId, "UNKNOWN", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ISceneNode> GetPersonnelOnPlanet(GalaxyMapPlanet planet)
    {
        List<ISceneNode> personnel = new List<ISceneNode>();
        personnel.AddRange(planet.Planet.Officers);
        personnel.AddRange(planet.Planet.SpecialForces);
        foreach (Fleet fleet in planet.Planet.Fleets)
        {
            personnel.AddRange(fleet.GetOfficers());
            personnel.AddRange(fleet.GetSpecialForces());
        }

        return personnel;
    }

    private static List<SpecialForces> GetSpecialForcesOnPlanet(GalaxyMapPlanet planet)
    {
        List<SpecialForces> specialForces = new List<SpecialForces>();
        specialForces.AddRange(planet.Planet.SpecialForces);
        foreach (Fleet fleet in planet.Planet.Fleets)
        {
            specialForces.AddRange(fleet.GetSpecialForces());
        }

        return specialForces;
    }

    private static List<int> CountRegimentsByType(List<Regiment> regiments)
    {
        return regiments
            .GroupBy(regiment => regiment.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(group => group.Count())
            .ToList();
    }

    private static List<int> CountSpecialForcesByType(List<SpecialForces> specialForces)
    {
        return specialForces
            .GroupBy(unit => unit.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(group => group.Count())
            .ToList();
    }
}

public sealed class FinderWindowRow
{
    public FinderWindowRow(
        string name,
        GalaxyMapPlanet planet,
        PlanetIcon targetIcon = PlanetIcon.None,
        ISceneNode node = null,
        Fleet fleet = null,
        List<int> counts = null
    )
    {
        Name = name;
        Planet = planet;
        TargetIcon = targetIcon;
        Node = node;
        Fleet = fleet;
        Counts = counts ?? new List<int>();
    }

    public string Name { get; }
    public GalaxyMapPlanet Planet { get; }
    public PlanetIcon TargetIcon { get; }
    public ISceneNode Node { get; }
    public Fleet Fleet { get; }
    public List<int> Counts { get; }
    public string OwnerFactionId => Node?.OwnerInstanceID ?? Planet?.Planet.OwnerInstanceID;
}
