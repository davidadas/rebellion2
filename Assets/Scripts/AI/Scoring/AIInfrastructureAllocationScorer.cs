using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Scores planets for sector production-hub allocation.
    /// </summary>
    public static class AIInfrastructureAllocationScorer
    {
        /// <summary>
        /// Returns a planet's configured production-hub utility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The candidate hub planet.</param>
        /// <param name="buildingType">The production facility being allocated.</param>
        /// <param name="feasibleCount">The facility count the planet can support.</param>
        /// <param name="assignedHubIds">Planets already assigned another primary hub role.</param>
        /// <returns>The production-hub allocation score.</returns>
        public static double Score(
            AITurnContext context,
            Planet planet,
            BuildingType buildingType,
            int feasibleCount,
            ISet<string> assignedHubIds
        )
        {
            GameConfig.AIInfrastructureAllocationUtilityConfig utility = context
                .Game
                .Config
                .AI
                .Infrastructure
                .AllocationUtility;
            AIUtilityScore score = new AIUtilityScore();
            score.AddRaw(
                planet.GetTotalBuildingTypeCount(buildingType),
                utility.ExistingFacilities
            );
            score.Add(
                assignedHubIds?.Contains(planet.InstanceID) == true ? 0 : 1,
                utility.UnassignedHub
            );
            score.AddRaw(feasibleCount, utility.FeasibleCapacity);
            score.AddRaw(context.Assessment.GetPlanetValue(planet), utility.StrategicValue);
            return score.Value;
        }
    }
}
