using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Galaxy;

namespace Rebellion.AI.Scoring
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
            score.AddRaw(planet.GetEnergyCapacity(), utility.Energy);
            score.AddRaw(planet.GetRawResourceNodes(), utility.Resources);
            return score.Value;
        }
    }
}
