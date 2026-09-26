using System;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Scorers
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
                    AIFleetProposalScorer.CountCurrentAttackCapabilities(context, fleet, target),
                    utility.AttackRequirements
                ),
                utility.AttackRequirements
            );
            score.Add(
                assessment.GetOwnedSystemPresenceRatio(assessment.GetPlanetSystemId(target)),
                utility.SystemPresence
            );
            score.Add(target.IsHeadquarters ? 1 : 0, utility.Headquarters);
            score.Add(
                AIUtility.Fulfillment(assessment.GetPlanetValue(target), utility.TargetValue),
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
                AIUtility.Fulfillment(fleet.GetCurrentRegimentCount(), utility.ColonyRegiments),
                utility.ColonyRegiments
            );
            score.Add(
                AIUtility.Fulfillment(fleet.GetRegimentCapacity(), utility.ColonyCapacity),
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
            score.Add(1 - AIUtility.Fulfillment(combat, int.MaxValue), utility.AssemblyWeakness);
            score.Add(
                1 - AIUtility.Fulfillment(capacity, int.MaxValue),
                utility.AssemblyCapacityNeed
            );
            return score.Value;
        }

        /// <summary>
        /// Scores the production pressure created by a fleet-unit deficit.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="basePressure">Configured pressure before deficit scaling.</param>
        /// <param name="deficit">Current unit deficit.</param>
        /// <param name="targetCount">Target unit count.</param>
        /// <returns>The scaled fleet-production pressure.</returns>
        public static double ScoreDeficit(
            AITurnContext context,
            int basePressure,
            int deficit,
            int targetCount
        )
        {
            double deficitRatio = deficit / (double)Math.Max(1, targetCount);
            return Math.Min(
                100,
                basePressure
                    + AIUtility.EvaluateDiscretePressure(
                        deficitRatio,
                        context.Game.Config.AI.Infrastructure.DemandUtility.Deficit
                    )
            );
        }

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
            int requiredStrength = context.StrategicPlan.GetDefenseStrength(targetPlanet);
            int strengthGap = Math.Max(0, requiredStrength - availableStrength);
            return AIUtility.Evaluate(
                AIUtility.Fulfillment(
                    strengthGap,
                    context
                        .Game
                        .Config
                        .AI
                        .FleetDeployment
                        .DefenseAllocationUtility
                        .ReinforcementNeed
                ),
                context.Game.Config.AI.FleetDeployment.DefenseAllocationUtility.ReinforcementNeed
            );
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
