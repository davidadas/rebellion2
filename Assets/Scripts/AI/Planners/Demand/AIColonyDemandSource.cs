using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners.Demand
{
    /// <summary>
    /// Generates founding-facility demand for newly claimed planets.
    /// </summary>
    internal static class AIColonyDemandSource
    {
        /// <summary>
        /// Adds colony demand to the production plan.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand collection to update.</param>
        internal static void AddDemands(
            AITurnContext context,
            ICollection<AIProductionDemand> demands
        )
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
                    new AIProductionDemand(
                        $"production:{context.Faction.InstanceID}:{AIProductionDemandKind.Colony}:{planet.InstanceID}",
                        AIProductionDemandKind.Colony,
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
}
