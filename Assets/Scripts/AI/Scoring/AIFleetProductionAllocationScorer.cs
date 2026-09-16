using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Scores fleets competing for production capacity.
    /// </summary>
    public static class AIFleetProductionAllocationScorer
    {
        /// <summary>
        /// Scores an attack fleet's production priority.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet seeking reinforcement.</param>
        /// <param name="target">The assigned attack target.</param>
        /// <param name="readiness">The fleet's projected readiness.</param>
        /// <returns>The fleet's production-allocation score.</returns>
        public static double ScoreAttack(
            AITurnContext context,
            Fleet fleet,
            Planet target,
            double readiness
        )
        {
            GameConfig.AIFleetProductionAllocationUtilityConfig utility = GetUtility(context);
            AIAssessment assessment = context.Assessment;
            AIUtilityScore score = new AIUtilityScore();
            score.Add(readiness, utility.AttackReadiness);
            score.Add(
                AIUtility.Fulfillment(
                    assessment.CountCurrentAttackRequirementsMet(fleet, target),
                    AIUtilityDomain.AttackRequirementCount
                ),
                utility.AttackRequirements
            );
            score.Add(
                assessment.GetOwnedSystemPresenceRatio(assessment.GetPlanetSystemId(target)),
                utility.SystemPresence
            );
            score.Add(target.IsHeadquarters ? 1 : 0, utility.Headquarters);
            score.Add(
                AIUtility.Fulfillment(
                    assessment.GetPlanetValue(target),
                    AIUtilityDomain.FleetPlanetValue
                ),
                utility.TargetValue
            );
            return score.Value;
        }

        /// <summary>
        /// Scores a colonization fleet's production priority.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet seeking reinforcement.</param>
        /// <returns>The fleet's production-allocation score.</returns>
        public static double ScoreColonization(AITurnContext context, Fleet fleet)
        {
            GameConfig.AIFleetProductionAllocationUtilityConfig utility = GetUtility(context);
            AIUtilityScore score = new AIUtilityScore();
            score.Add(
                AIUtility.Fulfillment(
                    fleet.GetCurrentRegimentCount(),
                    AIUtilityDomain.FleetRegimentCount
                ),
                utility.ColonyRegiments
            );
            score.Add(
                AIUtility.Fulfillment(
                    fleet.GetRegimentCapacity(),
                    AIUtilityDomain.FleetRegimentCount
                ),
                utility.ColonyCapacity
            );
            return score.Value;
        }

        /// <summary>
        /// Scores an idle battle fleet's need for initial assembly.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet seeking reinforcement.</param>
        /// <returns>The fleet's production-allocation score.</returns>
        public static double ScoreAssembly(AITurnContext context, Fleet fleet)
        {
            GameConfig.AIFleetProductionAllocationUtilityConfig utility = GetUtility(context);
            double combat = context.Assessment.GetProjectedFleetCombatValue(fleet);
            double capacity = fleet.GetRegimentCapacity();
            AIUtilityScore score = new AIUtilityScore();
            score.Add(
                1 - AIUtility.Fulfillment(combat, AIUtilityDomain.AssemblyOrderingScale),
                utility.AssemblyWeakness
            );
            score.Add(
                1 - AIUtility.Fulfillment(capacity, AIUtilityDomain.AssemblyOrderingScale),
                utility.AssemblyCapacityNeed
            );
            return score.Value;
        }

        /// <summary>
        /// Returns the fleet-production allocation utility configuration.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The configured fleet-allocation considerations.</returns>
        private static GameConfig.AIFleetProductionAllocationUtilityConfig GetUtility(
            AITurnContext context
        ) => context.Game.Config.AI.Infrastructure.FleetAllocationUtility;
    }
}
