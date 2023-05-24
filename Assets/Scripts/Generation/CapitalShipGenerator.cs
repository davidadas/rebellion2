using System.Collections.Generic;
using System.Linq;
using System;
using IDictionaryExtensions;
using IEnumerableExtensions;

/// <summary>
///
/// </summary>
public class CapitalShipGenerator : UnitGenerator<CapitalShip>
{
    /// <summary>
    /// Default constructor, constructs a CapitalShipGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="resourceManager">The resource manager from which to load game data.</param>
    public CapitalShipGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public IConfig[] getCapitalShipConfigs()
    {
        GameSize galaxySize = GetGameSummary().GalaxySize;
        IConfig config = GetConfig();
        Dictionary<string, CapitalShip> capitalShipsByFaction =
            new Dictionary<string, CapitalShip>();

        // Subsequent GameSizes include the previous values.
        // Therefore, we must include them in our calculations.
        IEnumerable<int> sizeRange = Enumerable.Range((int)GameSize.Small, (int)galaxySize);
        IEnumerable<IConfig> capitalShipConfigs = sizeRange.SelectMany(
            (intSize) =>
            {
                string stringSize = ((GameSize)intSize).ToString();
                IConfig[] configs = config.GetValue<IConfig[]>(
                    $"CapitalShips.InitialCapitalShips.GalaxySize.{stringSize}"
                );
                return configs;
            }
        );

        return capitalShipConfigs.ToArray();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="capitalShips"></param>
    /// <param name="capitalShipConfigs"></param>
    /// <returns></returns>
    private CapitalShip[] getCapitalShipMapping(
        CapitalShip[] capitalShips,
        IConfig[] capitalShipConfigs
    )
    {
        List<CapitalShip> mappedCapitalShips = new List<CapitalShip>();

        foreach (IConfig capitalShipConfig in capitalShipConfigs)
        {
            CapitalShip capitalShip = capitalShips.First(
                (capitalShip) => capitalShip.GameID == capitalShipConfig.GetValue<string>("GameID")
            );
            capitalShip.InitialParentGameID = capitalShipConfig.GetValue<string>(
                "InitialParentGameID"
            );
            capitalShip.OwnerGameID = capitalShipConfig.GetValue<string>("OwnerGameID");
            mappedCapitalShips.Add(capitalShip);
        }

        return mappedCapitalShips.ToArray();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="capitalShips"></param>
    /// <param name="planetSystems"></param>
    /// <returns></returns>
    private CapitalShip[] deployUnits(CapitalShip[] capitalShips, PlanetSystem[] planetSystems)
    {
        // Flatten the list of planets from planet systems.
        IEnumerable<Planet> flattenedPlanets = planetSystems.SelectMany(
            (planetSystem) => planetSystem.Planets
        );

        // Create a dictionary of planet GameIDs to planets, containing only HQs.
        Dictionary<string, Planet> hqs = flattenedPlanets
            .Where((planet) => planet.IsHeadquarters)
            .ToDictionary((planet) => planet.GameID, planet => planet);

        // Create a dictionary of factions to their owned planets (sans HQs).
        Dictionary<string, Planet[]> planets = flattenedPlanets
            .Where((planet) => planet.OwnerGameID != null && !planet.IsHeadquarters)
            .GroupBy((planet) => planet.OwnerGameID, planet => planet)
            .ToDictionary((grouping) => grouping.Key, (grouping) => grouping.ToArray());

        foreach (CapitalShip capitalShip in capitalShips)
        {
            // Handle case where capital ship has pre-defined parent.
            // We can only assign to HQs, as planets are randomly generated.
            if (capitalShip.InitialParentGameID != null)
            {
                Planet planet = hqs[capitalShip.InitialParentGameID];
                planet.AddCapitalShip(capitalShip);
            }
            // Otherwise, randomly assign to a planet.
            else
            {
                Planet planet = planets[capitalShip.OwnerGameID].Shuffle().First();
                planet.AddCapitalShip(capitalShip);
            }
        }

        return capitalShips;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="capitalShips"></param>
    /// <returns></returns>
    public override CapitalShip[] SelectUnits(CapitalShip[] capitalShips)
    {
        int startingResearchLevel = this.GetGameSummary().StartingResearchLevel;
        CapitalShip[] selectedShips = capitalShips
            .Where(
                capitalship =>
                    capitalship.RequiredResearchLevel <= this.GetGameSummary().StartingResearchLevel
            )
            .ToArray();

        return selectedShips;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="capitalShips"></param>
    /// <returns></returns>
    public override CapitalShip[] DecorateUnits(CapitalShip[] capitalShips)
    {
        // No op.
        return capitalShips;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public override CapitalShip[] DeployUnits(CapitalShip[] units, PlanetSystem[] destinations)
    {
        // Get the config for each capital ship we are adding to the scene.
        IConfig[] capitalShipConfigs = getCapitalShipConfigs();
        // Map each config previously returned to a specific capital ship.
        CapitalShip[] mappedCapitalShips = getCapitalShipMapping(units, capitalShipConfigs);
        // Deploy each capital ship to the scene graph.
        CapitalShip[] deployedCapitalShips = deployUnits(mappedCapitalShips, destinations);

        return deployedCapitalShips;
    }
}
