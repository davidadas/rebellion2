using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Generation;

namespace Rebellion.Systems
{
    /// <summary>
    /// Performs the optional strategic chores delegated to the protocol advisor.
    /// </summary>
    public sealed class FactionAutomationSystem
    {
        private readonly GameRoot _game;
        private readonly GameDataCatalog _gameData;
        private readonly ManufacturingSystem _manufacturing;

        /// <summary>
        /// Creates the faction automation system.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="gameData">Templates available to the selected content pack.</param>
        /// <param name="manufacturing">The manufacturing system used to place orders.</param>
        public FactionAutomationSystem(
            GameRoot game,
            GameDataCatalog gameData,
            ManufacturingSystem manufacturing
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            _manufacturing =
                manufacturing ?? throw new ArgumentNullException(nameof(manufacturing));
        }

        /// <summary>
        /// Fills currently idle manufacturing capacity with the advisor's delegated work.
        /// </summary>
        public void ProcessTick()
        {
            foreach (Faction faction in _game.GetFactions())
                ProcessFaction(faction);
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
                FillGarrisonManufacturingCapacity(faction);

            if (faction.ManageProduction)
                FillProductionManufacturingCapacity(faction);
        }

        /// <summary>
        /// Fills the faction's currently available troop-manufacturing capacity.
        /// </summary>
        /// <param name="faction">The faction delegating garrison management.</param>
        private void FillGarrisonManufacturingCapacity(Faction faction)
        {
            List<Planet> ownedPlanets = GetOwnedPlanets(faction);
            int availableCapacity = ownedPlanets.Sum(planet =>
                planet.GetAvailableManufacturingCapacity(ManufacturingType.Troop)
            );

            for (int orderIndex = 0; orderIndex < availableCapacity; orderIndex++)
            {
                if (!TryQueueGarrisonRegiment(faction, ownedPlanets))
                    break;
            }
        }

        /// <summary>
        /// Queues one regiment for the faction's highest-priority garrison shortage.
        /// </summary>
        /// <param name="faction">The faction delegating garrison management.</param>
        /// <param name="ownedPlanets">The faction's colonized planets.</param>
        /// <returns>True when an order was queued.</returns>
        private bool TryQueueGarrisonRegiment(Faction faction, List<Planet> ownedPlanets)
        {
            Planet destination = ownedPlanets
                .Select(planet => new
                {
                    Planet = planet,
                    Deficit = GetGarrisonTarget(planet, faction)
                        - CountFactionRegiments(planet, faction),
                })
                .Where(candidate => candidate.Deficit > 0)
                .OrderByDescending(candidate => candidate.Planet.IsInUprising)
                .ThenByDescending(candidate => candidate.Deficit)
                .ThenByDescending(candidate => HasManufacturingFacilities(candidate.Planet))
                .ThenBy(candidate => candidate.Planet.InstanceID, StringComparer.Ordinal)
                .Select(candidate => candidate.Planet)
                .FirstOrDefault();
            if (destination == null)
                return false;

            Regiment template = GetAvailableRegiment(faction);
            Planet producer = FindProducer(ownedPlanets, ManufacturingType.Troop);
            return template != null
                && producer != null
                && _manufacturing.StartManufacturing(
                    producer,
                    template,
                    destination,
                    1,
                    faction.InstanceID
                );
        }

        /// <summary>
        /// Fills the faction's currently available building-manufacturing capacity.
        /// </summary>
        /// <param name="faction">The faction delegating production management.</param>
        private void FillProductionManufacturingCapacity(Faction faction)
        {
            List<Planet> ownedPlanets = GetOwnedPlanets(faction);
            int availableCapacity = ownedPlanets.Sum(planet =>
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
                return false;

            Building template = GetAvailableBuilding(faction, nextType);
            return template != null
                && _manufacturing.StartManufacturing(
                    producer,
                    template,
                    destination,
                    1,
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
        /// Calculates the advisor's desired garrison for one planet.
        /// </summary>
        /// <param name="planet">The planet to protect.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <returns>The desired regiment count.</returns>
        private int GetGarrisonTarget(Planet planet, Faction faction)
        {
            int required = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                faction,
                _game.Config.AI.Garrison
            );
            int manufacturingDefense = HasManufacturingFacilities(planet) ? 1 : 0;
            return Math.Max(1, required) + manufacturingDefense;
        }

        /// <summary>
        /// Counts stationary regiments owned by the controlling faction, including pending orders.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <returns>The regiment count.</returns>
        private static int CountFactionRegiments(Planet planet, Faction faction)
        {
            return planet
                .GetAllRegiments()
                .Count(regiment =>
                    string.Equals(
                        regiment.GetOwnerInstanceID(),
                        faction.InstanceID,
                        StringComparison.Ordinal
                    )
                    && regiment.Movement == null
                );
        }

        /// <summary>
        /// Returns whether a planet contains strategically important manufacturing capacity.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when a manufacturing facility is present.</returns>
        private static bool HasManufacturingFacilities(Planet planet)
        {
            return planet.Buildings.Any(building =>
                building.BuildingType
                    is BuildingType.ConstructionFacility
                        or BuildingType.Shipyard
                        or BuildingType.TrainingFacility
            );
        }

        /// <summary>
        /// Selects an owned planet with free production capacity.
        /// </summary>
        /// <param name="planets">Candidate planets.</param>
        /// <param name="manufacturingType">The required production type.</param>
        /// <returns>The selected producer, or null.</returns>
        private static Planet FindProducer(
            IEnumerable<Planet> planets,
            ManufacturingType manufacturingType
        )
        {
            return planets
                .Where(planet => planet.GetAvailableManufacturingCapacity(manufacturingType) > 0)
                .OrderByDescending(planet =>
                    planet.GetAvailableManufacturingCapacity(manufacturingType)
                )
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
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
                    planet.GetAvailableManufacturingCapacity(ManufacturingType.Building) > 0
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
        /// Selects the scenario's configured standard garrison regiment.
        /// </summary>
        /// <param name="faction">The faction placing the order.</param>
        /// <returns>The selected regiment template, or null.</returns>
        private Regiment GetAvailableRegiment(Faction faction)
        {
            FactionSetup factionSetup =
                _gameData.GenerationConfig.GalaxyClassification.FactionSetups.FirstOrDefault(
                    setup =>
                        string.Equals(setup.FactionID, faction.InstanceID, StringComparison.Ordinal)
                );
            if (string.IsNullOrEmpty(factionSetup?.GarrisonTroopTypeID))
                return null;

            int unlockedOrder = faction.GetHighestUnlockedOrder(ManufacturingType.Troop);
            return _gameData.Regiments.FirstOrDefault(template =>
                string.Equals(
                    template.TypeID,
                    factionSetup.GarrisonTroopTypeID,
                    StringComparison.Ordinal
                )
                && IManufacturable.CanBeManufacturedBy(template, faction.InstanceID)
                && template.ResearchOrder <= unlockedOrder
            );
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
