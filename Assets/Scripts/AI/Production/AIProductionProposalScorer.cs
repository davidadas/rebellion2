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
            return proposal is AIManufactureProposal or AIFacilityRemovalProposal;
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
                AIFacilityRemovalProposal => 0,
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
            double demandPressure = proposal?.Requirement?.Pressure ?? 0;
            int maintenanceCost = proposal?.GetUnitMaintenanceCost() ?? 0;
            if (context?.Game == null || context.Faction == null || proposal == null)
                return AIUtility.Fulfillment(
                    demandPressure,
                    AIUtilityDomain.ProductionDemandPressure
                );

            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            GameConfig.AIProductionUtilityConfig utility = config.ProductionUtility;
            AIUtilityScore score = new AIUtilityScore();
            score.Add(
                AIUtility.Fulfillment(demandPressure, AIUtilityDomain.ProductionDemandPressure),
                config.DemandUtility
            );
            double colonyFoundationInput = GetColonyFoundationInput(context, proposal);
            if (proposal.Requirement.BuildingType == BuildingType.ConstructionFacility)
                score.Add(colonyFoundationInput, utility.ColonyFoundation);
            score.AddCost(
                AIUtility.Fulfillment(GetTravelCost(context, proposal), AIUtilityDomain.TravelCost),
                utility.TravelCost
            );

            int projectedHeadroom =
                context.Assessment.ProjectedMaintenanceHeadroom - maintenanceCost;
            if (
                maintenanceCost > 0
                && proposal.Requirement.RestoresMaintenanceCapacity == false
                && projectedHeadroom < proposal.GetMinimumMaintenanceHeadroom(context)
            )
                return 0;

            int headroomDeficit = config.MaintenanceHeadroomReserve - projectedHeadroom;

            score.AddCost(
                AIUtility.Fulfillment(
                    System.Math.Max(0, headroomDeficit),
                    config.MaintenanceHeadroomReserve
                ),
                utility.HeadroomRisk
            );
            score.AddCost(projectedHeadroom < 0 ? 1 : 0, utility.Shortfall);

            return score.RankValue;
        }

        /// <summary>
        /// Returns the normalized value of using a proposal to found Outer Rim construction.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The production proposal to inspect.</param>
        /// <returns>One before the first construction yard reaches an Outer Rim planet; otherwise zero.</returns>
        private static double GetColonyFoundationInput(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            Planet destination = proposal?.Requirement?.DestinationPlanet;
            if (
                destination?.GetParentOfType<PlanetSector>()?.SectorType
                != PlanetSectorType.OuterRim
            )
            {
                return 0;
            }

            return
                context.Assessment.GetPlanetProductionFacilityCount(
                    destination,
                    ManufacturingType.Building
                ) == 0
                ? 1
                : 0;
        }

        /// <summary>
        /// Returns the travel penalty for fleet reinforcement production.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>The travel penalty.</returns>
        private double GetTravelCost(AITurnContext context, AIManufactureProposal proposal)
        {
            if (proposal?.Requirement?.Destination is not Fleet destinationFleet)
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
