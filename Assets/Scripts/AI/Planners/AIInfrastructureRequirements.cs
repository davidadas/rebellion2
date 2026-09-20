using System;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Defines strategic production-facility requirements shared by construction and retirement.
    /// </summary>
    internal sealed class AIInfrastructureRequirements
    {
        /// <summary>
        /// Returns the strategic facility quantity required by the faction's current planet count.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">The production-facility category.</param>
        /// <returns>The required faction-wide facility count.</returns>
        internal int GetDesiredFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            if (context?.Assessment == null || context.Game?.Config?.AI?.Infrastructure == null)
                return 0;

            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int planetsPerFacility = GetPlanetsPerFacility(config, buildingType);
            if (planetsPerFacility <= 0)
                return 0;

            int desiredCount = IntegerMath.DivideRoundedUp(
                context.Assessment.OwnedPlanets.Count,
                planetsPerFacility
            );
            return buildingType == BuildingType.ConstructionFacility
                ? Math.Max(
                    desiredCount,
                    Math.Min(
                        context.Assessment.OwnedPlanets.Count,
                        config.MinimumConstructionFacilityLanes
                    )
                )
                : desiredCount;
        }

        /// <summary>
        /// Returns the configured number of planets supported by one facility category.
        /// </summary>
        /// <param name="config">Infrastructure configuration.</param>
        /// <param name="buildingType">The production-facility category.</param>
        /// <returns>The planets-per-facility ratio, or zero for non-production facilities.</returns>
        private static int GetPlanetsPerFacility(
            GameConfig.AIInfrastructureConfig config,
            BuildingType buildingType
        )
        {
            return buildingType switch
            {
                BuildingType.ConstructionFacility => config.PlanetsPerConstructionFacility,
                BuildingType.Shipyard => config.PlanetsPerShipyard,
                BuildingType.TrainingFacility => config.PlanetsPerTrainingFacility,
                _ => 0,
            };
        }
    }
}
