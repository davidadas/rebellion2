using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Selects the Core-sector planets retained for manufacturing investment.
    /// </summary>
    internal sealed class AIProductionSitePolicy
    {
        private const int _preferredSiteCount = 3;

        private readonly Dictionary<string, IReadOnlyList<Planet>> _planetsBySystem = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, HashSet<string>> _preferredPlanetIdsBySystem = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Creates the production-site policy for one faction turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        internal AIProductionSitePolicy(AITurnContext context)
        {
            if (context?.Assessment == null)
                return;

            foreach (
                IGrouping<string, Planet> system in context
                    .Assessment.OwnedPlanets.Where(IsEligibleCorePlanet)
                    .GroupBy(context.Assessment.GetPlanetSystemId)
            )
            {
                List<Planet> planets = system.OrderBy(planet => planet.InstanceID).ToList();
                Dictionary<string, int> facilityCounts = planets.ToDictionary(
                    planet => planet.InstanceID,
                    planet => GetProductionFacilityCount(context, planet),
                    StringComparer.Ordinal
                );
                HashSet<string> preferredIds = planets
                    .OrderByDescending(planet => facilityCounts[planet.InstanceID])
                    .ThenByDescending(planet => planet.EnergyCapacity)
                    .ThenByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                    .Take(_preferredSiteCount)
                    .Select(planet => planet.InstanceID)
                    .ToHashSet(StringComparer.Ordinal);

                _planetsBySystem[system.Key] = planets;
                _preferredPlanetIdsBySystem[system.Key] = preferredIds;
            }
        }

        /// <summary>
        /// Returns indexed Core-sector identifiers.
        /// </summary>
        /// <returns>The indexed system identifiers.</returns>
        internal IEnumerable<string> GetSystemIds() => _planetsBySystem.Keys;

        /// <summary>
        /// Returns owned planets in an indexed Core sector.
        /// </summary>
        /// <param name="systemId">The system identifier.</param>
        /// <returns>The owned Core-sector planets.</returns>
        internal IReadOnlyList<Planet> GetPlanets(string systemId)
        {
            return systemId != null && _planetsBySystem.TryGetValue(systemId, out var planets)
                ? planets
                : Array.Empty<Planet>();
        }

        /// <summary>
        /// Returns whether a planet is retained for manufacturing investment.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet is one of its Core sector's preferred sites.</returns>
        internal bool IsPreferred(Planet planet)
        {
            string systemId = planet?.GetParentOfType<PlanetSector>()?.InstanceID;
            return systemId != null
                && _preferredPlanetIdsBySystem.TryGetValue(systemId, out var preferredIds)
                && preferredIds.Contains(planet.InstanceID);
        }

        /// <summary>
        /// Returns whether a planet participates in Core-sector production-site policy.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet is an owned, usable Core-sector world.</returns>
        private static bool IsEligibleCorePlanet(Planet planet)
        {
            return planet?.IsColonized == true
                && !planet.IsDestroyed
                && planet.GetParentOfType<PlanetSector>()?.SectorType == PlanetSectorType.Core;
        }

        /// <summary>
        /// Returns the completed and pending production facilities at a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The total construction, ship, and training facility count.</returns>
        private static int GetProductionFacilityCount(AITurnContext context, Planet planet)
        {
            return context
                .Assessment.GetPlanetBuildings(planet)
                .Count(building =>
                    building.GetBuildingType()
                        is BuildingType.ConstructionFacility
                            or BuildingType.Shipyard
                            or BuildingType.TrainingFacility
                );
        }
    }
}
