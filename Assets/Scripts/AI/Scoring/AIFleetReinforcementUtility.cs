using System;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Evaluates fleet reinforcement needs shared by production and unit transfer planning.
    /// </summary>
    internal static class AIFleetReinforcementUtility
    {
        /// <summary>
        /// Returns the utility of closing a planet's remaining fleet-defense gap.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">The defended planet.</param>
        /// <param name="availableStrength">Combat strength already available or committed.</param>
        /// <returns>The configured utility of the remaining defense need.</returns>
        internal static double ScoreDefenseNeed(
            AITurnContext context,
            Planet targetPlanet,
            int availableStrength
        )
        {
            int requiredStrength = context.Assessment.GetRequiredDefenseStrength(targetPlanet);
            int strengthGap = Math.Max(0, requiredStrength - availableStrength);
            return AIUtility.Evaluate(
                AIUtility.Fulfillment(strengthGap, 10000),
                context.Game.Config.AI.FleetDeployment.DefenseAllocationUtility.ReinforcementNeed
            );
        }
    }
}
