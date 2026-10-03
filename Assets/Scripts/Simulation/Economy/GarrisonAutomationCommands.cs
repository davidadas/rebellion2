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
    /// Queues advisor-managed garrison regiment orders.
    /// </summary>
    public sealed class GarrisonAutomationCommands
    {
        private const int _automatedOrderQuantity = 1;
        private const int _minimumGarrisonTarget = 1;
        private const int _resourceFacilitiesPerGarrison = 2;
        private const int _tieBreakRollMinimum = 0;
        private const int _tieBreakRollMaximum = 10;
        private const int _replaceSelectionRollThreshold = 5;

        private readonly GameRoot _game;
        private readonly GameDataCatalog _gameData;
        private readonly ManufacturingCommands _manufacturing;

        /// <summary>
        /// Creates advisor-managed garrison commands for the active game.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="gameData">Templates available to the selected content pack.</param>
        /// <param name="manufacturing">The manufacturing commands used to place orders.</param>
        public GarrisonAutomationCommands(
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
        /// Selects and queues the next advisor-managed regiment order for a faction.
        /// </summary>
        /// <param name="faction">The faction delegating garrison management.</param>
        /// <returns>True when an order was queued.</returns>
        public bool TryQueueRegiment(Faction faction)
        {
            if (faction == null)
                throw new ArgumentNullException(nameof(faction));

            List<Planet> controlledPlanets = GetControlledPlanets(faction);
            Planet destination = FindDestination(controlledPlanets, faction);
            Regiment template =
                destination == null
                    ? null
                    : FindRegimentTemplate(faction, destination.IsInUprising);
            Planet producer =
                destination == null ? null : FindProducer(controlledPlanets, destination);
            if (
                destination == null
                || template == null
                || producer == null
                || template.MaintenanceCost > faction.ProjectedMaintenanceHeadroom
            )
                return false;

            return _manufacturing.StartManufacturing(
                producer,
                template,
                destination,
                _automatedOrderQuantity,
                faction.InstanceID
            );
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
        /// <returns>The selected destination, or null.</returns>
        private Planet FindDestination(IReadOnlyList<Planet> controlledPlanets, Faction faction)
        {
            List<Planet> shortages = new List<Planet>();
            Dictionary<Planet, int> deficits = new Dictionary<Planet, int>();
            foreach (Planet planet in controlledPlanets)
            {
                if (planet.IsBlockaded() || planet.GetParentOfType<PlanetSector>() == null)
                    continue;

                int deficit =
                    CalculateTargetRegimentCount(planet, faction)
                    - CountStationaryRegiments(planet, faction);
                if (deficit <= 0)
                    continue;

                shortages.Add(planet);
                deficits.Add(planet, deficit);
            }

            List<Planet> uprisingShortages = shortages
                .Where(planet => planet.IsInUprising)
                .ToList();
            if (uprisingShortages.Count > 0)
            {
                PlanetSector uprisingSector = SelectRanked(
                    uprisingShortages
                        .Select(planet => planet.GetParentOfType<PlanetSector>())
                        .Distinct(),
                    sector =>
                        uprisingShortages
                            .Where(planet => planet.GetParentOfType<PlanetSector>() == sector)
                            .Sum(planet => deficits[planet]),
                    preferGreater: true
                );
                return SelectRanked(
                    uprisingShortages.Where(planet =>
                        planet.GetParentOfType<PlanetSector>() == uprisingSector
                    ),
                    planet => deficits[planet],
                    preferGreater: false
                );
            }

            PlanetSector normalSector = SelectRanked(
                shortages.Select(planet => planet.GetParentOfType<PlanetSector>()).Distinct(),
                sector =>
                    controlledPlanets.Count(planet =>
                        planet.GetParentOfType<PlanetSector>() == sector
                    ),
                preferGreater: false
            );
            return SelectRanked(
                shortages.Where(planet => planet.GetParentOfType<PlanetSector>() == normalSector),
                planet => deficits[planet],
                preferGreater: true
            );
        }

        /// <summary>
        /// Calculates the desired regiment count for one controlled planet.
        /// </summary>
        /// <param name="planet">The planet to protect.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <returns>The desired regiment count.</returns>
        private int CalculateTargetRegimentCount(Planet planet, Faction faction)
        {
            GameConfig.GarrisonConfig config = _game.Config.AI.Garrison;
            int support = planet.GetPopularSupport(faction.InstanceID);
            int supportTarget = _minimumGarrisonTarget;
            if (support < config.SupportThreshold)
            {
                supportTarget =
                    (int)
                        Math.Ceiling(
                            (config.SupportThreshold - support) / (double)config.GarrisonDivisor
                        ) + _minimumGarrisonTarget;
            }

            if (planet.IsInUprising)
                supportTarget *= config.UprisingMultiplier;

            int resourceFacilities =
                planet.GetTotalBuildingTypeCount(BuildingType.Mine)
                + planet.GetTotalBuildingTypeCount(BuildingType.Refinery);
            int facilityTarget =
                resourceFacilities / _resourceFacilitiesPerGarrison
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
        private static int CountStationaryRegiments(Planet planet, Faction faction)
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
        private Planet FindProducer(IEnumerable<Planet> controlledPlanets, Planet destination)
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
                if (
                    isBetter
                    || (
                        comparison == 0
                        && _game.Random.NextInt(_tieBreakRollMinimum, _tieBreakRollMaximum)
                            >= _replaceSelectionRollThreshold
                    )
                )
                {
                    selected = candidate;
                    selectedRank = rank;
                }
            }

            return selected;
        }

        /// <summary>
        /// Selects an unlocked defensive regiment for the destination state.
        /// </summary>
        /// <param name="faction">The faction placing the order.</param>
        /// <param name="isUprising">Whether the destination is in uprising.</param>
        /// <returns>The selected regiment template, or null.</returns>
        private Regiment FindRegimentTemplate(Faction faction, bool isUprising)
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
    }
}
