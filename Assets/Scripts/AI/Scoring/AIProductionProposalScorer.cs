using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Scores production proposals.
    /// </summary>
    public sealed class AIProductionProposalScorer : IAIProposalScorer
    {
        /// <summary>
        /// Returns whether this scorer can score the proposal.
        /// </summary>
        /// <param name="proposal">The proposal to check.</param>
        /// <returns>True if the proposal is a production proposal.</returns>
        public bool CanScore(AIProposal proposal)
        {
            return proposal is AIManufactureProposal;
        }

        /// <summary>
        /// Returns the production proposal score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>The production proposal score.</returns>
        public double Score(AITurnContext context, AIProposal proposal)
        {
            return proposal switch
            {
                AIManufactureProposal manufactureProposal => ScoreManufactureProposal(
                    context,
                    manufactureProposal
                ),
                _ => 0,
            };
        }

        /// <summary>
        /// Returns the score for one manufacture proposal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>The manufacture proposal score.</returns>
        private double ScoreManufactureProposal(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            double demandPressure = proposal?.Demand?.Pressure ?? 0;
            int maintenanceCost = proposal?.GetUnitMaintenanceCost() ?? 0;
            if (context?.Game == null || context.Faction == null || proposal == null)
                return AIUtility.Fulfillment(
                    demandPressure,
                    new GameConfig.AISelectionConfig().DemandUtility.InputMaximum
                );

            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            GameConfig.AIProductionUtilityConfig utility = config.ProductionUtility;
            AIUtilityScore score = new AIUtilityScore();
            score.AddRaw(demandPressure, config.DemandUtility);
            score.AddCostRaw(GetTravelCost(context, proposal), utility.TravelCost);

            int projectedHeadroom =
                context.Assessment.ProjectedMaintenanceHeadroom - maintenanceCost;
            if (maintenanceCost > 0 && projectedHeadroom < config.MaintenanceHeadroomHardFloor)
                return 0;

            int headroomDeficit =
                config.MinimumMaintenanceHeadroomAfterProduction - projectedHeadroom;

            score.AddCost(
                AIUtility.Fulfillment(
                    System.Math.Max(0, headroomDeficit),
                    config.MinimumMaintenanceHeadroomAfterProduction
                ),
                utility.HeadroomRisk
            );
            score.AddCost(projectedHeadroom < 0 ? 1 : 0, utility.Shortfall);

            return score.RankValue;
        }

        /// <summary>
        /// Returns the travel penalty for fleet reinforcement production.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>The travel penalty.</returns>
        private double GetTravelCost(AITurnContext context, AIManufactureProposal proposal)
        {
            if (proposal?.Demand?.Destination is not Fleet destinationFleet)
                return 0;

            Planet producerPlanet = proposal.ProducerPlanet;
            Planet destinationPlanet = context.Assessment.GetFleetPlanet(destinationFleet);
            if (producerPlanet == null || destinationPlanet == null)
                return 0;

            double distanceScale = context.Game.Config.Movement.DistanceScale;
            if (distanceScale <= 0)
                return 0;

            return producerPlanet.GetRawDistanceTo(destinationPlanet) / distanceScale;
        }
    }
}
