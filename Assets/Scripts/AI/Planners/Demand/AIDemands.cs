using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners.Demand
{
    /// <summary>
    /// Generates founding-facility demand for newly claimed planets.
    /// </summary>
    internal sealed class AIColonyDemandSource : AIDemandSource
    {
        /// <summary>
        /// Adds colony demand to the production plan.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand collection to update.</param>
        internal override void AddDemands(AITurnContext context, ICollection<AIDemand> demands)
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(planet =>
                        RequiresFoundingFacility(context, planet)
                    )
                    .OrderByDescending(planet => planet.GetUnminedResourceNodeCount())
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                BuildingType buildingType =
                    planet.GetUnminedResourceNodeCount() > 0
                        ? BuildingType.Mine
                        : BuildingType.Refinery;
                demands.Add(
                    new AIDemand(
                        AIDemand.CreateId(
                            context.Faction.InstanceID,
                            AIDemandKind.Colony,
                            planet.InstanceID
                        ),
                        AIDemandKind.Colony,
                        ManufacturingType.Building,
                        buildingType,
                        planet,
                        1,
                        context.Game.Config.AI.Infrastructure.EconomySevereDemandPercent
                    )
                );
            }
        }

        /// <summary>
        /// Returns whether a claimed planet still needs its first economic facility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet has a settled regiment but no colony infrastructure.</returns>
        private static bool RequiresFoundingFacility(AITurnContext context, Planet planet)
        {
            return planet?.IsColonized == false
                && !planet.IsDestroyed
                && planet.GetAvailableEnergy() > 0
                && context
                    .Assessment.GetPlanetRegiments(planet)
                    .Any(regiment =>
                        regiment.ManufacturingStatus == ManufacturingStatus.Complete
                        && regiment.Movement == null
                        && regiment.GetOwnerInstanceID() == planet.GetOwnerInstanceID()
                    );
        }
    }

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
            Dictionary<string, int> inventoryByRole = new Dictionary<string, int>(
                StringComparer.Ordinal
            );
            Dictionary<string, int> readyOrBuildingByRole = new Dictionary<string, int>(
                StringComparer.Ordinal
            );
            foreach (SpecialForces unit in existingUnits)
            {
                string roleId = GetRoleId(unit);
                IncrementCount(inventoryByRole, roleId);
                if (
                    context.Faction.IsAvailableMissionParticipant(unit)
                    || unit.ManufacturingStatus != ManufacturingStatus.Complete
                )
                {
                    IncrementCount(readyOrBuildingByRole, roleId);
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
                SpecialForces template = role.OrderBy(candidate => candidate.ConstructionCost)
                    .ThenBy(candidate => candidate.MaintenanceCost)
                    .ThenBy(candidate => candidate.GetTypeID(), StringComparer.Ordinal)
                    .First();
                inventoryByRole.TryGetValue(role.Key, out int currentCount);
                readyOrBuildingByRole.TryGetValue(role.Key, out int availableOrBuildingCount);
                int inventoryDeficit = config.SpecialForcesTargetCountPerRole - currentCount;
                int readinessDeficit =
                    config.SpecialForcesReadyReservePerRole - availableOrBuildingCount;
                int deficit = Math.Max(inventoryDeficit, readinessDeficit);
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
                            deficit,
                            Math.Max(
                                config.SpecialForcesTargetCountPerRole,
                                config.SpecialForcesReadyReservePerRole
                            ),
                            config.SpecialForcesDemandPercent
                        ),
                        template.GetTypeID()
                    )
                );
            }
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
        /// <param name="deficit">The number of missing units.</param>
        /// <param name="targetCount">The configured target count.</param>
        /// <param name="baseDemandPercent">The base production pressure.</param>
        /// <returns>The bounded production pressure.</returns>
        private static double GetPressure(int deficit, int targetCount, int baseDemandPercent)
        {
            int deficitPercent = deficit * 100 / Math.Max(1, targetCount);
            return Math.Min(100, baseDemandPercent + deficitPercent);
        }
    }
}
