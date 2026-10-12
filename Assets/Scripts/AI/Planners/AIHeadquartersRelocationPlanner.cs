using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Plans mobile-headquarters evacuation from a known superior incoming fleet.
    /// </summary>
    public sealed class AIHeadquartersRelocationPlanner : IAIProposalPlanner
    {
        private readonly AIPlanetBuildingSpacePolicy _spacePolicy = new();

        /// <summary>
        /// Builds a relocation proposal when the current headquarters cannot defeat a known
        /// incoming hostile fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Zero or one headquarters-relocation proposal.</returns>
        public List<AIProposal> Plan(AITurnContext context)
        {
            List<AIProposal> proposals = new();
            if (
                context?.Assessment == null
                || context.Faction?.Settings?.Headquarters?.IsMobile != true
                || context.Headquarters == null
                || context.HeadquartersQueries == null
                || context.Maintenance == null
                || context.Manufacturing == null
            )
                return proposals;

            Planet origin = context.Assessment.FactionHeadquarters;
            Building headquarters = FindHeadquarters(context, origin);
            if (headquarters == null)
                return proposals;

            int threatStrength = context.Assessment.GetKnownIncomingHostileFleetStrength(origin);
            if (threatStrength <= 0)
                return proposals;

            int defenseStrength = context.Assessment.GetHeadquartersOrbitalDefenseStrength(origin);
            if (threatStrength <= defenseStrength)
                return proposals;

            RelocationDestination relocation = SelectDestination(context, headquarters, origin);
            if (relocation.Planet == null)
                return proposals;

            proposals.Add(
                new AIHeadquartersRelocationProposal(
                    headquarters,
                    origin,
                    relocation.Planet,
                    relocation.BuildingToRemove,
                    threatStrength,
                    defenseStrength
                )
            );
            return proposals;
        }

        /// <summary>
        /// Finds the faction's completed mobile-headquarters building at its active planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="origin">The active headquarters planet.</param>
        /// <returns>The headquarters building, or null when it is unavailable.</returns>
        private static Building FindHeadquarters(AITurnContext context, Planet origin)
        {
            if (origin == null)
                return null;

            return context
                .Assessment.GetPlanetBuildings(origin)
                .FirstOrDefault(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == BuildingType.Headquarters
                    && building.ManufacturingStatus == ManufacturingStatus.Complete
                    && building.Movement == null
                );
        }

        /// <summary>
        /// Chooses the safest usable owned planet, preferring free capacity when safety is equal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="headquarters">The headquarters building to move.</param>
        /// <param name="origin">The current headquarters planet.</param>
        /// <returns>The selected destination and any building that must be removed.</returns>
        private RelocationDestination SelectDestination(
            AITurnContext context,
            Building headquarters,
            Planet origin
        )
        {
            Dictionary<string, int> knownEnemyPlanetsBySystem = context
                .Assessment.EnemyPlanets.GroupBy(context.Assessment.GetPlanetSystemId)
                .Where(group => !string.IsNullOrEmpty(group.Key))
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            IEnumerable<Planet> candidates = context
                .Assessment.OwnedPlanets.Where(planet =>
                    planet != null
                    && planet != origin
                    && planet.IsColonized
                    && !planet.IsDestroyed
                    && !context.Assessment.HasKnownHostileFleet(planet)
                    && !MovementQueries.IsBlockedFromDestinationByBlockade(headquarters, planet)
                )
                .OrderBy(planet =>
                {
                    knownEnemyPlanetsBySystem.TryGetValue(
                        context.Assessment.GetPlanetSystemId(planet),
                        out int count
                    );
                    return count;
                })
                .ThenByDescending(planet => planet.GetAvailableEnergy() > 0)
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal);

            foreach (Planet candidate in candidates)
            {
                if (context.HeadquartersQueries.CanRelocate(headquarters, candidate))
                    return new RelocationDestination(candidate, null);

                Building buildingToRemove = _spacePolicy.SelectBuildingToRemove(context, candidate);
                if (buildingToRemove != null)
                    return new RelocationDestination(candidate, buildingToRemove);
            }

            return default;
        }

        private readonly struct RelocationDestination
        {
            internal Planet Planet { get; }

            internal Building BuildingToRemove { get; }

            /// <summary>
            /// Creates a resolved headquarters destination.
            /// </summary>
            /// <param name="planet">The destination planet.</param>
            /// <param name="buildingToRemove">The building that must be removed, if any.</param>
            internal RelocationDestination(Planet planet, Building buildingToRemove)
            {
                Planet = planet;
                BuildingToRemove = buildingToRemove;
            }
        }
    }
}
