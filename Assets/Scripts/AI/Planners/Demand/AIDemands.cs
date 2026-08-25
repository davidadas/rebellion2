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

            foreach (
                SpecialForces template in context
                    .Faction.GetUnlockedTechnologies(ManufacturingType.Troop)
                    .Select(technology => technology.GetReference())
                    .OfType<SpecialForces>()
                    .OrderBy(template => template.GetTypeID(), StringComparer.Ordinal)
            )
            {
                int currentCount = existingUnits.Count(unit =>
                    unit.GetTypeID() == template.GetTypeID()
                );
                int deficit = config.SpecialForcesTargetCountPerType - currentCount;
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
                            config.SpecialForcesTargetCountPerType,
                            config.SpecialForcesDemandPercent
                        ),
                        template.GetTypeID()
                    )
                );
            }
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
