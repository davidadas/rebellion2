using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Generates production demand for faction special-forces templates.
    /// </summary>
    internal sealed class AISpecialForcesDemandSource : AIDemandSource
    {
        /// <summary>
        /// Adds special-forces demand to the production plan.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand collection to update.</param>
        internal override void AddDemands(AITurnContext context, ICollection<AIDemand> demands)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            List<SpecialForces> existingUnits =
                context.Faction.GetOwnedUnitsByType<SpecialForces>();
            Dictionary<string, int> decoySupplyByRole = new Dictionary<string, int>(
                StringComparer.Ordinal
            );
            Dictionary<string, int> activeOfficerMissionsByType =
                GetActiveHostileOfficerMissionCounts(context);
            foreach (SpecialForces unit in existingUnits)
            {
                if (
                    context.Faction.IsAvailableMissionParticipant(unit)
                    || unit.ManufacturingStatus != ManufacturingStatus.Complete
                    || IsAssignedAsDecoy(unit)
                )
                {
                    IncrementCount(decoySupplyByRole, GetRoleId(unit));
                }
            }

            foreach (
                IGrouping<string, SpecialForces> role in context
                    .Faction.GetUnlockedTechnologies(ManufacturingType.Troop)
                    .Select(technology => technology.GetReference())
                    .OfType<SpecialForces>()
                    .Where(template => template.AllowedMissionTypeIDs.Count > 0)
                    .GroupBy(GetRoleId, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
            )
            {
                GameConfig.AIConsiderationConfig buildEfficiency = context
                    .Game
                    .Config
                    .AI
                    .Selection
                    .TechnologyUtility
                    .SpecialForces
                    .BuildEfficiency;
                SpecialForces template = role.OrderByDescending(candidate =>
                        AIUtility.Evaluate(
                            1 - AIUtility.Fulfillment(candidate.ConstructionCost, 100),
                            buildEfficiency
                        )
                    )
                    .ThenBy(candidate => candidate.MaintenanceCost)
                    .ThenBy(candidate => candidate.GetTypeID(), StringComparer.Ordinal)
                    .First();
                decoySupplyByRole.TryGetValue(role.Key, out int decoySupply);
                int activeMissionDemand = template.AllowedMissionTypeIDs.Sum(missionTypeId =>
                    activeOfficerMissionsByType.TryGetValue(missionTypeId, out int count)
                        ? count
                        : 0
                );
                int desiredSupply = GetDesiredSupply(
                    activeMissionDemand,
                    config.SpecialForcesMissionCoveragePercent
                );
                int deficit = desiredSupply - decoySupply;
                if (deficit <= 0)
                    continue;

                Planet destination = FindDestination(context);
                if (destination == null)
                    return;

                demands.Add(
                    new AIDemand(
                        AIDemand.CreateId(
                            context.Faction.InstanceID,
                            AIDemandKind.SpecialForces,
                            template.GetTypeID()
                        ),
                        AIDemandKind.SpecialForces,
                        ManufacturingType.Troop,
                        BuildingType.None,
                        destination,
                        deficit,
                        GetPressure(
                            context,
                            deficit,
                            desiredSupply,
                            config.SpecialForcesDemandPercent
                        ),
                        template.GetTypeID()
                    )
                );
            }
        }

        /// <summary>
        /// Calculates decoy supply from current hostile officer-mission workload.
        /// </summary>
        /// <param name="activeMissionCount">The active officer-led hostile mission count.</param>
        /// <param name="coveragePercent">The portion of that workload to cover with decoys.</param>
        /// <returns>The required decoy supply.</returns>
        private static int GetDesiredSupply(int activeMissionCount, int coveragePercent)
        {
            int boundedCoveragePercent = Math.Max(0, Math.Min(100, coveragePercent));
            return (activeMissionCount * boundedCoveragePercent + 99) / 100;
        }

        /// <summary>
        /// Returns whether a special-forces unit is currently assigned as a mission decoy.
        /// </summary>
        /// <param name="unit">The special-forces unit to inspect.</param>
        /// <returns>True when the unit belongs to a mission's decoy team.</returns>
        private static bool IsAssignedAsDecoy(SpecialForces unit)
        {
            Mission mission = unit?.GetParentOfType<Mission>();
            return mission?.GetDecoyParticipants().Contains(unit) == true;
        }

        /// <summary>
        /// Counts active officer-led missions in enemy territory by mission type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Active hostile officer-mission counts keyed by mission type.</returns>
        private static Dictionary<string, int> GetActiveHostileOfficerMissionCounts(
            AITurnContext context
        )
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Mission mission in context.Assessment.ActiveMissions)
            {
                Planet target = mission.GetParentOfType<Planet>();
                if (
                    target == null
                    || !context.Assessment.IsEnemyPlanet(target)
                    || !mission.GetMainParticipants().OfType<Officer>().Any()
                )
                    continue;

                string missionTypeId = mission.ConfigKey;
                counts.TryGetValue(missionTypeId, out int count);
                counts[missionTypeId] = count + 1;
            }

            return counts;
        }

        /// <summary>
        /// Increments the count stored for a special-forces role.
        /// </summary>
        /// <param name="counts">The role counts to update.</param>
        /// <param name="roleId">The role identifier to increment.</param>
        private static void IncrementCount(IDictionary<string, int> counts, string roleId)
        {
            counts.TryGetValue(roleId, out int count);
            counts[roleId] = count + 1;
        }

        /// <summary>
        /// Returns the stable role represented by a special-forces unit's mission capabilities.
        /// </summary>
        /// <param name="unit">The special-forces unit or template to inspect.</param>
        /// <returns>The ordered mission-capability identifier.</returns>
        private static string GetRoleId(SpecialForces unit)
        {
            return string.Join(
                "|",
                unit.AllowedMissionTypeIDs.OrderBy(
                    missionTypeId => missionTypeId,
                    StringComparer.Ordinal
                )
            );
        }

        /// <summary>
        /// Finds the owned planet best suited to receive another special-forces unit.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The selected destination, or null when no owned colony is available.</returns>
        private static Planet FindDestination(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderBy(planet => planet.GetChildren<SpecialForces>().Count)
                .ThenByDescending(planet =>
                    context.Assessment.GetPlanetProductionRate(planet, ManufacturingType.Troop)
                )
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Calculates production pressure from the configured target and current deficit.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="deficit">The number of missing units.</param>
        /// <param name="targetCount">The configured target count.</param>
        /// <param name="baseDemandPercent">The base production pressure.</param>
        /// <returns>The bounded production pressure.</returns>
        private static double GetPressure(
            AITurnContext context,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            double deficitValue = AIUtility.EvaluateDiscretePressure(
                deficit / (double)Math.Max(1, targetCount),
                context.Game.Config.AI.Infrastructure.DemandUtility.Deficit
            );
            return Math.Min(100, baseDemandPercent + deficitValue);
        }
    }
}
