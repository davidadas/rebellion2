using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.AI.Scorers
{
    /// <summary>
    /// Scores mission proposals.
    /// </summary>
    public sealed class AIMissionProposalScorer : IAIProposalScorer
    {
        /// <summary>
        /// Returns whether this scorer can score the proposal.
        /// </summary>
        /// <param name="proposal">The proposal to check.</param>
        /// <returns>True if the proposal is a mission proposal.</returns>
        public bool CanScore(AIProposal proposal)
        {
            return proposal
                is AIMissionProposal
                    or AIAbortMissionProposal
                    or AIMissionStagingProposal;
        }

        /// <summary>
        /// Returns the mission proposal score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>The mission proposal score.</returns>
        public double Score(AITurnContext context, AIProposal proposal)
        {
            return Score(context, proposal, null);
        }

        /// <summary>
        /// Scores a mission against diplomacy commitments already selected this turn.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposal">The mission proposal to score.</param>
        /// <param name="commitments">Diplomats and their selected destinations.</param>
        /// <param name="includeTravelTime">Whether to discount sabotage and espionage by their complete mission cycle.</param>
        /// <param name="travelTicks">Optional complete preparation and travel time for the team.</param>
        /// <returns>The current mission score.</returns>
        internal double Score(
            AITurnContext context,
            AIProposal proposal,
            IReadOnlyDictionary<Officer, Planet> commitments,
            bool includeTravelTime = true,
            long? travelTicks = null
        )
        {
            AIMissionStagingProposal staging = proposal as AIMissionStagingProposal;
            if (staging != null)
            {
                if (!staging.TryGetTravelTicks(context, out _, out long stagedTicks))
                    return 0;
                travelTicks = stagedTicks;
                proposal = staging.Mission;
            }

            if (proposal is AIAbortMissionProposal)
                return 0;

            if (
                context?.Faction == null
                || context.Game?.Config == null
                || context.Missions == null
                || proposal is not AIMissionProposal missionProposal
            )
                return 0;

            MissionOdds odds =
                missionProposal.OperationalOdds
                ?? context.Assessment.GetOperationalMissionOdds(
                    context.MissionQueries,
                    missionProposal.CreateContext()
                );
            if (odds == null)
                return 0;

            bool isSabotage = missionProposal.MissionTypeID == MissionTypeIDs.Sabotage;
            bool usesMissionTime =
                isSabotage || missionProposal.MissionTypeID == MissionTypeIDs.Espionage;
            double successProbability = usesMissionTime
                ? odds.OverallSuccessProbability
                : odds.ObjectiveSuccessProbability;
            if (
                isSabotage
                && (
                    successProbability <= 0
                    || successProbability
                        < context.Game.Config.AI.MissionPlanning.MinimumSabotageSuccessPercent
                )
            )
                return 0;
            if (
                isSabotage
                && IsOvertakenByCommittedAttack(context, missionProposal, staging, travelTicks)
            )
                return 0;
            if (!MeetsUprisingMissionProbabilityFloor(context, missionProposal, successProbability))
                return 0;

            double foilProbability = odds.FoilProbability;
            missionProposal.SetFoilProbability(foilProbability);
            GameConfig.AIMissionUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility;
            AIUtilityScore score = GetMissionScore(
                context,
                missionProposal,
                successProbability,
                commitments,
                odds.ObjectiveSuccessProbability
            );
            AddMissionPriorityUtility(
                ref score,
                context,
                utility.Priority,
                missionProposal,
                odds.ObjectiveSuccessProbability
            );
            score.AddCost(
                AIUtility.Fulfillment(foilProbability, utility.Objective.FoilRisk),
                utility.Objective.FoilRisk
            );
            score.AddCost(
                AIUtility.Fulfillment(
                    usesMissionTime ? 0 : GetTravelCost(context, missionProposal),
                    utility.Objective.TravelCost
                ),
                utility.Objective.TravelCost
            );
            score.AddCost(
                HasOfficerReplacementRisk(context, missionProposal) ? 1 : 0,
                utility.Objective.OfficerRisk
            );
            long outboundTicks = 0;
            if (usesMissionTime && includeTravelTime)
            {
                long completeTravelTicks =
                    travelTicks ?? missionProposal.EstimateTravelTicks(context);
                outboundTicks = travelTicks.HasValue
                    ? Math.Max(
                        0,
                        completeTravelTicks
                            - missionProposal.EstimateReturnTravelTicks(
                                context,
                                staging?.Destination
                            )
                    )
                    : missionProposal.EstimateOutboundTravelTicks(context);
                AddMissionTimeCost(ref score, context, missionProposal, completeTravelTicks);
            }

            double value =
                missionProposal.MissionTypeID == MissionTypeIDs.Diplomacy
                    ? ApplyDiplomacyModifiers(
                        context,
                        missionProposal.TargetPlanet,
                        missionProposal.Participant,
                        score.RankValue
                    )
                    : score.RankValue;
            if (usesMissionTime && includeTravelTime)
                value *= GetElapsedDiscount(
                    outboundTicks + EstimateExecutionTicks(context, missionProposal),
                    utility.Objective.ExecutionDelay
                );
            return value >= context.Game.Config.AI.MissionPlanning.MinimumMissionScore ? value : 0;
        }

        /// <summary>
        /// Returns whether an uprising mission is viable before strategic priority is applied.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The mission proposal being evaluated.</param>
        /// <param name="successProbability">The calculated mission success probability.</param>
        /// <returns>True when the mission meets its feasibility requirement.</returns>
        private static bool MeetsUprisingMissionProbabilityFloor(
            AITurnContext context,
            AIMissionProposal proposal,
            double successProbability
        )
        {
            if (
                proposal.MissionTypeID != MissionTypeIDs.InciteUprising
                && proposal.MissionTypeID != MissionTypeIDs.SubdueUprising
            )
                return true;

            return successProbability
                >= context.Game.Config.AI.MissionPlanning.MinimumUprisingMissionSuccessPercent;
        }

        /// <summary>
        /// Returns the highest score a mission proposal can achieve before its odds are resolved.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The mission proposal to inspect.</param>
        /// <param name="departure">Optional assembly planet for personnel already in transit.</param>
        /// <returns>An upper bound for the proposal score.</returns>
        public double GetScoreUpperBound(
            AITurnContext context,
            AIMissionProposal proposal,
            Planet departure = null
        )
        {
            if (context?.Faction == null || context.Game?.Config == null || proposal == null)
                return 0;

            GameConfig.AIMissionUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility;
            AIUtilityScore score = GetMissionScore(context, proposal, successProbability: 100);
            AddMissionPriorityUtility(ref score, context, utility.Priority, proposal);
            score.AddCost(0, utility.Objective.FoilRisk);
            score.AddCost(
                AIUtility.Fulfillment(
                    proposal.MissionTypeID is MissionTypeIDs.Sabotage or MissionTypeIDs.Espionage
                        ? 0
                        : GetTravelCost(context, proposal),
                    utility.Objective.TravelCost
                ),
                utility.Objective.TravelCost
            );
            score.AddCost(
                HasOfficerReplacementRisk(context, proposal) ? 1 : 0,
                utility.Objective.OfficerRisk
            );
            if (proposal.MissionTypeID is MissionTypeIDs.Sabotage or MissionTypeIDs.Espionage)
            {
                IMissionParticipant leader = proposal.Participant;
                Planet origin = departure ?? leader.GetParentOfType<Planet>();
                long minimumTravel =
                    origin?.InstanceID == proposal.TargetPlanet?.InstanceID ? 0
                    : origin == null || proposal.TargetPlanet == null ? 0
                    : context.MovementQueries.CalculateTransitTicks(
                        leader,
                        origin,
                        proposal.TargetPlanet
                    );
                if (departure != null)
                    minimumTravel += Math.Max(
                        0,
                        leader.GetTransitMovement()?.TicksRemaining() ?? 0
                    );
                AddMissionTimeCost(ref score, context, proposal, minimumTravel);
                return score.RankValue
                    * GetElapsedDiscount(
                        minimumTravel + EstimateExecutionTicks(context, proposal),
                        utility.Objective.ExecutionDelay
                    );
            }
            return proposal.MissionTypeID == MissionTypeIDs.Diplomacy
                ? ApplyDiplomacyModifiers(
                    context,
                    proposal.TargetPlanet,
                    proposal.Participant,
                    score.RankValue
                )
                : score.RankValue;
        }

        /// <summary>
        /// Returns the mission-specific objective score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="successProbability">The calculated success probability.</param>
        /// <param name="commitments">Diplomats and destinations already selected this turn.</param>
        /// <param name="intelligenceProbability">The chance of obtaining intelligence after reaching the objective.</param>
        /// <returns>The objective score before general bonuses and penalties.</returns>
        private AIUtilityScore GetMissionScore(
            AITurnContext context,
            AIMissionProposal proposal,
            double successProbability,
            IReadOnlyDictionary<Officer, Planet> commitments = null,
            double intelligenceProbability = 100
        )
        {
            GameConfig.AIMissionObjectiveUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility
                .Objective;
            AIUtilityScore score = new AIUtilityScore();
            score.Add(AIUtility.Fulfillment(successProbability, utility.Success), utility.Success);
            bool isDiplomacy = proposal.MissionTypeID == MissionTypeIDs.Diplomacy;
            bool isSabotage = proposal.MissionTypeID == MissionTypeIDs.Sabotage;
            double intelligenceSuccess =
                proposal.MissionTypeID == MissionTypeIDs.Espionage
                    ? intelligenceProbability / 100d
                    : 0;
            AddDiplomacyUtility(ref score, context, proposal, isDiplomacy, commitments);
            AddSabotageTargetUtility(
                ref score,
                context,
                isSabotage ? proposal.TargetPlanet : null,
                isSabotage ? proposal.SelectedTarget as IManufacturable : null
            );
            score.Add(
                GetIntelligenceNeed(context, proposal) * intelligenceSuccess,
                utility.IntelAge
            );
            score.Add(
                GetAttackPreparationIntelPriority(context, proposal) * intelligenceSuccess,
                utility.AttackPreparationIntel
            );
            score.Add(
                GetBlockedAttackIntelPriority(context, proposal) * intelligenceSuccess,
                utility.BlockedAttackIntel
            );
            score.Add(
                AIUtility.Fulfillment(
                    proposal.MissionTypeID == MissionTypeIDs.JediTraining
                        ? GetJediTrainingValue(proposal)
                        : 0,
                    utility.TrainingValue
                ),
                utility.TrainingValue
            );

            return score;
        }

        /// <summary>
        /// Returns the urgency of refreshing intelligence for an active attack target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The mission proposal to inspect.</param>
        /// <returns>Normalized campaign-intelligence urgency.</returns>
        private static double GetAttackPreparationIntelPriority(
            AITurnContext context,
            AIMissionProposal proposal
        )
        {
            if (
                proposal.MissionTypeID != MissionTypeIDs.Espionage
                || !context.Assessment.IsAttackPreparationTarget(proposal.TargetPlanet)
            )
                return 0;

            if (!context.Assessment.HasMissionDefenseIntelligence(proposal.TargetPlanet))
                return 1;

            int age = context.Assessment.GetPlanetIntelAge(proposal.TargetPlanet);
            int refreshInterval = Math.Max(
                1,
                context.Game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks
            );
            if (age < refreshInterval)
                return 0;

            return AIUtility.Fulfillment(
                age,
                Math.Max(
                    1,
                    context.Game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks
                )
            );
        }

        /// <summary>
        /// Returns intelligence urgency from staged fleets meeting known but not uncertain combat requirements.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposal">The intelligence mission to evaluate.</param>
        /// <returns>The greatest blocked fleet's combat fulfillment, or zero without a blocked fleet.</returns>
        private static double GetBlockedAttackIntelPriority(
            AITurnContext context,
            AIMissionProposal proposal
        )
        {
            if (
                proposal.MissionTypeID != MissionTypeIDs.Espionage
                || !context.Assessment.IsAttackPreparationTarget(proposal.TargetPlanet)
            )
                return 0;

            double priority = 0;
            foreach (Fleet fleet in context.Assessment.AttackOrderedFleets)
            {
                if (
                    fleet.Order.TargetPlanetId != proposal.TargetPlanet.InstanceID
                    || fleet.Order.Status
                        is not (FleetOrderStatus.Building or FleetOrderStatus.Staging)
                    || fleet.Movement != null
                    || fleet.IsInCombat
                )
                    continue;

                var demand = context.GetAttackDemand(proposal.TargetPlanet, fleet);
                if (demand == null)
                    continue;

                int knownRequirement = Math.Max(
                    context.Game.Config.AI.FleetDeployment.MinimumAttackStrength,
                    demand.OrbitalStrength
                );
                if (demand.CombatStrength <= knownRequirement)
                    continue;

                int available = context.Assessment.GetReadyFleetCombatValueAgainstCapitalShips(
                    fleet
                );
                if (available < knownRequirement || available >= demand.CombatStrength)
                    continue;

                priority = Math.Max(
                    priority,
                    AIUtility.Fulfillment(available, demand.CombatStrength)
                );
            }

            return priority;
        }

        /// <summary>
        /// Adds sabotage-target utility to a mission score.
        /// </summary>
        /// <param name="score">The score value.</param>
        /// <param name="context">The context value.</param>
        /// <param name="planet">The planet value.</param>
        /// <param name="target">The target value.</param>
        private static void AddSabotageTargetUtility(
            ref AIUtilityScore score,
            AITurnContext context,
            Planet planet,
            IManufacturable target
        )
        {
            GameConfig.AISabotageUtilityConfig utility = context
                ?.Game
                ?.Config
                ?.AI
                ?.MissionPlanning
                ?.Utility
                ?.Sabotage;
            if (utility == null)
                return;

            bool isAttackTarget =
                planet != null
                && target != null
                && context.Assessment.IsAttackPreparationTarget(planet);
            Building building = target as Building;
            bool isPlanetaryDefense = IsPlanetaryDefenseBuilding(building);
            bool isGarrisonRegiment = target is Regiment && IsGarrisonedAtPlanet(planet, target);
            bool isGarrisonStarfighter =
                target is Starfighter && IsGarrisonedAtPlanet(planet, target);
            score.Add(
                building?.GetBuildingType()
                    is BuildingType.Shipyard
                        or BuildingType.TrainingFacility
                        or BuildingType.ConstructionFacility
                    ? 1
                    : 0,
                utility.Infrastructure
            );
            score.Add(isPlanetaryDefense ? 1 : 0, utility.Defense);
            score.Add(building?.IsShieldGenerator() == true ? 1 : 0, utility.Shield);
            score.Add(isAttackTarget ? 1 : 0, utility.AttackTarget);
            score.Add(isAttackTarget && isPlanetaryDefense ? 1 : 0, utility.AttackDefense);
            score.Add(
                isGarrisonRegiment && context.Assessment.IsGarrisonSabotageCritical(planet) ? 1 : 0,
                utility.FavoredSupportRegiment
            );
            score.Add(isGarrisonRegiment ? 1 : 0, utility.GarrisonRegiment);
            score.Add(isGarrisonStarfighter ? 1 : 0, utility.GarrisonStarfighter);
            score.Add(target is SpecialForces ? 1 : 0, utility.SpecialForces);
            score.Add(
                building == null
                && !isGarrisonRegiment
                && !isGarrisonStarfighter
                && target is not SpecialForces
                    ? 1
                    : 0,
                utility.OtherUnit
            );
        }

        /// <summary>
        /// Calculates the normalized utility of a sabotage target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The target planet.</param>
        /// <param name="target">The target unit or facility.</param>
        /// <returns>The target utility from zero through one.</returns>
        internal static double GetSabotageTargetValue(
            AITurnContext context,
            Planet planet,
            IManufacturable target
        )
        {
            AIUtilityScore score = new AIUtilityScore();
            AddSabotageTargetUtility(ref score, context, planet, target);
            return score.Value;
        }

        /// <summary>
        /// Values industrial opportunities before discounting travel and diplomatic competition.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="planet">The proposed target.</param>
        /// <param name="participant">The assigned diplomat, or null for target ordering.</param>
        /// <param name="value">The utility before diplomatic costs.</param>
        /// <returns>The utility after travel costs and applicable competition costs.</returns>
        private static double ApplyDiplomacyModifiers(
            AITurnContext context,
            Planet planet,
            IMissionParticipant participant,
            double value
        )
        {
            if (context?.Assessment == null || planet == null)
                return value;

            GameConfig.AIDiplomacyUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility
                .Diplomacy;
            if (
                utility.IndustrialOpportunity.Weight > 0
                && context.Assessment.IsNeutralPlanet(planet)
            )
            {
                double opportunity = Math.Clamp(
                    AIUtility.Evaluate(
                        context.StrategicPlan.GetInfrastructureAcquisitionValue(planet),
                        utility.IndustrialOpportunity
                    ),
                    0,
                    1
                );
                value += (1 - value) * opportunity;
            }
            if (utility.Competition.Weight > 0 && context.Assessment.IsNeutralPlanet(planet))
            {
                double competition = context.Assessment.GetEnemyDiplomacyStrengthInSystem(
                    planet.GetParentOfType<PlanetSector>()?.InstanceID
                );
                value *=
                    1
                    - AIUtility.Evaluate(
                        AIUtility.Fulfillment(
                            competition,
                            competition + Math.Max(1, utility.Competition.SaturationValue)
                        ),
                        utility.Competition
                    );
            }
            if (
                utility.TravelTime.Weight > 0
                && participant?.GetParentOfType<Planet>() is Planet departure
            )
            {
                int ticks =
                    departure.InstanceID == planet.InstanceID
                        ? 0
                        : context.MovementQueries.CalculateTransitTicks(
                            participant,
                            departure,
                            planet
                        );
                value *=
                    1
                    - AIUtility.Evaluate(
                        AIUtility.Fulfillment(
                            ticks,
                            ticks + Math.Max(1, utility.TravelTime.SaturationValue)
                        ),
                        utility.TravelTime
                    );
            }
            return value;
        }

        /// <summary>
        /// Returns whether a target is directly stationed on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="target">The target unit.</param>
        /// <returns>True when the target is a direct child of the planet.</returns>
        private static bool IsGarrisonedAtPlanet(Planet planet, IManufacturable target)
        {
            return target.GetParent() is Planet parent && parent.InstanceID == planet.InstanceID;
        }

        /// <summary>
        /// Returns whether a building contributes to planetary defense.
        /// </summary>
        /// <param name="building">The building to inspect.</param>
        /// <returns>True for shield and weapon facilities.</returns>
        private static bool IsPlanetaryDefenseBuilding(Building building)
        {
            return building?.GetBuildingType() is BuildingType.Defense or BuildingType.Weapon;
        }

        /// <summary>
        /// Scores diplomacy success and the support deficit it can reduce.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="score">The score receiving diplomacy considerations.</param>
        /// <param name="enabled">Whether the proposal is a diplomacy mission.</param>
        /// <param name="commitments">Diplomats and destinations already selected this turn.</param>
        private static void AddDiplomacyUtility(
            ref AIUtilityScore score,
            AITurnContext context,
            AIMissionProposal proposal,
            bool enabled,
            IReadOnlyDictionary<Officer, Planet> commitments
        )
        {
            if (!enabled)
                return;

            AddDiplomacyTargetUtility(
                ref score,
                context,
                proposal.TargetPlanet,
                proposal.Participant,
                commitments
            );
        }

        /// <summary>
        /// Returns the strategic value of one diplomacy target.
        /// </summary>
        /// <param name="context">Current faction turn.</param>
        /// <param name="planet">Diplomacy target to evaluate.</param>
        /// <returns>The normalized diplomacy-target value.</returns>
        internal static double GetDiplomacyTargetValue(AITurnContext context, Planet planet)
        {
            AIUtilityScore score = new AIUtilityScore();
            AddDiplomacyTargetUtility(ref score, context, planet);
            return ApplyDiplomacyModifiers(context, planet, null, score.Value);
        }

        /// <summary>
        /// Adds support, acquisition, and strategic-infrastructure value for one diplomacy target.
        /// </summary>
        /// <param name="score">Score receiving diplomacy considerations.</param>
        /// <param name="context">Current faction turn.</param>
        /// <param name="planet">Diplomacy target to evaluate.</param>
        /// <param name="participant">The diplomat receiving the score, or null for target ordering.</param>
        /// <param name="commitments">Diplomats and destinations already selected this turn.</param>
        private static void AddDiplomacyTargetUtility(
            ref AIUtilityScore score,
            AITurnContext context,
            Planet planet,
            IMissionParticipant participant = null,
            IReadOnlyDictionary<Officer, Planet> commitments = null
        )
        {
            if (context?.Assessment == null || context.StrategicPlan == null || planet == null)
                return;

            GameConfig.AIDiplomacyUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility
                .Diplomacy;
            int opposingSupport = planet.GetOpposingPopularSupport(context.Faction.InstanceID);
            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            bool isCoreWorld = sector?.SectorType == PlanetSectorType.Core;
            score.Add(isCoreWorld ? 1 : 0, utility.CoreWorld);
            PlanetSector headquartersSector =
                context.Assessment.FactionHeadquarters?.GetParentOfType<PlanetSector>();
            if (headquartersSector != null && sector?.InstanceID == headquartersSector.InstanceID)
                score.Add(1, utility.HeadquartersSector);
            if (utility.SystemPresence.Weight > 0)
                score.Add(
                    context.Assessment.GetOwnedSystemPresenceRatio(sector?.InstanceID),
                    utility.SystemPresence
                );

            if (utility.Concentration.Weight > 0)
            {
                double concentration = context.StrategicPlan.GetDiplomacyConcentrationValue(
                    planet,
                    context.MovementQueries,
                    participant,
                    commitments
                );
                if (concentration > 0)
                    score.Add(concentration, utility.Concentration);
            }

            if (context.Assessment.IsOwnedPlanet(planet))
            {
                score.Add(
                    AIUtility.Fulfillment(opposingSupport, utility.SupportDeficit),
                    utility.SupportDeficit
                );
                score.Add(
                    AIUtility.Fulfillment(
                        context.Assessment.GetDefensiveSupportRisk(planet),
                        utility.SectorSupportRisk
                    ),
                    utility.SectorSupportRisk
                );
                return;
            }

            if (!context.Assessment.IsNeutralPlanet(planet))
                return;

            score.Add(
                AIUtility.Fulfillment(opposingSupport, utility.SupportDeficit),
                utility.SupportDeficit
            );
            AddInfrastructureAcquisitionUtility(
                ref score,
                context,
                planet,
                BuildingType.ConstructionFacility,
                ManufacturingType.Building,
                utility.ConstructionFacility
            );
            AddInfrastructureAcquisitionUtility(
                ref score,
                context,
                planet,
                BuildingType.Shipyard,
                ManufacturingType.Ship,
                utility.Shipyard
            );
            AddInfrastructureAcquisitionUtility(
                ref score,
                context,
                planet,
                BuildingType.TrainingFacility,
                ManufacturingType.Troop,
                utility.TrainingFacility
            );

            if (
                context.Assessment.ProjectedMaintenanceHeadroom
                < context.Game.Config.AI.Selection.MaintenanceHeadroomReserve
            )
                score.Add(
                    AIUtility.Fulfillment(planet.GetRawResourceNodes(), utility.ResourceNode),
                    utility.ResourceNode
                );
        }

        /// <summary>
        /// Adds the value of acquiring facilities that satisfy a current infrastructure deficit.
        /// </summary>
        /// <param name="score">Score receiving the acquisition value.</param>
        /// <param name="context">Current faction turn.</param>
        /// <param name="planet">Neutral planet being evaluated.</param>
        /// <param name="buildingType">Infrastructure category supplied by the planet.</param>
        /// <param name="manufacturingType">Manufacturing capability used to count facilities.</param>
        /// <param name="consideration">Configured utility consideration.</param>
        private static void AddInfrastructureAcquisitionUtility(
            ref AIUtilityScore score,
            AITurnContext context,
            Planet planet,
            BuildingType buildingType,
            ManufacturingType manufacturingType,
            GameConfig.AIConsiderationConfig consideration
        )
        {
            double deficitRatio = context.StrategicPlan.GetInfrastructureDeficitRatio(buildingType);
            double supply = AIUtility.Fulfillment(
                planet.GetProductionFacilityCount(manufacturingType),
                consideration
            );
            score.Add(supply * deficitRatio, consideration);
        }

        /// <summary>
        /// Returns the total Force-rank improvement available to training students.
        /// </summary>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The potential Force-rank gain.</returns>
        private double GetJediTrainingValue(AIMissionProposal proposal)
        {
            List<Officer> officers = proposal.Participants.OfType<Officer>().ToList();
            Officer trainer = officers
                .OrderByDescending(officer => officer.ForceRank)
                .FirstOrDefault();
            if (trainer == null)
                return 0;

            return officers
                .Where(officer => officer != trainer)
                .Sum(officer => Math.Max(0, trainer.ForceRank - officer.ForceRank));
        }

        /// <summary>
        /// Returns the configured strategic-priority bonus for a mission type.
        /// </summary>
        /// <param name="score">The score receiving mission-priority considerations.</param>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="utility">The applicable utility configuration.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="successProbability">The probability that an intelligence objective is achieved.</param>
        private static void AddMissionPriorityUtility(
            ref AIUtilityScore score,
            AITurnContext context,
            GameConfig.AIMissionPriorityUtilityConfig utility,
            AIMissionProposal proposal,
            double successProbability = 100
        )
        {
            string missionTypeId = proposal.MissionTypeID;
            score.Add(
                missionTypeId == MissionTypeIDs.Reconnaissance ? 1 : 0,
                utility.Reconnaissance
            );
            score.Add(missionTypeId == MissionTypeIDs.Recruitment ? 1 : 0, utility.Recruitment);
            score.Add(missionTypeId == MissionTypeIDs.Rescue ? 1 : 0, utility.Rescue);
            score.Add(
                missionTypeId == MissionTypeIDs.SubdueUprising ? 1 : 0,
                utility.SubdueUprising
            );
            score.Add(missionTypeId == MissionTypeIDs.Research ? 1 : 0, utility.Research);
            score.Add(missionTypeId == MissionTypeIDs.JediTraining ? 1 : 0, utility.JediTraining);
            score.Add(
                missionTypeId == MissionTypeIDs.Espionage ? successProbability / 100d : 0,
                utility.Espionage
            );
            score.Add(
                missionTypeId == MissionTypeIDs.Espionage
                && proposal.Participant is SpecialForces specialForces
                && context.IsHeadquartersEspionageAgent(specialForces)
                && proposal.TargetPlanet == context.Assessment.FactionHeadquarters
                    ? successProbability / 100d
                    : 0,
                utility.MobileHeadquartersEspionage
            );
            score.Add(missionTypeId == MissionTypeIDs.Diplomacy ? 1 : 0, utility.Diplomacy);
        }

        /// <summary>
        /// Penalizes risking an officer on hostile work that unlocked special forces can perform.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The mission proposal being evaluated.</param>
        /// <returns>True when the officer has an unlocked special-forces replacement.</returns>
        private static bool HasOfficerReplacementRisk(
            AITurnContext context,
            AIMissionProposal proposal
        )
        {
            if (
                (
                    proposal.IsTeamFinal
                        ? !proposal.MainParticipants.OfType<Officer>().Any()
                        : proposal.Participant is not Officer
                )
                || proposal.TargetPlanet?.GetOwnerInstanceID() == null
                || proposal.TargetPlanet.GetOwnerInstanceID() == context.Faction.InstanceID
                || !context.HasUnlockedSpecialForcesForMission(proposal.MissionTypeID)
            )
                return false;

            return true;
        }

        /// <summary>
        /// Avoids starting sabotage that a capable, already committed attack is expected to overtake.
        /// </summary>
        /// <param name="context">The current faction turn and measured attack requirements.</param>
        /// <param name="mission">The sabotage objective under consideration.</param>
        /// <param name="staging">Optional preparation before starting the mission.</param>
        /// <param name="travelTicks">Optional complete preparation and round-trip travel.</param>
        /// <returns>True when a committed fleet can address the defenses before the mission does.</returns>
        private static bool IsOvertakenByCommittedAttack(
            AITurnContext context,
            AIMissionProposal mission,
            AIMissionStagingProposal staging,
            long? travelTicks
        )
        {
            Planet target = mission.TargetPlanet;
            double objectiveTicks =
                (travelTicks ?? mission.EstimateTravelTicks(context))
                - mission.EstimateReturnTravelTicks(context, staging?.Destination)
                + EstimateExecutionTicks(context, mission);
            foreach (Fleet fleet in context.Assessment.AttackOrderedFleets)
            {
                if (
                    fleet.Order.TargetPlanetId != target.InstanceID
                    || fleet.Order.Status == FleetOrderStatus.Returning
                    || fleet.IsInCombat
                    || context.Assessment.GetFleetPlanet(fleet)?.InstanceID != target.InstanceID
                )
                    continue;

                int decisionTicks = Math.Max(1, context.Game.Config.AI.TickInterval);
                int captureTicks = (fleet.Movement?.TicksRemaining() ?? 0) + decisionTicks;
                if (fleet.Order.Status != FleetOrderStatus.Ready)
                    captureTicks += decisionTicks;
                if (captureTicks >= objectiveTicks)
                    continue;

                var demand = context.GetAttackDemand(target, fleet);
                if (demand == null || !context.Game.Config.AI.EnablePlanetaryAssaults)
                    continue;
                bool canBombard = context.Assessment.CanBombardMilitaryTargets(fleet, target);
                if (
                    context.Assessment.GetReadyFleetCombatValueAgainstCapitalShips(fleet)
                        >= demand.CombatStrength
                    && !demand.IsAssaultBlockedByShields
                    && !canBombard
                    && context.Assessment.GetReadyFleetRegimentCount(fleet) >= demand.RegimentCount
                    && context.Assessment.GetReadyFleetRegimentAttackStrength(fleet)
                        >= demand.RegimentStrength
                    && context.Assessment.GetPlanetaryAssaultSuccessPercent(fleet, target)
                        >= context
                            .Game
                            .Config
                            .AI
                            .FleetDeployment
                            .MinimumPlanetaryAssaultSuccessPercent
                )
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Estimates execution time using the mission's configured duration and faction difficulty.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="mission">The mission whose duration is required.</param>
        /// <returns>The expected execution duration after the team arrives.</returns>
        private static double EstimateExecutionTicks(
            AITurnContext context,
            AIMissionProposal mission
        )
        {
            return context.MissionQueries.EstimateExecutionTicks(
                mission.MissionTypeID,
                context.Faction.InstanceID
            );
        }

        /// <summary>
        /// Ranks personnel by skill discounted by the time their team needs to reach its objective.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="participant">The available or currently arriving participant.</param>
        /// <param name="target">The observed mission planet.</param>
        /// <param name="skill">The participant's rating or objective probability for the role.</param>
        /// <param name="teamTravelTicks">The outbound duration already required by the team.</param>
        /// <param name="departure">An optional common future departure planet.</param>
        /// <returns>The skill value discounted by configured travel costs.</returns>
        internal static double GetParticipantPriority(
            AITurnContext context,
            IMissionParticipant participant,
            Planet target,
            double skill,
            long teamTravelTicks = 0,
            Planet departure = null
        )
        {
            GameConfig.AIConsiderationConfig cost = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility
                .Objective
                .ParticipantTravelTime;
            if (cost == null || cost.Weight <= 0)
                return skill;

            Planet origin = departure ?? participant.GetParentOfType<Planet>();
            long travel =
                origin.InstanceID == target.InstanceID
                    ? 0
                    : context.MovementQueries.CalculateTransitTicks(participant, origin, target);
            travel += Math.Max(0, participant.GetTransitMovement()?.TicksRemaining() ?? 0);
            return skill * GetElapsedDiscount(Math.Max(teamTravelTicks, travel), cost);
        }

        /// <summary>
        /// Returns the configured discount for delaying useful work.
        /// </summary>
        /// <param name="ticks">The estimated delay.</param>
        /// <param name="consideration">The optional travel-delay consideration.</param>
        /// <returns>A multiplier from zero through one, with one preserving the current value.</returns>
        private static double GetElapsedDiscount(
            double ticks,
            GameConfig.AIConsiderationConfig consideration
        )
        {
            if (consideration == null || consideration.Weight <= 0)
                return 1;
            double periods = ticks / Math.Max(1, consideration.SaturationValue);
            double completePeriods = Math.Floor(periods);
            double delay =
                completePeriods
                + AIUtility.EvaluateCurve(periods - completePeriods, consideration.Curve);
            return 1 / (1 + consideration.Weight * delay);
        }

        /// <summary>
        /// Adds complete mission-cycle duration to the shared travel costs.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="mission">The mission opportunity being compared.</param>
        /// <param name="score">The mission utility receiving the elapsed-time cost.</param>
        /// <param name="travelTicks">Waiting and travel until the complete team returns.</param>
        private static void AddMissionTimeCost(
            ref AIUtilityScore score,
            AITurnContext context,
            AIMissionProposal mission,
            long travelTicks
        )
        {
            GameConfig.AIMissionObjectiveUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility
                .Objective;
            GameConfig.AIConsiderationConfig cost = utility.TravelTime ?? utility.TravelCost;
            double missionTicks = EstimateExecutionTicks(context, mission);
            AddElapsedTimeCost(ref score, travelTicks + missionTicks, cost);
        }

        /// <summary>
        /// Adds a configured duration cost without capping long journeys at one cost unit.
        /// </summary>
        /// <param name="score">The mission utility receiving the duration penalty.</param>
        /// <param name="ticks">The estimated duration.</param>
        /// <param name="cost">The optional duration consideration.</param>
        private static void AddElapsedTimeCost(
            ref AIUtilityScore score,
            double ticks,
            GameConfig.AIConsiderationConfig cost
        )
        {
            if (cost == null || cost.Weight <= 0)
                return;
            double periods = ticks / Math.Max(1, cost.SaturationValue);
            double completePeriods = Math.Floor(periods);
            score.AddCost(1, cost, completePeriods);
            score.AddCost(periods - completePeriods, cost);
        }

        /// <summary>
        /// Returns the longest participant travel distance as a score penalty.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The normalized travel penalty.</returns>
        private static double GetTravelCost(AITurnContext context, AIMissionProposal proposal)
        {
            double distanceDivisor = context.Game.Config.Movement.DistanceDivisor;
            if (proposal.TargetPlanet == null || distanceDivisor <= 0)
                return 0;

            return proposal
                    .Participants.Select(participant =>
                        participant
                            .GetParentOfType<Planet>()
                            ?.GetRawDistanceTo(proposal.TargetPlanet)
                        ?? 0
                    )
                    .DefaultIfEmpty()
                    .Max() / distanceDivisor;
        }

        /// <summary>
        /// Values obtaining missing military information or refreshing an aging observation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The intelligence mission to evaluate.</param>
        /// <returns>The normalized need for new military information.</returns>
        private static double GetIntelligenceNeed(AITurnContext context, AIMissionProposal proposal)
        {
            if (proposal.MissionTypeID != MissionTypeIDs.Espionage)
                return 0;

            int tickInterval = context.Game.Config.AI.TickInterval;
            int age = context.Assessment.GetPlanetIntelAge(proposal.TargetPlanet);
            if (!context.Assessment.HasMissionDefenseIntelligence(proposal.TargetPlanet))
                age = Math.Max(
                    age,
                    context.Game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks
                );
            return tickInterval > 0
                ? AIUtility.Fulfillment(
                    (double)age / tickInterval,
                    context.Game.Config.AI.MissionPlanning.Utility.Objective.IntelAge
                )
                : 0;
        }
    }
}
