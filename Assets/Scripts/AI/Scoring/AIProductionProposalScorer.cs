using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Game.World;

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
            double score = proposal?.Demand?.Pressure ?? 0;
            int maintenanceCost = proposal?.GetMaintenanceCost() ?? 0;
            if (context?.Game == null || context.Faction == null || proposal == null)
                return score;

            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            score -= GetTravelPenalty(context, proposal);

            if (maintenanceCost <= 0)
                return score;

            int projectedHeadroom = context.Faction.ProjectedMaintenanceHeadroom - maintenanceCost;
            if (projectedHeadroom < config.MaintenanceHeadroomHardFloor)
                return 0;

            int headroomDeficit =
                config.MinimumMaintenanceHeadroomAfterProduction - projectedHeadroom;

            if (headroomDeficit > 0)
                score -= headroomDeficit * config.MaintenanceHeadroomPenaltyWeight;

            if (projectedHeadroom < 0)
                score -= config.MaintenanceShortfallPenalty;

            return score;
        }

        /// <summary>
        /// Returns the travel penalty for fleet reinforcement production.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>The travel penalty.</returns>
        private double GetTravelPenalty(AITurnContext context, AIManufactureProposal proposal)
        {
            if (proposal?.Demand?.Destination is not Fleet destinationFleet)
                return 0;

            Planet producerPlanet = proposal.ProducerPlanet;
            Planet destinationPlanet = context.Assessment.GetFleetPlanet(destinationFleet);
            if (producerPlanet == null || destinationPlanet == null)
                return 0;

            return producerPlanet.GetRawDistanceTo(destinationPlanet)
                * context.Game.Config.AI.Infrastructure.FleetReinforcementTravelPenaltyWeight;
        }
    }
}
