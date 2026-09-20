using Rebellion.AI.Core;
using Rebellion.Game;
using Rebellion.Game.Galaxy;

namespace Rebellion.AI.Fleets
{
    /// <summary>
    /// Scores colony targets within an assigned system.
    /// </summary>
    public static class AIColonizationTargetScorer
    {
        /// <summary>
        /// Returns a colony target's configured economic utility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The candidate colony.</param>
        /// <returns>The colony target utility score.</returns>
        public static double Score(AITurnContext context, Planet planet)
        {
            if (context?.Game?.Config == null || planet == null)
                return 0;

            GameConfig.AIColonizationTargetUtilityConfig utility = context
                .Game
                .Config
                .AI
                .FleetDeployment
                .ColonizationTargetUtility;
            AIUtilityScore score = new AIUtilityScore();
            score.Add(
                AIUtility.Fulfillment(
                    planet.GetEnergyCapacity(),
                    AIUtilityDomain.ColonizationCapacity
                ),
                utility.Energy
            );
            score.Add(
                AIUtility.Fulfillment(
                    planet.GetRawResourceNodes(),
                    AIUtilityDomain.ColonizationCapacity
                ),
                utility.Resources
            );
            return score.Value;
        }
    }
}
