using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Selects production-facility destinations from strategic and local infrastructure value.
    /// </summary>
    public sealed class AIInfrastructurePlacementScorer
    {
        private readonly AITurnContext _context;
        private readonly Dictionary<string, int> _ownedPlanetCountsBySystem = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _knownPlanetCountsBySystem = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _facilityCountsBySystem = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _unminedResourceCountsByPlanet = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Creates a placement scorer and its turn-scoped system index.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public AIInfrastructurePlacementScorer(AITurnContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            foreach (Planet planet in context.Assessment.FactionViewPlanets)
                Increment(_knownPlanetCountsBySystem, context.Assessment.GetPlanetSystemId(planet));

            foreach (Planet planet in context.Assessment.OwnedPlanets)
            {
                string systemId = context.Assessment.GetPlanetSystemId(planet);
                Increment(_ownedPlanetCountsBySystem, systemId);
                IndexInfrastructure(planet, systemId);
            }
        }

        /// <summary>
        /// Returns the strongest destination for a production facility.
        /// </summary>
        /// <param name="candidates">Eligible destination planets.</param>
        /// <param name="demandPlanet">The planet whose demand prompted the expansion.</param>
        /// <param name="manufacturingType">The manufacturing category being expanded.</param>
        /// <param name="availableEnergy">Returns energy available after defensive reserves.</param>
        /// <returns>The preferred destination, or null when no candidate is available.</returns>
        public Planet SelectDestination(
            IReadOnlyList<Planet> candidates,
            Planet demandPlanet,
            ManufacturingType manufacturingType,
            Func<Planet, int> availableEnergy
        )
        {
            return RankDestinations(candidates, demandPlanet, manufacturingType, availableEnergy)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns production-facility destinations in descending strategic-value order.
        /// </summary>
        /// <param name="candidates">Eligible destination planets.</param>
        /// <param name="demandPlanet">The planet whose demand prompted the expansion.</param>
        /// <param name="manufacturingType">The manufacturing category being expanded.</param>
        /// <param name="availableEnergy">Returns energy available after defensive reserves.</param>
        /// <returns>The ranked destinations.</returns>
        public IReadOnlyList<Planet> RankDestinations(
            IReadOnlyList<Planet> candidates,
            Planet demandPlanet,
            ManufacturingType manufacturingType,
            Func<Planet, int> availableEnergy
        )
        {
            if (candidates == null || candidates.Count == 0)
                return Array.Empty<Planet>();

            double highestProductionRate = 0;
            double highestPlanetValue = 0;
            int highestAvailableEnergy = 0;
            int highestUnminedResourceCount = 0;
            double greatestDistance = 0;
            Dictionary<string, int> availableEnergyByPlanet = new(StringComparer.Ordinal);
            foreach (Planet candidate in candidates)
            {
                int candidateAvailableEnergy = availableEnergy(candidate);
                availableEnergyByPlanet[candidate.InstanceID] = candidateAvailableEnergy;
                highestProductionRate = Math.Max(
                    highestProductionRate,
                    _context.Assessment.GetPlanetProductionRate(candidate, manufacturingType)
                );
                highestPlanetValue = Math.Max(
                    highestPlanetValue,
                    _context.Assessment.GetPlanetValue(candidate)
                );
                highestAvailableEnergy = Math.Max(highestAvailableEnergy, candidateAvailableEnergy);
                highestUnminedResourceCount = Math.Max(
                    highestUnminedResourceCount,
                    GetUnminedResourceCount(candidate)
                );
                if (demandPlanet != null)
                    greatestDistance = Math.Max(
                        greatestDistance,
                        demandPlanet.GetRawDistanceTo(candidate)
                    );
            }

            return candidates
                .Select(candidate => new
                {
                    Planet = candidate,
                    Score = Score(
                        candidate,
                        demandPlanet,
                        manufacturingType,
                        availableEnergyByPlanet[candidate.InstanceID],
                        highestProductionRate,
                        highestPlanetValue,
                        highestAvailableEnergy,
                        highestUnminedResourceCount,
                        greatestDistance
                    ),
                })
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Planet.InstanceID, StringComparer.Ordinal)
                .Select(candidate => candidate.Planet)
                .ToList();
        }

        /// <summary>
        /// Returns the weighted strategic value of one facility destination.
        /// </summary>
        /// <param name="planet">The candidate planet.</param>
        /// <param name="demandPlanet">The planet whose demand prompted expansion.</param>
        /// <param name="manufacturingType">The manufacturing category being expanded.</param>
        /// <param name="availableEnergy">Energy available after defensive reserves.</param>
        /// <param name="highestProductionRate">The strongest candidate production rate.</param>
        /// <param name="highestPlanetValue">The strongest candidate strategic value.</param>
        /// <param name="highestAvailableEnergy">The greatest candidate available energy.</param>
        /// <param name="highestUnminedResourceCount">The greatest candidate unmined resource count.</param>
        /// <param name="greatestDistance">The greatest distance from the demand destination.</param>
        /// <returns>The weighted placement score.</returns>
        private double Score(
            Planet planet,
            Planet demandPlanet,
            ManufacturingType manufacturingType,
            int availableEnergy,
            double highestProductionRate,
            double highestPlanetValue,
            int highestAvailableEnergy,
            int highestUnminedResourceCount,
            double greatestDistance
        )
        {
            GameConfig.AIInfrastructureConfig config = _context.Game.Config.AI.Infrastructure;
            string systemId = _context.Assessment.GetPlanetSystemId(planet);
            int systemFacilityCount = _facilityCountsBySystem.TryGetValue(systemId, out int count)
                ? count
                : 0;
            double systemControl =
                _ownedPlanetCountsBySystem.TryGetValue(systemId, out int ownedCount)
                && _knownPlanetCountsBySystem.TryGetValue(systemId, out int knownCount)
                    ? (double)ownedCount / Math.Max(1, knownCount)
                    : 0;
            double proximity =
                demandPlanet == null || greatestDistance <= 0
                    ? 1
                    : 1 - demandPlanet.GetRawDistanceTo(planet) / greatestDistance;
            double score = systemFacilityCount == 0 ? config.FacilitySystemCoverageWeight : 0;
            int hubWeight =
                manufacturingType == ManufacturingType.Building
                    ? config.ConstructionFacilityHubWeight
                    : config.FacilityExistingHubWeight;
            score +=
                hubWeight
                * Normalize(
                    _context.Assessment.GetPlanetProductionRate(planet, manufacturingType),
                    highestProductionRate
                );
            score +=
                config.FacilityAvailableEnergyWeight
                * Normalize(availableEnergy, highestAvailableEnergy);
            score +=
                config.FacilityPlanetValueWeight
                * Normalize(_context.Assessment.GetPlanetValue(planet), highestPlanetValue);
            score += config.FacilitySystemSecurityWeight * systemControl;
            score += config.FacilityDemandProximityWeight * proximity;

            if (manufacturingType != ManufacturingType.Building)
                score -=
                    config.FacilityResourceOpportunityCostWeight
                    * Normalize(GetUnminedResourceCount(planet), highestUnminedResourceCount);

            return score;
        }

        /// <summary>
        /// Returns a value relative to the strongest candidate value.
        /// </summary>
        /// <param name="value">The candidate value.</param>
        /// <param name="maximum">The strongest candidate value.</param>
        /// <returns>A value from zero through one.</returns>
        private static double Normalize(double value, double maximum) =>
            maximum <= 0 ? 0 : value / maximum;

        /// <summary>
        /// Adds one planet to a system count.
        /// </summary>
        /// <param name="counts">System counts to update.</param>
        /// <param name="systemId">The containing system identifier.</param>
        private static void Increment(IDictionary<string, int> counts, string systemId)
        {
            counts[systemId] = counts.TryGetValue(systemId, out int count) ? count + 1 : 1;
        }

        /// <summary>
        /// Adds a planet's projected facilities and remaining resource capacity to the turn index.
        /// </summary>
        /// <param name="planet">The owned planet to index.</param>
        /// <param name="systemId">The containing system identifier.</param>
        private void IndexInfrastructure(Planet planet, string systemId)
        {
            int facilityCount = 0;
            int mineCount = 0;
            foreach (Building building in _context.Assessment.GetPlanetBuildings(planet))
            {
                switch (building.GetBuildingType())
                {
                    case BuildingType.ConstructionFacility:
                    case BuildingType.Shipyard:
                    case BuildingType.TrainingFacility:
                        facilityCount++;
                        break;
                    case BuildingType.Mine:
                        mineCount++;
                        break;
                }
            }

            if (facilityCount > 0)
            {
                _facilityCountsBySystem[systemId] = _facilityCountsBySystem.TryGetValue(
                    systemId,
                    out int existingCount
                )
                    ? existingCount + facilityCount
                    : facilityCount;
            }

            _unminedResourceCountsByPlanet[planet.InstanceID] = Math.Max(
                0,
                planet.GetRawResourceNodes() - mineCount
            );
        }

        /// <summary>
        /// Returns the indexed unmined resource capacity for a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The unmined resource node count.</returns>
        private int GetUnminedResourceCount(Planet planet)
        {
            return
                planet != null
                && _unminedResourceCountsByPlanet.TryGetValue(planet.InstanceID, out int count)
                ? count
                : 0;
        }
    }
}
