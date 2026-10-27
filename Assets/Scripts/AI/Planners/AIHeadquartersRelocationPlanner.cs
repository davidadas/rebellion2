using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

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

            IReadOnlyList<Fleet> defensiveFleets = FindDefensiveFleets(context, origin);
            (SpecialForces espionageAgent, Mission espionageMission) =
                FindHeadquartersEspionageAssignment(context, origin);
            RelocationDestination relocation = SelectDestination(context, headquarters, origin);
            if (relocation.Planet == null)
                return proposals;

            proposals.Add(
                new AIHeadquartersRelocationProposal(
                    headquarters,
                    origin,
                    relocation.Planet,
                    relocation.BuildingToRemove,
                    relocation.MaintenanceHeadroomCost,
                    defensiveFleets,
                    espionageAgent,
                    espionageMission,
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
        /// Returns ready fleets explicitly defending the headquarters plus the strongest idle
        /// local battle fleet when no defense order exists yet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="origin">The threatened headquarters planet.</param>
        /// <returns>The fleets that should accompany the headquarters.</returns>
        private static IReadOnlyList<Fleet> FindDefensiveFleets(
            AITurnContext context,
            Planet origin
        )
        {
            List<Fleet> localCandidates = context
                .Assessment.GetFriendlyFleets(origin)
                .Where(fleet =>
                    fleet.RoleType == FleetRoleType.Battle
                    && fleet.Movement == null
                    && !fleet.IsInCombat
                    && fleet.HasOperationalCapitalShips()
                )
                .ToList();
            List<Fleet> assigned = localCandidates
                .Where(fleet =>
                    fleet.Order?.OrderType == FleetOrderType.Defend
                    && fleet.Order.TargetPlanetId == origin.InstanceID
                )
                .OrderBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                .ToList();
            if (assigned.Count > 0)
                return assigned;

            Fleet available = localCandidates
                .Where(fleet => fleet.Order == null)
                .OrderByDescending(context.Assessment.GetFleetCombatValue)
                .ThenBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
            return available == null ? Array.Empty<Fleet>() : new[] { available };
        }

        /// <summary>
        /// Finds the special-forces unit assigned to local headquarters espionage and its active
        /// mission when one exists.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="origin">The threatened headquarters planet.</param>
        /// <returns>The assigned espionage unit and optional active mission.</returns>
        private static (SpecialForces Agent, Mission Mission) FindHeadquartersEspionageAssignment(
            AITurnContext context,
            Planet origin
        )
        {
            foreach (
                Mission mission in context
                    .Assessment.ActiveMissions.Where(mission =>
                        mission.ConfigKey == MissionTypeIDs.Espionage
                        && mission.GetParentOfType<Planet>() == origin
                    )
                    .OrderBy(mission => mission.InstanceID, StringComparer.Ordinal)
            )
            {
                SpecialForces activeAgent = mission
                    .GetAllParticipants()
                    .OfType<SpecialForces>()
                    .Where(unit =>
                        unit.GetOwnerInstanceID() == context.Faction.InstanceID
                        && unit.CanPerformMission(MissionTypeIDs.Espionage)
                    )
                    .OrderBy(unit => unit.InstanceID, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (activeAgent != null)
                    return (activeAgent, mission);
            }

            SpecialForces availableAgent = context
                .Assessment.AvailableMissionParticipants.OfType<SpecialForces>()
                .Where(unit =>
                    unit.GetParentOfType<Planet>() == origin
                    && unit.CanPerformMission(MissionTypeIDs.Espionage)
                )
                .OrderBy(unit => unit.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
            return (availableAgent, null);
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

            IEnumerable<IGrouping<int, Planet>> candidateGroups = context
                .Assessment.OwnedPlanets.Where(planet =>
                    planet != null
                    && planet != origin
                    && planet.IsColonized
                    && !planet.IsDestroyed
                    && !context.Assessment.HasKnownHostileFleet(planet)
                    && !MovementQueries.IsBlockedFromDestinationByBlockade(headquarters, planet)
                )
                .GroupBy(planet =>
                    GetKnownEnemyPlanetCount(context, knownEnemyPlanetsBySystem, planet)
                )
                .OrderBy(group => group.Key);

            foreach (IGrouping<int, Planet> candidateGroup in candidateGroups)
            {
                Planet openDestination = candidateGroup
                    .Where(candidate =>
                        context.HeadquartersQueries.CanRelocate(headquarters, candidate)
                    )
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(candidate => candidate.InstanceID, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (openDestination != null)
                    return new RelocationDestination(openDestination, null, 0);

                Planet selectedPlanet = null;
                AIPlanetBuildingSpacePolicy.Removal selectedRemoval = default;
                double selectedRemainingValue = 0;
                foreach (Planet candidate in candidateGroup)
                {
                    AIPlanetBuildingSpacePolicy.Removal removal = _spacePolicy.SelectRemoval(
                        context,
                        candidate
                    );
                    if (!removal.IsAvailable)
                        continue;

                    double remainingValue = GetRemainingPlanetValue(
                        context,
                        candidate,
                        removal.Building
                    );
                    int comparison = selectedRemoval.IsAvailable
                        ? removal.CompareCostTo(selectedRemoval)
                        : -1;
                    if (
                        comparison < 0
                        || comparison == 0
                            && (
                                remainingValue > selectedRemainingValue
                                || remainingValue == selectedRemainingValue
                                    && string.CompareOrdinal(
                                        candidate.InstanceID,
                                        selectedPlanet.InstanceID
                                    ) < 0
                            )
                    )
                    {
                        selectedPlanet = candidate;
                        selectedRemoval = removal;
                        selectedRemainingValue = remainingValue;
                    }
                }

                if (selectedRemoval.IsAvailable)
                {
                    return new RelocationDestination(
                        selectedPlanet,
                        selectedRemoval.Building,
                        selectedRemoval.MaintenanceHeadroomCost
                    );
                }
            }

            return default;
        }

        /// <summary>
        /// Returns the number of known enemy planets in a destination's system.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="knownEnemyPlanetsBySystem">Known enemy counts keyed by system.</param>
        /// <param name="planet">The destination planet.</param>
        /// <returns>The known enemy planet count.</returns>
        private static int GetKnownEnemyPlanetCount(
            AITurnContext context,
            IReadOnlyDictionary<string, int> knownEnemyPlanetsBySystem,
            Planet planet
        )
        {
            return knownEnemyPlanetsBySystem.TryGetValue(
                context.Assessment.GetPlanetSystemId(planet),
                out int count
            )
                ? count
                : 0;
        }

        /// <summary>
        /// Calculates destination value after its selected building has been removed.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The destination planet.</param>
        /// <param name="building">The building selected for removal.</param>
        /// <returns>The remaining strategic planet value.</returns>
        private static double GetRemainingPlanetValue(
            AITurnContext context,
            Planet planet,
            Building building
        )
        {
            double remainingValue = context.Assessment.GetPlanetValue(planet) - 1;
            int processRate = building.GetProcessRate();
            if (
                building.ManufacturingStatus == ManufacturingStatus.Complete
                && building.GetProductionType() != ManufacturingType.None
                && processRate > 0
            )
                remainingValue -= 1d / processRate;
            return remainingValue;
        }

        private readonly struct RelocationDestination
        {
            internal Planet Planet { get; }

            internal Building BuildingToRemove { get; }

            internal int MaintenanceHeadroomCost { get; }

            /// <summary>
            /// Creates a resolved headquarters destination.
            /// </summary>
            /// <param name="planet">The destination planet.</param>
            /// <param name="buildingToRemove">The building that must be removed, if any.</param>
            /// <param name="maintenanceHeadroomCost">The net maintenance headroom consumed by the removal.</param>
            internal RelocationDestination(
                Planet planet,
                Building buildingToRemove,
                int maintenanceHeadroomCost
            )
            {
                Planet = planet;
                BuildingToRemove = buildingToRemove;
                MaintenanceHeadroomCost = Math.Max(0, maintenanceHeadroomCost);
            }
        }
    }
}
