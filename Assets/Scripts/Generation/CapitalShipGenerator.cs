using System;
using System.Collections.Generic;
using System.Linq;
using IDictionaryExtensions;
using IEnumerableExtensions;
using ObjectExtensions;

/// <summary>
/// Responsible for generating and deploying Capital Ships to the scene graph.
/// </summary>
public class CapitalShipGenerator : UnitGenerator<CapitalShip>
{
    /// <summary>
    /// Constructs a CapitalShipGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="resourceManager">The resource manager from which to load game data.</param>
    public CapitalShipGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    /// Retrieves the configuration for all capital ships based on the current galaxy size.
    /// </summary>
    /// <returns>An array of <cref="IConfig"/> objects representing the capital ships for the current galaxy size.</returns>
    private IConfig[] GetCapitalShipConfigs()
    {
        GameSize galaxySize = GetGameSummary().GalaxySize;
        IConfig config = GetConfig();

        // Generate a range of galaxy sizes and retrieve configurations for each
        return config
            .GetValue<IConfig[]>(
                $"CapitalShips.InitialCapitalShips.GalaxySize.{(galaxySize).ToString()}"
            )
            .ToArray();
    }

    /// <summary>
    /// Maps the provided capital ships to the config files, capital ship selection and placement
    /// are set. The result is a list of capital ships which should then be deployed to the scene graph.
    /// </summary>
    /// <param name="capitalShips">The list of available capital ships.</param>
    /// <param name="capitalShipConfigs">The configurations to map to the ships.</param>
    /// <returns>An array of mapped CapitalShip objects with updated configurations.</returns>
    private CapitalShip[] GetCapitalShipsToDeploy(
        CapitalShip[] capitalShips,
        IConfig[] capitalShipConfigs
    )
    {
        return capitalShipConfigs
            .Select(config =>
            {
                // Find matching ship and update its properties.
                CapitalShip ship = capitalShips.First(s =>
                    s.TypeID == config.GetValue<string>("TypeID")
                );
                ship.InitialParentInstanceID = config.GetValue<string>("InitialParentInstanceID");
                ship.OwnerInstanceID = config.GetValue<string>("OwnerInstanceID");
                return ship;
            })
            .ToArray();
    }

    /// <summary>
    /// Assigns the capital ships to planets or fleets based on their configurations.
    /// </summary>
    /// <param name="capitalShips">The list of capital ships to assign.</param>
    /// <param name="planetSystems">The list of planetary systems in the galaxy.</param>
    /// <returns>The array of assigned CapitalShip objects.</returns>
    private CapitalShip[] AssignUnits(CapitalShip[] capitalShips, PlanetSystem[] planetSystems)
    {
        Dictionary<string, Planet> hqs = GetHeadquarters(planetSystems);
        Dictionary<string, Planet[]> factionPlanets = GetFactionPlanets(planetSystems);

        foreach (CapitalShip ship in capitalShips)
        {
            CapitalShip copy = (CapitalShip)ship.GetDeepCopy();
            copy.SetOwnerInstanceID(ship.GetOwnerInstanceID());
            Planet targetPlanet = GetTargetPlanet(copy, hqs, factionPlanets);
            Fleet fleet = GetOrCreateFleet(targetPlanet);
            fleet.AddChild(copy);
        }

        return capitalShips;
    }

    /// <summary>
    /// Retrieves all headquarters planets from the given planet systems.
    /// </summary>
    /// <param name="planetSystems">Array of planet systems to search.</param>
    /// <returns>A dictionary of headquarters planets, keyed by their InstanceID.</returns>
    private Dictionary<string, Planet> GetHeadquarters(PlanetSystem[] planetSystems)
    {
        return planetSystems
            .SelectMany(ps => ps.Planets)
            .Where(p => p.IsHeadquarters)
            .ToDictionary(p => p.InstanceID);
    }

    /// <summary>
    /// Retrieves all non-headquarters planets grouped by faction.
    /// </summary>
    /// <param name="planetSystems">Array of planet systems to search.</param>
    /// <returns>A dictionary of planet arrays, keyed by faction (owner) ID.</returns>
    private Dictionary<string, Planet[]> GetFactionPlanets(PlanetSystem[] planetSystems)
    {
        return planetSystems
            .SelectMany(ps => ps.Planets)
            .Where(p => !string.IsNullOrWhiteSpace(p.GetOwnerInstanceID()) && !p.IsHeadquarters)
            .GroupBy(p => p.GetOwnerInstanceID())
            .ToDictionary(g => g.Key, g => g.ToArray());
    }

    /// <summary>
    /// Determines the target planet for a capital ship based on its configuration.
    /// </summary>
    /// <param name="ship">The capital ship to assign.</param>
    /// <param name="hqs">Dictionary of headquarters planets.</param>
    /// <param name="factionPlanets">Dictionary of faction planets.</param>
    /// <returns>The target Planet for the capital ship.</returns>
    private Planet GetTargetPlanet(
        CapitalShip ship,
        Dictionary<string, Planet> hqs,
        Dictionary<string, Planet[]> factionPlanets
    )
    {
        return ship.InitialParentInstanceID != null
            ? hqs[ship.InitialParentInstanceID]
            : factionPlanets[ship.OwnerInstanceID].Shuffle().First();
    }

    /// <summary>
    /// Retrieves an existing fleet from a planet or creates a new one if none exists.
    /// </summary>
    /// <param name="planet">The planet to check or create a fleet for.</param>
    /// <returns>An existing or newly created Fleet.</returns>
    private Fleet GetOrCreateFleet(Planet planet)
    {
        Fleet fleet = planet.GetFleets().FirstOrDefault();
        if (fleet == null)
        {
            fleet = new Fleet
            {
                DisplayName = $"{planet.GetDisplayName()} Fleet",
                OwnerInstanceID = planet.OwnerInstanceID,
            };
            planet.AddChild(fleet);
        }
        return fleet;
    }

    /// <summary>
    /// Filters the capital ships based on the game's starting research level.
    /// </summary>
    /// <param name="capitalShips">The list of available capital ships.</param>
    /// <returns>An array of CapitalShip objects that meet the research level criteria.</returns>
    public override CapitalShip[] SelectUnits(CapitalShip[] capitalShips)
    {
        int startingResearchLevel = GetGameSummary().StartingResearchLevel;
        return capitalShips
            .Where(ship => ship.RequiredResearchLevel <= startingResearchLevel)
            .ToArray();
    }

    /// <summary>
    /// Optionally decorates the capital ships with additional properties or behaviors.
    /// </summary>
    /// <param name="capitalShips">The list of capital ships to decorate.</param>
    /// <returns>The same list of CapitalShip objects.</returns>
    public override CapitalShip[] DecorateUnits(CapitalShip[] capitalShips)
    {
        // No additional decoration is needed for now.
        return capitalShips;
    }

    /// <summary>
    /// Deploys capital ships to their designated locations in the galaxy.
    /// </summary>
    /// <param name="units">The list of capital ships to deploy.</param>
    /// <param name="destinations">The planetary systems in the galaxy.</param>
    /// <returns>The list of deployed CapitalShip objects.</returns>
    public override CapitalShip[] DeployUnits(CapitalShip[] units, PlanetSystem[] destinations)
    {
        IConfig[] capitalShipConfigs = GetCapitalShipConfigs();
        CapitalShip[] shipsToDeploy = GetCapitalShipsToDeploy(units, capitalShipConfigs);
        return AssignUnits(shipsToDeploy, destinations);
    }
}
