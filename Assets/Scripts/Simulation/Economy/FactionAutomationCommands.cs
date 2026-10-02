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
        private const int _garrisonSupportTarget = 60;
        private const int _garrisonSupportStep = 10;

        private readonly GameRoot _game;
        private readonly GameDataCatalog _gameData;
        private readonly ManufacturingCommands _manufacturing;

        /// <summary>
        /// Creates the delegated faction automation commands.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="gameData">Templates available to the selected content pack.</param>
        /// <param name="manufacturing">The manufacturing commands used to place orders.</param>
        public FactionAutomationCommands(
            GameRoot game,
            GameDataCatalog gameData,
            ManufacturingCommands manufacturing
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            _manufacturing =
                manufacturing ?? throw new ArgumentNullException(nameof(manufacturing));
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
                TryQueueGarrisonRegiment(faction);

            if (faction.ManageProduction)
                FillProductionManufacturingCapacity(faction);
        }

        /// <summary>
        /// Queues one regiment for the faction's highest-priority garrison shortage.
        /// </summary>
        /// <param name="faction">The faction delegating garrison management.</param>
        /// <returns>True when an order was queued.</returns>
        private bool TryQueueGarrisonRegiment(Faction faction)
        {
            List<Planet> controlledPlanets = GetControlledPlanets(faction);
            GarrisonCandidate destination = FindGarrisonDestination(controlledPlanets, faction);
            if (destination == null)
                return false;

            Regiment template = GetAvailableRegiment(faction, destination.Planet.IsInUprising);
            Planet producer = FindGarrisonProducer(controlledPlanets, destination.Planet);
            if (
                template == null
                || producer == null
                || template.MaintenanceCost > faction.ProjectedMaintenanceHeadroom
            )
            {
                return false;
            }

            return _manufacturing.StartManufacturing(
                producer,
                template,
                destination.Planet,
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
        /// Returns every planet controlled by the faction in scene order.
        /// </summary>
        /// <param name="faction">The faction whose planets are requested.</param>
        /// <returns>The controlled planets.</returns>
        private List<Planet> GetControlledPlanets(Faction faction)
        {
            return _game
                .GetSceneNodesByType<Planet>()
                .Where(planet =>
                    string.Equals(
                        planet.GetOwnerInstanceID(),
                        faction.InstanceID,
                        StringComparison.Ordinal
                    )
                )
                .ToList();
        }

        /// <summary>
        /// Selects the highest-priority planet with a garrison shortage.
        /// </summary>
        /// <param name="controlledPlanets">The faction's controlled planets.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <returns>The selected shortage, or null.</returns>
        private GarrisonCandidate FindGarrisonDestination(
            IReadOnlyList<Planet> controlledPlanets,
            Faction faction
        )
        {
            List<GarrisonCandidate> shortages = controlledPlanets
                .Where(planet => !planet.IsBlockaded())
                .Select(planet => new GarrisonCandidate(
                    planet,
                    GetGarrisonTarget(planet, faction),
                    CountFactionRegiments(planet, faction)
                ))
                .Where(candidate => candidate.Deficit > 0 && candidate.Sector != null)
                .ToList();
            List<GarrisonCandidate> uprisingShortages = shortages
                .Where(candidate => candidate.Planet.IsInUprising)
                .ToList();
            if (uprisingShortages.Count > 0)
            {
                PlanetSector uprisingSector = SelectRanked(
                    uprisingShortages.Select(candidate => candidate.Sector).Distinct(),
                    sector =>
                        uprisingShortages
                            .Where(candidate => candidate.Sector == sector)
                            .Sum(candidate => candidate.Deficit),
                    preferGreater: true
                );
                return SelectRanked(
                    uprisingShortages.Where(candidate => candidate.Sector == uprisingSector),
                    candidate => candidate.Deficit,
                    preferGreater: false
                );
            }

            PlanetSector normalSector = SelectRanked(
                shortages.Select(candidate => candidate.Sector).Distinct(),
                sector =>
                    controlledPlanets.Count(planet =>
                        planet.GetParentOfType<PlanetSector>() == sector
                    ),
                preferGreater: false
            );
            return SelectRanked(
                shortages.Where(candidate => candidate.Sector == normalSector),
                candidate => candidate.Deficit,
                preferGreater: true
            );
        }

        /// <summary>
        /// Calculates the desired regiment count for one controlled planet.
        /// </summary>
        /// <param name="planet">The planet to protect.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <returns>The desired regiment count.</returns>
        private static int GetGarrisonTarget(Planet planet, Faction faction)
        {
            int support = planet.GetPopularSupport(faction.InstanceID);
            int supportTarget = 1;
            if (support < _garrisonSupportTarget)
            {
                supportTarget =
                    (int)
                        Math.Ceiling(
                            (_garrisonSupportTarget - support) / (double)_garrisonSupportStep
                        ) + 1;
            }

            if (planet.IsInUprising)
                supportTarget *= 2;

            int resourceFacilities =
                planet.GetTotalBuildingTypeCount(BuildingType.Mine)
                + planet.GetTotalBuildingTypeCount(BuildingType.Refinery);
            int facilityTarget =
                resourceFacilities / 2
                + planet.GetTotalBuildingTypeCount(BuildingType.Shipyard)
                + planet.GetTotalBuildingTypeCount(BuildingType.TrainingFacility)
                + planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility);
            return Math.Max(supportTarget, facilityTarget);
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
        /// Selects the eligible troop producer closest to the destination's sector.
        /// </summary>
        /// <param name="controlledPlanets">The faction's controlled planets.</param>
        /// <param name="destination">The destination requiring a regiment.</param>
        /// <returns>The selected producer, or null.</returns>
        private Planet FindGarrisonProducer(
            IEnumerable<Planet> controlledPlanets,
            Planet destination
        )
        {
            PlanetSector destinationSector = destination.GetParentOfType<PlanetSector>();
            if (destinationSector == null)
                return null;

            List<Planet> eligibleProducers = controlledPlanets
                .Where(planet =>
                    !planet.IsBlockaded()
                    && !planet.IsInUprising
                    && !planet.IsManufacturingReserved(ManufacturingType.Troop)
                    && planet.GetIdleManufacturingFacilities(ManufacturingType.Troop) > 0
                    && planet.GetParentOfType<PlanetSector>() != null
                )
                .ToList();
            PlanetSector producerSector = SelectRanked(
                eligibleProducers
                    .Select(planet => planet.GetParentOfType<PlanetSector>())
                    .Distinct(),
                sector => GetSectorDistanceSquared(destinationSector, sector),
                preferGreater: false
            );
            return SelectRanked(
                eligibleProducers.Where(planet =>
                    planet.GetParentOfType<PlanetSector>() == producerSector
                ),
                planet => planet.GetPopularSupport(planet.GetOwnerInstanceID()),
                preferGreater: false
            );
        }

        /// <summary>
        /// Calculates squared distance between two sector coordinates.
        /// </summary>
        /// <param name="origin">The origin sector.</param>
        /// <param name="destination">The destination sector.</param>
        /// <returns>The squared coordinate distance.</returns>
        private static long GetSectorDistanceSquared(PlanetSector origin, PlanetSector destination)
        {
            long deltaX = origin.PositionX - destination.PositionX;
            long deltaY = origin.PositionY - destination.PositionY;
            return deltaX * deltaX + deltaY * deltaY;
        }

        /// <summary>
        /// Selects the strongest-ranked value and resolves equal ranks through game randomness.
        /// </summary>
        /// <typeparam name="T">The candidate type.</typeparam>
        /// <typeparam name="TRank">The comparable rank type.</typeparam>
        /// <param name="candidates">The candidates in scene order.</param>
        /// <param name="getRank">Returns a candidate's rank.</param>
        /// <param name="preferGreater">Whether larger ranks are preferred.</param>
        /// <returns>The selected candidate, or the default value.</returns>
        private T SelectRanked<T, TRank>(
            IEnumerable<T> candidates,
            Func<T, TRank> getRank,
            bool preferGreater
        )
            where T : class
            where TRank : IComparable<TRank>
        {
            T selected = null;
            TRank selectedRank = default;
            foreach (T candidate in candidates)
            {
                TRank rank = getRank(candidate);
                if (selected == null)
                {
                    selected = candidate;
                    selectedRank = rank;
                    continue;
                }

                int comparison = rank.CompareTo(selectedRank);
                bool isBetter = preferGreater ? comparison > 0 : comparison < 0;
                if (isBetter || (comparison == 0 && _game.Random.NextInt(0, 10) >= 5))
                {
                    selected = candidate;
                    selectedRank = rank;
                }
            }

            return selected;
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
        /// Selects an unlocked defensive regiment for the destination state.
        /// </summary>
        /// <param name="faction">The faction placing the order.</param>
        /// <param name="isUprising">Whether the destination is in uprising.</param>
        /// <returns>The selected regiment template, or null.</returns>
        private Regiment GetAvailableRegiment(Faction faction, bool isUprising)
        {
            int unlockedOrder = faction.GetHighestUnlockedOrder(ManufacturingType.Troop);
            IEnumerable<Regiment> candidates = _gameData.Regiments.Where(template =>
                IManufacturable.CanBeManufacturedBy(template, faction.InstanceID)
                && template.ResearchOrder <= unlockedOrder
                && template.DefenseRating >= template.AttackRating
            );
            return isUprising
                ? SelectRanked(
                    candidates,
                    template => template.MaintenanceCost,
                    preferGreater: false
                )
                : SelectRanked(candidates, template => template.DefenseRating, preferGreater: true);
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

        /// <summary>
        /// Describes one controlled planet's current garrison shortage.
        /// </summary>
        private sealed class GarrisonCandidate
        {
            public Planet Planet { get; }
            public PlanetSector Sector { get; }
            public int Deficit { get; }

            /// <summary>
            /// Creates a garrison-shortage candidate.
            /// </summary>
            /// <param name="planet">The controlled planet.</param>
            /// <param name="target">The desired regiment count.</param>
            /// <param name="current">The current stationary regiment count.</param>
            public GarrisonCandidate(Planet planet, int target, int current)
            {
                Planet = planet;
                Sector = planet.GetParentOfType<PlanetSector>();
                Deficit = target - current;
            }
        }
    }
}
