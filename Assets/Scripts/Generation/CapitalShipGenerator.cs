using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Util.Extensions;

namespace Rebellion.Generation
{
    /// <summary>
    /// Responsible for generating and deploying Capital Ships into the scene graph
    /// based on strongly-typed generation rules.
    /// </summary>
    public class CapitalShipGenerator : UnitGenerator<CapitalShip>
    {
        /// <summary>
        /// Constructs a CapitalShipGenerator.
        /// </summary>
        /// <param name="summary">The GameSummary containing player-selected options.</param>
        /// <param name="resourceManager">The resource manager used to load entity data.</param>
        /// <param name="randomProvider">Random number provider for deterministic generation.</param>
        public CapitalShipGenerator(
            GameSummary summary,
            IResourceManager resourceManager,
            IRandomNumberProvider randomProvider
        )
            : base(summary, resourceManager, randomProvider) { }

        /// <summary>
        /// Retrieves the capital ship generation rules corresponding to the selected galaxy size.
        /// </summary>
        /// <returns>An array of InitialCapitalShipOption describing ships to spawn.</returns>
        private InitialCapitalShipOption[] GetCapitalShipConfigs()
        {
            GameSize galaxySize = GetGameSummary().GalaxySize;
            GameGenerationRules rules = GetRules();

            return galaxySize switch
            {
                GameSize.Small => rules.CapitalShips.InitialCapitalShips.Small.ToArray(),
                GameSize.Medium => rules.CapitalShips.InitialCapitalShips.Medium.ToArray(),
                GameSize.Large => rules.CapitalShips.InitialCapitalShips.Large.ToArray(),
                _ => throw new InvalidOperationException("Invalid galaxy size."),
            };
        }

        /// <summary>
        /// Maps generation rule entries to concrete CapitalShip instances,
        /// assigning ownership and optional starting parent location.
        /// </summary>
        /// <param name="capitalShips">Available capital ship definitions.</param>
        /// <param name="capitalShipConfigs">Generation rule entries for this galaxy size.</param>
        /// <returns>Capital ships configured for deployment.</returns>
        private CapitalShip[] GetCapitalShipsToDeploy(
            CapitalShip[] capitalShips,
            InitialCapitalShipOption[] capitalShipConfigs
        )
        {
            return capitalShipConfigs
                .Select(config =>
                {
                    CapitalShip ship = capitalShips.First(s => s.TypeID == config.TypeID);

                    ship.InitialParentInstanceID = config.InitialParentInstanceID;
                    ship.OwnerInstanceID = config.OwnerInstanceID;

                    return ship;
                })
                .ToArray();
        }

        /// <summary>
        /// Assigns capital ships to planets or fleets within the galaxy.
        /// Ships are deployed either to a specific headquarters planet
        /// or to a random owned planet if no explicit parent is provided.
        /// </summary>
        /// <param name="capitalShips">Configured capital ships.</param>
        /// <param name="planetSystems">All planet systems in the generated galaxy.</param>
        /// <returns>The original ship definitions (deployment occurs via scene graph mutation).</returns>
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
        /// Retrieves all headquarters planets across all systems.
        /// </summary>
        private Dictionary<string, Planet> GetHeadquarters(PlanetSystem[] planetSystems)
        {
            return planetSystems
                .SelectMany(ps => ps.Planets)
                .Where(p => p.IsHeadquarters)
                .ToDictionary(p => p.InstanceID);
        }

        /// <summary>
        /// Retrieves all non-headquarters planets grouped by owning faction.
        /// </summary>
        private Dictionary<string, Planet[]> GetFactionPlanets(PlanetSystem[] planetSystems)
        {
            return planetSystems
                .SelectMany(ps => ps.Planets)
                .Where(p => !string.IsNullOrWhiteSpace(p.GetOwnerInstanceID()) && !p.IsHeadquarters)
                .GroupBy(p => p.GetOwnerInstanceID())
                .ToDictionary(g => g.Key, g => g.ToArray());
        }

        /// <summary>
        /// Determines the target planet for a capital ship.
        /// If an explicit InitialParentInstanceID exists, deploy to that HQ.
        /// Otherwise, deploy to a random owned planet.
        /// </summary>
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
        /// Retrieves an existing fleet from a planet or creates one if none exists.
        /// </summary>
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
        /// Filters capital ships based on the starting research level
        /// selected in the GameSummary.
        /// </summary>
        public override CapitalShip[] SelectUnits(CapitalShip[] capitalShips)
        {
            int startingResearchLevel = GetGameSummary().StartingResearchLevel;

            return capitalShips
                .Where(ship => ship.RequiredResearchLevel <= startingResearchLevel)
                .ToArray();
        }

        /// <summary>
        /// Optional hook to modify ships prior to deployment.
        /// No additional decoration is required currently.
        /// </summary>
        public override CapitalShip[] DecorateUnits(CapitalShip[] capitalShips)
        {
            return capitalShips;
        }

        /// <summary>
        /// Deploys capital ships into the generated galaxy
        /// according to generation rules.
        /// </summary>
        public override CapitalShip[] DeployUnits(CapitalShip[] units, PlanetSystem[] destinations)
        {
            InitialCapitalShipOption[] configs = GetCapitalShipConfigs();
            CapitalShip[] shipsToDeploy = GetCapitalShipsToDeploy(units, configs);

            return AssignUnits(shipsToDeploy, destinations);
        }
    }
}
