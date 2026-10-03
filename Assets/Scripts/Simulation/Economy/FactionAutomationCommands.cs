using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Performs the optional strategic chores delegated to the protocol advisor.
    /// </summary>
    public sealed class FactionAutomationCommands
    {
        private const int _automatedOrderQuantity = 1;

        private readonly GameRoot _game;
        private readonly GameDataCatalog _gameData;
        private readonly GarrisonAutomationCommands _garrisonAutomation;
        private readonly ManufacturingCommands _manufacturing;

        /// <summary>
        /// Creates the delegated faction automation commands.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="gameData">Templates available to the selected content pack.</param>
        /// <param name="manufacturing">The manufacturing commands used to place orders.</param>
        /// <param name="garrisonAutomation">Queues advisor-managed garrison regiment orders.</param>
        public FactionAutomationCommands(
            GameRoot game,
            GameDataCatalog gameData,
            ManufacturingCommands manufacturing,
            GarrisonAutomationCommands garrisonAutomation
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            _manufacturing =
                manufacturing ?? throw new ArgumentNullException(nameof(manufacturing));
            _garrisonAutomation =
                garrisonAutomation ?? throw new ArgumentNullException(nameof(garrisonAutomation));
        }

        /// <summary>
        /// Immediately fills idle capacity for one faction's delegated work.
        /// </summary>
        /// <param name="faction">The faction whose current automation choices should run.</param>
        public void ProcessFaction(Faction faction)
        {
            if (faction == null)
                throw new ArgumentNullException(nameof(faction));

            if (faction.ManageGarrisons)
                _garrisonAutomation.TryQueueRegiment(faction);

            if (faction.ManageProduction)
                FillProductionManufacturingCapacity(faction);
        }

        /// <summary>
        /// Fills the faction's currently available building-manufacturing capacity.
        /// </summary>
        /// <param name="faction">The faction delegating production management.</param>
        private void FillProductionManufacturingCapacity(Faction faction)
        {
            List<Planet> ownedPlanets = GetOwnedPlanets(faction);
            int availableCapacity = ownedPlanets
                .Where(planet => !planet.IsManufacturingReserved(ManufacturingType.Building))
                .Sum(planet =>
                    planet.GetAvailableManufacturingCapacity(ManufacturingType.Building)
                );

            for (int orderIndex = 0; orderIndex < availableCapacity; orderIndex++)
            {
                if (!TryQueueProductionFacility(faction, ownedPlanets))
                    break;
            }
        }

        /// <summary>
        /// Queues the next mine or refinery needed to expand paired production.
        /// </summary>
        /// <param name="faction">The faction delegating production management.</param>
        /// <param name="ownedPlanets">The faction's colonized planets.</param>
        /// <returns>True when an order was queued.</returns>
        private bool TryQueueProductionFacility(Faction faction, List<Planet> ownedPlanets)
        {
            int mineCount = CountBuildings(ownedPlanets, BuildingType.Mine);
            int refineryCount = CountBuildings(ownedPlanets, BuildingType.Refinery);
            BuildingType nextType =
                mineCount <= refineryCount ? BuildingType.Mine : BuildingType.Refinery;
            if (
                !TryFindResourceFacilityOrder(
                    ownedPlanets,
                    nextType,
                    out Planet producer,
                    out Planet destination
                )
            )
            {
                nextType =
                    nextType == BuildingType.Mine ? BuildingType.Refinery : BuildingType.Mine;
                if (
                    !HasActiveBuildingProject(ownedPlanets, nextType)
                    || !TryFindResourceFacilityOrder(
                        ownedPlanets,
                        nextType,
                        out producer,
                        out destination
                    )
                )
                {
                    return false;
                }
            }

            Building template = GetAvailableBuilding(faction, nextType);
            return template != null
                && _manufacturing.StartManufacturing(
                    producer,
                    template,
                    destination,
                    _automatedOrderQuantity,
                    faction.InstanceID
                );
        }

        /// <summary>
        /// Returns the faction's colonized planets in stable order.
        /// </summary>
        /// <param name="faction">The faction whose planets are requested.</param>
        /// <returns>The owned planets.</returns>
        private List<Planet> GetOwnedPlanets(Faction faction)
        {
            return _game
                .GetSceneNodesByType<Planet>()
                .Where(planet =>
                    planet.IsColonized
                    && string.Equals(
                        planet.GetOwnerInstanceID(),
                        faction.InstanceID,
                        StringComparison.Ordinal
                    )
                )
                .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Selects the closest valid resource slot to any idle construction yard.
        /// </summary>
        /// <param name="planets">Candidate planets.</param>
        /// <param name="buildingType">The resource facility being placed.</param>
        /// <param name="producer">The selected construction-yard planet.</param>
        /// <param name="destination">The selected resource-facility destination.</param>
        /// <returns>True when a valid order was found.</returns>
        private static bool TryFindResourceFacilityOrder(
            IEnumerable<Planet> planets,
            BuildingType buildingType,
            out Planet producer,
            out Planet destination
        )
        {
            List<Planet> producers = planets
                .Where(planet =>
                    !planet.IsManufacturingReserved(ManufacturingType.Building)
                    && planet.GetAvailableManufacturingCapacity(ManufacturingType.Building) > 0
                    && IsCompatibleBuildingProject(planet, buildingType)
                )
                .ToList();
            IEnumerable<Planet> destinations = planets.Where(planet =>
                planet.GetAvailableEnergy() > 0
            );
            if (buildingType == BuildingType.Mine)
                destinations = destinations.Where(planet =>
                    planet.GetUnminedResourceNodeCount() > 0
                );

            var order = producers
                .SelectMany(source =>
                    destinations.Select(target => new
                    {
                        Producer = source,
                        Destination = target,
                        Distance = source.GetRawDistanceTo(target),
                    })
                )
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Producer.InstanceID, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Destination.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
            producer = order?.Producer;
            destination = order?.Destination;
            return producer != null && destination != null;
        }

        /// <summary>
        /// Returns whether any available construction lane is already committed to a building type.
        /// </summary>
        /// <param name="planets">The faction's candidate producer planets.</param>
        /// <param name="buildingType">The building project to locate.</param>
        /// <returns>True when an active lane can accept another copy without replacement.</returns>
        private static bool HasActiveBuildingProject(
            IEnumerable<Planet> planets,
            BuildingType buildingType
        )
        {
            return planets.Any(planet =>
                planet.GetAvailableManufacturingCapacity(ManufacturingType.Building) > 0
                && planet
                    .GetManufacturingQueue()
                    .TryGetValue(ManufacturingType.Building, out List<IManufacturable> queue)
                && queue?.OfType<Building>().Any(building => building.BuildingType == buildingType)
                    == true
            );
        }

        /// <summary>
        /// Returns whether a construction lane is idle or already producing the requested project.
        /// </summary>
        /// <param name="producer">The planet containing the construction lane.</param>
        /// <param name="buildingType">The requested building project.</param>
        /// <returns>True when adding the project would not replace active automated work.</returns>
        private static bool IsCompatibleBuildingProject(Planet producer, BuildingType buildingType)
        {
            if (
                !producer
                    .GetManufacturingQueue()
                    .TryGetValue(ManufacturingType.Building, out List<IManufacturable> queue)
                || queue == null
                || queue.Count == 0
            )
            {
                return true;
            }

            return queue.OfType<Building>().All(building => building.BuildingType == buildingType);
        }

        /// <summary>
        /// Selects the most advanced unlocked resource-facility template.
        /// </summary>
        /// <param name="faction">The faction placing the order.</param>
        /// <param name="buildingType">The required resource-facility type.</param>
        /// <returns>The selected building template, or null.</returns>
        private Building GetAvailableBuilding(Faction faction, BuildingType buildingType)
        {
            int unlockedOrder = faction.GetHighestUnlockedOrder(ManufacturingType.Building);
            return _gameData
                .Buildings.Where(template =>
                    template.BuildingType == buildingType
                    && IManufacturable.CanBeManufacturedBy(template, faction.InstanceID)
                    && template.ResearchOrder <= unlockedOrder
                )
                .OrderByDescending(template => template.ResearchOrder)
                .ThenBy(template => template.TypeID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Counts existing and queued resource facilities across the supplied planets.
        /// </summary>
        /// <param name="planets">Planets to inspect.</param>
        /// <param name="buildingType">The facility type to count.</param>
        /// <returns>The total facility count.</returns>
        private static int CountBuildings(IEnumerable<Planet> planets, BuildingType buildingType)
        {
            return planets.Sum(planet => planet.GetTotalBuildingTypeCount(buildingType));
        }
    }
}
