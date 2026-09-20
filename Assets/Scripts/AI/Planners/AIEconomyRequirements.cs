using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Generates founding-facility requirements for newly claimed planets.
    /// </summary>
    internal sealed class AIEconomyRequirements
    {
        /// <summary>
        /// Adds colony requirements to the production plan.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        internal void AddColonyRequirements(
            AITurnContext context,
            ICollection<AIProductionRequirement> requirements
        )
        {
            int plannedMines = context.Faction.GetTotalRawMinedResources();
            int plannedRefineries = context.Faction.GetTotalRawRefinementCapacity();
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(planet =>
                        RequiresFoundingFacility(context, planet)
                    )
                    .OrderByDescending(planet => planet.GetUnminedResourceNodeCount())
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                BuildingType buildingType = SelectInitialColonyBuildingType(
                    planet,
                    plannedMines,
                    plannedRefineries
                );
                requirements.Add(
                    new AIProductionRequirement(
                        AIProductionRequirement.CreateId(
                            context.Faction.InstanceID,
                            AIProductionRequirementKind.Colony,
                            planet.InstanceID
                        ),
                        AIProductionRequirementKind.Colony,
                        ManufacturingType.Building,
                        buildingType,
                        planet,
                        1,
                        context.Game.Config.AI.Infrastructure.EconomySevereDemandPercent
                    )
                );

                if (buildingType == BuildingType.Mine)
                    plannedMines++;
                else
                    plannedRefineries++;
            }
        }

        /// <summary>
        /// Selects a colony's founding facility without worsening the faction's resource balance.
        /// </summary>
        /// <param name="planet">Planet receiving its first economic facility.</param>
        /// <param name="plannedMines">Current and queued mine output.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <returns>The preferred founding facility type.</returns>
        private static BuildingType SelectInitialColonyBuildingType(
            Planet planet,
            int plannedMines,
            int plannedRefineries
        )
        {
            return planet.GetUnminedResourceNodeCount() > 0 && plannedMines <= plannedRefineries
                ? BuildingType.Mine
                : BuildingType.Refinery;
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
