using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Scores fleet proposals.
    /// </summary>
    public sealed class AIFleetProposalScorer : IAIProposalScorer
    {
        /// <summary>
        /// Returns whether this scorer can score the proposal.
        /// </summary>
        /// <param name="proposal">The proposal to check.</param>
        /// <returns>True if the proposal is a fleet proposal.</returns>
        public bool CanScore(AIProposal proposal)
        {
            return proposal
                is AIFleetAttackProposal
                    or AIOrbitalEngagementProposal
                    or AIColonizationProposal
                    or AIClearFleetOrderProposal
                    or AIFleetDefenseProposal
                    or AITransferUnitProposal;
        }

        /// <summary>
        /// Returns the fleet proposal score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>The fleet proposal score.</returns>
        public double Score(AITurnContext context, AIProposal proposal)
        {
            if (context?.Game == null || context.Faction == null)
                return 0;

            return proposal switch
            {
                AIFleetAttackProposal attackProposal => ScoreAttack(
                    context,
                    attackProposal.Fleet,
                    attackProposal.TargetPlanet,
                    HasExistingOrder(attackProposal)
                ),
                AIOrbitalEngagementProposal engagementProposal => ScoreEngagement(
                    context,
                    engagementProposal
                ),
                AIColonizationProposal colonizationProposal => ScoreColonization(
                    context,
                    colonizationProposal.Fleet,
                    colonizationProposal.TargetPlanet,
                    HasExistingOrder(colonizationProposal)
                ),
                AIClearFleetOrderProposal => double.PositiveInfinity,
                AIFleetDefenseProposal => context.Game.Config.AI.FleetDeployment.FleetDefenseScore,
                AITransferUnitProposal transferProposal => ScoreUnitTransfer(
                    context,
                    transferProposal
                ),
                _ => 0,
            };
        }

        /// <summary>
        /// Returns whether a proposal is advancing an existing order.
        /// </summary>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True if the proposal matches the fleet's current order.</returns>
        private bool HasExistingOrder(AIFleetAttackProposal proposal)
        {
            FleetOrder order = proposal.Fleet?.Order;
            return order != null
                && order.OrderType == proposal.OrderType
                && order.TargetPlanetId == proposal.TargetPlanet?.InstanceID;
        }

        /// <summary>
        /// Returns whether a colonization proposal is advancing the fleet's current order.
        /// </summary>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True if the proposal matches the fleet's current colonization order.</returns>
        private bool HasExistingOrder(AIColonizationProposal proposal)
        {
            FleetOrder order = proposal.Fleet?.Order;
            return order != null
                && order.OrderType == FleetOrderType.Colonize
                && order.TargetPlanetId == proposal.TargetPlanet?.InstanceID;
        }

        /// <summary>
        /// Returns the score for engaging a known hostile fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The orbital engagement proposal.</param>
        /// <returns>The engagement score.</returns>
        private double ScoreEngagement(AITurnContext context, AIOrbitalEngagementProposal proposal)
        {
            Fleet fleet = proposal.Fleet;
            Planet target = proposal.TargetPlanet;
            bool existingOrder = fleet?.Order?.OrderType == FleetOrderType.Engage;
            if (existingOrder)
                return context.Game.Config.AI.FleetDeployment.FleetDefenseScore;

            if (!CanScoreEngagement(context, fleet, target))
                return 0;

            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            double score =
                config.OrbitalResponseBonus
                + ScoreStrategicTargetValue(context.Assessment, target)
                    * config.AttackStrategicValueWeight
                + ScoreTravelEfficiency(context.Assessment, fleet, target)
                    * config.AttackTravelEfficiencyWeight
                - ScoreOpportunityCost(context, fleet) * config.AttackOpportunityCostPenaltyWeight;

            return Math.Max(0, score);
        }

        /// <summary>
        /// Returns the score for assigning or advancing an attack.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to score.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <param name="existingOrder">Whether the fleet already has this order.</param>
        /// <returns>The attack score.</returns>
        private double ScoreAttack(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet,
            bool existingOrder
        )
        {
            if (!CanScoreAttack(context, fleet, targetPlanet))
                return 0;

            AIAssessment assessment = context.Assessment;
            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            double score =
                ScoreStrategicTargetValue(assessment, targetPlanet)
                    * config.AttackStrategicValueWeight
                + ScoreReadiness(context, fleet, targetPlanet) * config.AttackReadinessWeight
                + ScoreCaptureViability(context, fleet, targetPlanet)
                    * config.AttackCaptureViabilityWeight
                + ScoreTravelEfficiency(assessment, fleet, targetPlanet)
                    * config.AttackTravelEfficiencyWeight
                - ScoreExpectedLossRisk(context, fleet, targetPlanet)
                    * config.AttackExpectedLossPenaltyWeight
                - ScoreOpportunityCost(context, fleet) * config.AttackOpportunityCostPenaltyWeight;

            if (targetPlanet.IsHeadquarters)
                score += config.HeadquartersAttackBonus;

            if (assessment.CanWinOrbitalCombat(fleet, targetPlanet))
                score += config.OrbitalResponseBonus;

            score = Math.Max(0, score);
            return existingOrder ? score + config.ExistingAttackOrderBonus : score;
        }

        /// <summary>
        /// Scores colonization.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to evaluate.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <param name="existingOrder">The existing fleet order, if any.</param>
        /// <returns>The calculated value.</returns>
        private double ScoreColonization(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet,
            bool existingOrder
        )
        {
            if (!CanScoreColonization(context, fleet, targetPlanet, existingOrder))
                return 0;

            AIAssessment assessment = context.Assessment;
            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            double score =
                config.ColonizationBaseScore
                + GetFulfillmentRatio(
                    assessment.GetPlanetValue(targetPlanet),
                    assessment.GetHighestKnownUncolonizedPlanetValue()
                ) * config.ColonizationStrategicValueWeight
                + ScoreColonizationTravelEfficiency(assessment, fleet, targetPlanet)
                    * config.ColonizationTravelEfficiencyWeight
                - ScoreOpportunityCost(context, fleet)
                    * config.ColonizationOpportunityCostPenaltyWeight;

            if (assessment.GetReadyFleetRegimentCount(fleet) > 0)
                score += config.ColonizationReadyFleetBonus;

            if (existingOrder)
                score += config.ExistingColonizationOrderBonus;

            return Math.Max(0, score);
        }

        /// <summary>
        /// Returns the score for transferring a unit into an ordered fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The transfer proposal to score.</param>
        /// <returns>The transfer score.</returns>
        private double ScoreUnitTransfer(AITurnContext context, AITransferUnitProposal proposal)
        {
            if (!CanScoreUnitTransfer(context, proposal))
                return 0;

            Fleet sourceFleet = proposal.SourceContainer as Fleet;
            CapitalShip capitalShip = proposal.Unit as CapitalShip;
            AIAssessment assessment = context.Assessment;
            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            double readinessGain = ScoreTransferReadinessGain(
                context,
                proposal.TargetFleet,
                proposal.TargetPlanet,
                capitalShip
            );
            if (readinessGain <= 0)
                return 0;

            Planet receivingPlanet = assessment.GetFleetPlanet(proposal.TargetFleet);
            double travelEfficiency = ScoreTravelEfficiency(
                assessment,
                assessment.GetFleetPlanet(sourceFleet),
                receivingPlanet
            );
            if (proposal.TargetFleet.Order?.OrderType == FleetOrderType.Defend)
            {
                return Math.Max(
                    0,
                    config.FleetDefenseScore
                        + readinessGain * config.AttackReadinessWeight
                        + travelEfficiency * config.AttackTravelEfficiencyWeight
                        - ScoreOpportunityCost(context, sourceFleet)
                            * config.AttackOpportunityCostPenaltyWeight
                );
            }

            double score =
                readinessGain * config.AttackReadinessWeight
                + ScoreStrategicTargetValue(assessment, proposal.TargetPlanet)
                    * config.AttackStrategicValueWeight
                + travelEfficiency * config.AttackTravelEfficiencyWeight
                - ScoreOpportunityCost(context, sourceFleet)
                    * config.AttackOpportunityCostPenaltyWeight;

            return Math.Max(0, score);
        }

        /// <summary>
        /// Returns the normalized strategic value of a target planet.
        /// </summary>
        /// <param name="assessment">The current AI assessment.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The target value score.</returns>
        private double ScoreStrategicTargetValue(AIAssessment assessment, Planet targetPlanet)
        {
            return GetFulfillmentRatio(
                assessment.GetPlanetValue(targetPlanet),
                assessment.GetHighestEnemyPlanetValue()
            );
        }

        /// <summary>
        /// Returns the readiness score for an attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to score.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>The readiness score.</returns>
        private double ScoreReadiness(AITurnContext context, Fleet fleet, Planet targetPlanet)
        {
            AIAssessment assessment = context.Assessment;
            int requiredRegimentCount = assessment.GetRequiredAttackRegimentCount(
                fleet,
                targetPlanet
            );
            double combatReadiness = GetFulfillmentRatio(
                assessment.GetReadyFleetCombatValue(fleet),
                assessment.GetRequiredAttackCombatStrength(targetPlanet)
            );
            double regimentReadiness = GetFulfillmentRatio(
                assessment.GetReadyFleetRegimentCount(fleet),
                requiredRegimentCount
            );
            double transportReadiness = GetFulfillmentRatio(
                assessment.GetReadyFleetRegimentCapacity(fleet),
                requiredRegimentCount
            );
            double groundReadiness = GetFulfillmentRatio(
                assessment.GetReadyFleetRegimentAttackStrength(fleet),
                assessment.GetRequiredAttackRegimentStrength(fleet, targetPlanet)
            );
            List<double> readiness = new List<double>
            {
                combatReadiness,
                regimentReadiness,
                transportReadiness,
                groundReadiness,
            };
            int requiredBombardment = assessment.GetRequiredBombardmentStrength(targetPlanet);
            if (requiredBombardment > 0)
            {
                readiness.Add(
                    GetFulfillmentRatio(
                        assessment.GetFleetBombardmentStrength(fleet),
                        requiredBombardment
                    )
                );
            }

            return GetAverage(readiness.ToArray());
        }

        /// <summary>
        /// Returns the readiness gained by transferring a capital ship.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetFleet">The target fleet.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <param name="capitalShip">The capital ship to score.</param>
        /// <returns>The readiness gain.</returns>
        private double ScoreTransferReadinessGain(
            AITurnContext context,
            Fleet targetFleet,
            Planet targetPlanet,
            CapitalShip capitalShip
        )
        {
            if (targetFleet?.Order?.OrderType == FleetOrderType.Defend)
            {
                int requiredDefense = context.Assessment.GetRequiredDefenseStrength(targetPlanet);
                double defenseBefore = GetFulfillmentRatio(
                    context.Assessment.GetProjectedFleetCombatValue(targetFleet),
                    requiredDefense
                );
                double defenseAfter = GetFulfillmentRatio(
                    context.Assessment.GetProjectedFleetCombatValue(targetFleet)
                        + context.Assessment.GetProjectedCapitalShipCombatValue(capitalShip),
                    requiredDefense
                );
                return Math.Max(0, defenseAfter - defenseBefore);
            }

            double before = ScoreProjectedReadiness(context, targetFleet, targetPlanet, null);
            double after = ScoreProjectedReadiness(context, targetFleet, targetPlanet, capitalShip);
            return Math.Max(0, after - before);
        }

        /// <summary>
        /// Returns the projected readiness after adding a capital ship.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetFleet">The target fleet.</param>
        /// <param name="targetPlanet">The fleet attack target.</param>
        /// <param name="capitalShip">The capital ship to project.</param>
        /// <returns>The projected readiness score.</returns>
        private double ScoreProjectedReadiness(
            AITurnContext context,
            Fleet targetFleet,
            Planet targetPlanet,
            CapitalShip capitalShip
        )
        {
            AIAssessment assessment = context.Assessment;
            int requiredRegimentCount = assessment.GetRequiredAttackCampaignRegimentCount(
                targetPlanet
            );
            double combatReadiness = GetFulfillmentRatio(
                assessment.GetProjectedFleetCombatValue(targetFleet)
                    + assessment.GetProjectedCapitalShipCombatValue(capitalShip),
                assessment.GetRequiredAttackCampaignCombatStrength(targetPlanet)
            );
            double regimentReadiness = GetFulfillmentRatio(
                assessment.GetFleetLoadedRegimentCount(targetFleet)
                    + assessment.GetReadyCapitalShipRegimentCount(capitalShip),
                requiredRegimentCount
            );
            double transportReadiness = GetFulfillmentRatio(
                assessment.GetFleetRegimentCapacity(targetFleet)
                    + assessment.GetReadyCapitalShipRegimentCapacity(capitalShip),
                requiredRegimentCount
            );
            double groundReadiness = GetFulfillmentRatio(
                assessment.GetProjectedFleetRegimentAttackStrength(targetFleet)
                    + assessment.GetProjectedCapitalShipRegimentAttackStrength(
                        targetFleet,
                        capitalShip
                    ),
                assessment.GetRequiredAttackCampaignRegimentStrength(targetPlanet)
            );
            List<double> readiness = new List<double>
            {
                combatReadiness,
                regimentReadiness,
                transportReadiness,
                groundReadiness,
            };
            int requiredBombardment = assessment.GetRequiredAttackCampaignBombardmentStrength(
                targetPlanet
            );
            if (requiredBombardment > 0)
            {
                readiness.Add(
                    GetFulfillmentRatio(
                        assessment.GetProjectedFleetBombardmentStrength(targetFleet)
                            + assessment.GetProjectedCapitalShipBombardmentStrength(
                                targetFleet,
                                capitalShip
                            ),
                        requiredBombardment
                    )
                );
            }

            return GetAverage(readiness.ToArray());
        }

        /// <summary>
        /// Returns the capture viability score for an attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to score.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>The capture viability score.</returns>
        private double ScoreCaptureViability(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            AIAssessment assessment = context.Assessment;
            int requiredRegimentCount = assessment.GetRequiredAttackRegimentCount(
                fleet,
                targetPlanet
            );
            double groundStrengthRatio = GetFulfillmentRatio(
                assessment.GetReadyFleetRegimentAttackStrength(fleet),
                assessment.GetRequiredAttackRegimentStrength(fleet, targetPlanet)
            );
            double troopRatio = GetFulfillmentRatio(
                assessment.GetReadyFleetRegimentCount(fleet),
                requiredRegimentCount
            );
            List<double> viability = new List<double> { groundStrengthRatio, troopRatio };
            int requiredBombardment = assessment.GetRequiredBombardmentStrength(targetPlanet);
            if (requiredBombardment > 0)
            {
                viability.Add(
                    GetFulfillmentRatio(
                        assessment.GetFleetBombardmentStrength(fleet),
                        requiredBombardment
                    )
                );
            }

            return GetAverage(viability.ToArray());
        }

        /// <summary>
        /// Returns travel efficiency for a fleet targeting a planet.
        /// </summary>
        /// <param name="assessment">The current AI assessment.</param>
        /// <param name="fleet">The fleet to score.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>The travel efficiency score.</returns>
        private double ScoreTravelEfficiency(
            AIAssessment assessment,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            Planet currentPlanet = assessment.GetFleetPlanet(fleet);
            if (currentPlanet == null)
                return 0;

            return ScoreTravelEfficiency(assessment, currentPlanet, targetPlanet);
        }

        /// <summary>
        /// Returns travel efficiency between two planets.
        /// </summary>
        /// <param name="assessment">The current AI assessment.</param>
        /// <param name="currentPlanet">The starting planet.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The travel efficiency score.</returns>
        private double ScoreTravelEfficiency(
            AIAssessment assessment,
            Planet currentPlanet,
            Planet targetPlanet
        )
        {
            if (currentPlanet == null || targetPlanet == null)
                return 0;

            double distance = currentPlanet.GetRawDistanceTo(targetPlanet);
            double farthestTargetDistance = assessment
                .EnemyPlanets.Select(planet => currentPlanet.GetRawDistanceTo(planet))
                .DefaultIfEmpty()
                .Max();

            if (farthestTargetDistance <= 0)
                return 1;

            return 1 - GetFulfillmentRatio(distance, farthestTargetDistance);
        }

        /// <summary>
        /// Scores colonization travel efficiency.
        /// </summary>
        /// <param name="assessment">The current AI assessment.</param>
        /// <param name="fleet">The fleet to evaluate.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The calculated value.</returns>
        private double ScoreColonizationTravelEfficiency(
            AIAssessment assessment,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            Planet currentPlanet = assessment.GetFleetPlanet(fleet);
            if (currentPlanet == null || targetPlanet == null)
                return 0;

            double distance = currentPlanet.GetRawDistanceTo(targetPlanet);
            double farthestTargetDistance = assessment
                .KnownUncolonizedPlanets.Select(planet => currentPlanet.GetRawDistanceTo(planet))
                .DefaultIfEmpty()
                .Max();

            if (farthestTargetDistance <= 0)
                return 1;

            return 1 - GetFulfillmentRatio(distance, farthestTargetDistance);
        }

        /// <summary>
        /// Returns the expected loss risk score for an attack.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The attacking fleet.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>The expected loss risk score.</returns>
        private double ScoreExpectedLossRisk(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            AIAssessment assessment = context.Assessment;
            List<double> risks = new List<double>();
            int orbitalDefense = assessment.GetStrongestHostileFleetStrength(targetPlanet);
            if (orbitalDefense > 0)
            {
                risks.Add(
                    GetPressureRatio(orbitalDefense, assessment.GetReadyFleetCombatValue(fleet))
                );
            }

            int groundDefense = assessment.GetDefendingRegimentDefenseStrength(targetPlanet);
            if (groundDefense > 0)
            {
                risks.Add(
                    GetPressureRatio(
                        groundDefense,
                        assessment.GetReadyFleetRegimentAttackStrength(fleet)
                    )
                );
            }

            int requiredBombardment = assessment.GetRequiredBombardmentStrength(targetPlanet);
            if (requiredBombardment > 0)
            {
                risks.Add(
                    GetPressureRatio(
                        requiredBombardment,
                        assessment.GetFleetBombardmentStrength(fleet)
                    )
                );
            }

            return GetAverage(risks.ToArray());
        }

        /// <summary>
        /// Returns the local defense opportunity cost for using a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to score.</param>
        /// <returns>The opportunity cost score.</returns>
        private double ScoreOpportunityCost(AITurnContext context, Fleet fleet)
        {
            AIAssessment assessment = context.Assessment;
            Planet currentPlanet = assessment.GetFleetPlanet(fleet);
            if (
                currentPlanet == null
                || currentPlanet.GetOwnerInstanceID() != context.Faction.InstanceID
            )
                return 0;

            int remainingLocalDefense = assessment
                .GetFriendlyFleets(currentPlanet)
                .Where(localFleet => localFleet != fleet && localFleet.Movement == null)
                .Select(assessment.GetFleetCombatValue)
                .DefaultIfEmpty()
                .Max();
            int localHostileStrength = assessment.GetStrongestHostileFleetStrength(currentPlanet);
            int requiredDefenseStrength = Math.Max(
                context.Game.Config.AI.FleetDeployment.MinimumDefenseStrength,
                localHostileStrength
            );
            int defenseGap = Math.Max(0, requiredDefenseStrength - remainingLocalDefense);
            double localValue = ScoreFriendlyPlanetValue(assessment, currentPlanet);
            double localDefenseRisk = GetFulfillmentRatio(defenseGap, requiredDefenseStrength);

            return localValue * localDefenseRisk;
        }

        /// <summary>
        /// Returns the normalized value of a friendly planet.
        /// </summary>
        /// <param name="assessment">The current AI assessment.</param>
        /// <param name="planet">The planet to score.</param>
        /// <returns>The friendly planet value score.</returns>
        private double ScoreFriendlyPlanetValue(AIAssessment assessment, Planet planet)
        {
            return GetFulfillmentRatio(
                assessment.GetPlanetValue(planet),
                assessment.GetHighestOwnedPlanetValue()
            );
        }

        /// <summary>
        /// Returns whether an attack proposal can be scored.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The attacking fleet.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>True if the attack can be scored.</returns>
        private bool CanScoreAttack(AITurnContext context, Fleet fleet, Planet targetPlanet)
        {
            if (fleet == null || targetPlanet == null)
                return false;

            if (fleet.RoleType != FleetRoleType.Battle)
                return false;

            if (fleet.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            string targetOwnerId = targetPlanet.GetOwnerInstanceID();
            return !string.IsNullOrEmpty(targetOwnerId)
                && targetOwnerId != context.Faction.InstanceID;
        }

        /// <summary>
        /// Returns whether a fleet can favorably engage a known hostile fleet at a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet being scored.</param>
        /// <param name="targetPlanet">Planet containing the hostile fleet.</param>
        /// <returns>True when the fleet can win the projected orbital combat.</returns>
        private bool CanScoreEngagement(AITurnContext context, Fleet fleet, Planet targetPlanet)
        {
            if (
                fleet?.RoleType != FleetRoleType.Battle
                || targetPlanet == null
                || fleet.GetOwnerInstanceID() != context.Faction.InstanceID
            )
                return false;

            return context.Assessment.GetStrongestHostileFleetStrength(targetPlanet) > 0
                && context.Assessment.CanWinOrbitalCombat(fleet, targetPlanet);
        }

        /// <summary>
        /// Returns whether a fleet can begin or continue colonizing a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet assigned to the planet.</param>
        /// <param name="targetPlanet">The proposed colony.</param>
        /// <param name="existingOrder">Whether the fleet is continuing its current order.</param>
        /// <returns>True when the colonization proposal remains valid.</returns>
        private bool CanScoreColonization(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet,
            bool existingOrder
        )
        {
            return fleet != null
                && targetPlanet != null
                && (fleet.RoleType == FleetRoleType.Colonization || existingOrder)
                && fleet.GetOwnerInstanceID() == context.Faction.InstanceID
                && !targetPlanet.IsColonized
                && !targetPlanet.IsDestroyed
                && string.IsNullOrEmpty(targetPlanet.GetOwnerInstanceID());
        }

        /// <summary>
        /// Returns whether a unit transfer proposal can be scored.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True if the proposal can be scored.</returns>
        private bool CanScoreUnitTransfer(AITurnContext context, AITransferUnitProposal proposal)
        {
            if (
                proposal == null
                || proposal.SourceContainer == null
                || proposal.Destination == null
                || proposal.TargetFleet == null
                || proposal.Unit == null
                || proposal.TargetPlanet == null
            )
                return false;

            Fleet sourceFleet = proposal.SourceContainer as Fleet;
            CapitalShip capitalShip = proposal.Unit as CapitalShip;
            if (sourceFleet == null || capitalShip == null)
                return false;

            if (sourceFleet == proposal.TargetFleet)
                return false;

            if (sourceFleet.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            if (proposal.TargetFleet.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            if (capitalShip.GetParentOfType<Fleet>() != sourceFleet)
                return false;

            FleetOrder order = proposal.TargetFleet.Order;
            if (order == null || order.TargetPlanetId != proposal.TargetPlanet.InstanceID)
                return false;

            if (order.OrderType == FleetOrderType.Defend)
                return context.Assessment.IsOwnedPlanet(proposal.TargetPlanet)
                    && context.Assessment.GetRequiredDefenseStrength(proposal.TargetPlanet) > 0;

            string targetOwnerId = proposal.TargetPlanet.GetOwnerInstanceID();
            return order.OrderType == FleetOrderType.Attack
                && !string.IsNullOrEmpty(targetOwnerId)
                && targetOwnerId != context.Faction.InstanceID;
        }

        /// <summary>
        /// Returns a bounded fulfillment ratio.
        /// </summary>
        /// <param name="value">Current value.</param>
        /// <param name="target">Target value.</param>
        /// <returns>The bounded fulfillment ratio.</returns>
        private double GetFulfillmentRatio(double value, double target)
        {
            if (target <= 0)
                return 1;

            return Math.Max(0, Math.Min(1, value / target));
        }

        /// <summary>
        /// Returns pressure relative to resistance.
        /// </summary>
        /// <param name="pressure">Pressure value.</param>
        /// <param name="resistance">Resistance value.</param>
        /// <returns>The pressure ratio.</returns>
        private double GetPressureRatio(double pressure, double resistance)
        {
            if (pressure <= 0)
                return 0;

            return pressure / (pressure + Math.Max(0, resistance));
        }

        /// <summary>
        /// Returns the average of supplied values.
        /// </summary>
        /// <param name="values">Values to average.</param>
        /// <returns>The average value.</returns>
        private double GetAverage(params double[] values)
        {
            if (values == null || values.Length == 0)
                return 0;

            return values.Average();
        }
    }
}
