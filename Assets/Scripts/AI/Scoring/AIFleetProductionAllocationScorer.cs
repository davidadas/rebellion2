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
            return AIUtility.Evaluate(target != null ? 1 : 0, utility.AttackTarget)
                + AIUtility.Evaluate(readiness, utility.AttackReadiness)
                + AIUtility.EvaluateRaw(
                    assessment.CountCurrentAttackRequirementsMet(fleet, target),
                    utility.AttackRequirements
                )
                + AIUtility.Evaluate(
                    assessment.GetOwnedSystemPresenceRatio(assessment.GetPlanetSystemId(target)),
                    utility.SystemPresence
                )
                + AIUtility.Evaluate(target?.IsHeadquarters == true ? 1 : 0, utility.Headquarters)
                + AIUtility.EvaluateRaw(assessment.GetPlanetValue(target), utility.TargetValue);
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
            return AIUtility.EvaluateRaw(fleet.GetCurrentRegimentCount(), utility.ColonyRegiments)
                + AIUtility.EvaluateRaw(fleet.GetRegimentCapacity(), utility.ColonyCapacity);
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
            return AIUtility.Evaluate(
                    1 - AIUtility.Fulfillment(combat, int.MaxValue),
                    utility.AssemblyWeakness
                )
                + AIUtility.Evaluate(
                    1 - AIUtility.Fulfillment(capacity, int.MaxValue),
                    utility.AssemblyCapacityNeed
                );
        }

        private static GameConfig.AIFleetProductionAllocationUtilityConfig GetUtility(
            AITurnContext context
        ) => context.Game.Config.AI.Infrastructure.FleetAllocationUtility;
    }
}
