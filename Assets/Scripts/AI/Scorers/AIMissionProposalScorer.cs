using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Systems;

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
            return proposal is AIMissionProposal or AIAbortMissionProposal;
        }

        /// <summary>
        /// Returns the mission proposal score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>The mission proposal score.</returns>
        public double Score(AITurnContext context, AIProposal proposal)
        {
            if (proposal is AIAbortMissionProposal)
                return 0;

            if (
                context?.Faction == null
                || context.Game?.Config == null
                || context.Missions == null
                || proposal is not AIMissionProposal missionProposal
            )
                return 0;

            MissionOdds odds = context.Missions.GetMissionOdds(
                missionProposal.CreateRequest(),
                context.Assessment.GetMissionDetectorCandidates(missionProposal.TargetPlanet)
            );
            if (odds == null)
                return 0;

            double successProbability = odds.ObjectiveSuccessProbability;
            if (!MeetsUprisingMissionProbabilityFloor(context, missionProposal, successProbability))
                return 0;

            double foilProbability = odds.FoilProbability;
            missionProposal.SetFoilProbability(foilProbability);
            missionProposal.SetPersonnelLossProbability(odds.PersonnelLossProbability);
            GameConfig.AIMissionUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility;
            AIUtilityScore score = GetMissionScore(context, missionProposal, successProbability);
            AddMissionPriorityUtility(ref score, utility.Priority, missionProposal);
            score.AddCost(
                AIUtility.Fulfillment(foilProbability, utility.Objective.FoilRisk),
                utility.Objective.FoilRisk
            );
            score.AddCost(
                AIUtility.Fulfillment(
                    GetTravelCost(context, missionProposal),
                    utility.Objective.TravelCost
                ),
                utility.Objective.TravelCost
            );
            score.AddCost(
                HasOfficerReplacementRisk(context, missionProposal) ? 1 : 0,
                utility.Objective.OfficerRisk
            );

            return score.RankValue >= context.Game.Config.AI.MissionPlanning.MinimumMissionScore
                ? score.RankValue
                : 0;
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
        /// <returns>An upper bound for the proposal score.</returns>
        public double GetScoreUpperBound(AITurnContext context, AIMissionProposal proposal)
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
            AddMissionPriorityUtility(ref score, utility.Priority, proposal);
            score.AddCost(0, utility.Objective.FoilRisk);
            score.AddCost(
                AIUtility.Fulfillment(
                    GetTravelCost(context, proposal),
                    utility.Objective.TravelCost
                ),
                utility.Objective.TravelCost
            );
            score.AddCost(
                HasOfficerReplacementRisk(context, proposal) ? 1 : 0,
                utility.Objective.OfficerRisk
            );
            return score.RankValue;
        }

        /// <summary>
        /// Returns the mission-specific objective score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="successProbability">The calculated success probability.</param>
        /// <returns>The objective score before general bonuses and penalties.</returns>
        private AIUtilityScore GetMissionScore(
            AITurnContext context,
            AIMissionProposal proposal,
            double successProbability
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
            AddDiplomacyUtility(ref score, context, proposal, isDiplomacy);
            AddSabotageTargetUtility(
                ref score,
                context,
                isSabotage ? proposal.TargetPlanet : null,
                isSabotage ? proposal.SelectedTarget as IManufacturable : null
            );
            score.Add(
                AIUtility.Fulfillment(
                    proposal.MissionTypeID == MissionTypeIDs.Espionage
                        ? GetIntelAge(context, proposal)
                        : 0,
                    utility.IntelAge
                ),
                utility.IntelAge
            );
            score.Add(
                GetAttackPreparationIntelPriority(context, proposal),
                utility.AttackPreparationIntel
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
            score.Add(building != null ? 1 : 0, utility.Infrastructure);
            score.Add(isPlanetaryDefense ? 1 : 0, utility.Defense);
            score.Add(building?.IsShieldGenerator() == true ? 1 : 0, utility.Shield);
            score.Add(isAttackTarget ? 1 : 0, utility.AttackTarget);
            score.Add(isAttackTarget && isPlanetaryDefense ? 1 : 0, utility.AttackDefense);
            score.Add(
                isGarrisonRegiment && IsGarrisonSabotageCritical(context, planet) ? 1 : 0,
                utility.FavoredSupportRegiment
            );
            score.Add(isGarrisonRegiment ? 1 : 0, utility.GarrisonRegiment);
            score.Add(isGarrisonStarfighter ? 1 : 0, utility.GarrisonStarfighter);
            score.Add(
                building == null && !isGarrisonRegiment && !isGarrisonStarfighter ? 1 : 0,
                utility.OtherUnit
            );
        }

        /// <summary>
        /// Returns whether destroying one enemy garrison regiment would destabilize the planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The hostile planet to inspect.</param>
        /// <returns>True when one loss starts an uprising or transfers control to the AI faction.</returns>
        private static bool IsGarrisonSabotageCritical(AITurnContext context, Planet planet)
        {
            string ownerInstanceId = planet?.GetOwnerInstanceID();
            if (
                string.IsNullOrEmpty(ownerInstanceId)
                || ownerInstanceId == context?.Faction?.InstanceID
            )
                return false;

            int activeGarrisonCount = context.Assessment.GetActiveGarrisonCount(
                planet,
                ownerInstanceId
            );
            if (activeGarrisonCount <= 0)
                return false;

            Faction owner = context.Game.GetFactionByOwnerInstanceID(ownerInstanceId);
            int stabilityRequirement =
                owner == null
                    ? 0
                    : UprisingSystem.CalculateGarrisonRequirement(
                        planet,
                        owner,
                        context.Game.Config.AI.Garrison
                    );
            return activeGarrisonCount - 1 < stabilityRequirement
                || activeGarrisonCount == 1 && context.Assessment.HasFactionControlSupport(planet);
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
        private static void AddDiplomacyUtility(
            ref AIUtilityScore score,
            AITurnContext context,
            AIMissionProposal proposal,
            bool enabled
        )
        {
            int opposingSupport = enabled
                ? proposal.TargetPlanet?.GetOpposingPopularSupport(context.Faction.InstanceID) ?? 0
                : 0;
            GameConfig.AIDiplomacyUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility
                .Diplomacy;
            bool isCoreWorld =
                enabled
                && proposal.TargetPlanet?.GetParentOfType<PlanetSector>()?.SectorType
                    == PlanetSectorType.Core;
            score.Add(isCoreWorld ? 1 : 0, utility.CoreWorld);
            score.Add(
                AIUtility.Fulfillment(opposingSupport, utility.SupportDeficit),
                utility.SupportDeficit
            );
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
        /// <param name="utility">The applicable utility configuration.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The mission-type priority bonus.</returns>
        private static void AddMissionPriorityUtility(
            ref AIUtilityScore score,
            GameConfig.AIMissionPriorityUtilityConfig utility,
            AIMissionProposal proposal
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
            score.Add(missionTypeId == MissionTypeIDs.Espionage ? 1 : 0, utility.Espionage);
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
                proposal.Participant is not Officer
                || proposal.TargetPlanet?.GetOwnerInstanceID() == null
                || proposal.TargetPlanet.GetOwnerInstanceID() == context.Faction.InstanceID
                || !context.HasUnlockedSpecialForcesForMission(proposal.MissionTypeID)
            )
                return false;

            return true;
        }

        /// <summary>
        /// Returns the longest participant travel distance as a score penalty.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The normalized travel penalty.</returns>
        private static double GetTravelCost(AITurnContext context, AIMissionProposal proposal)
        {
            double distanceScale = context.Game.Config.Movement.DistanceScale;
            if (proposal.TargetPlanet == null || distanceScale <= 0)
                return 0;

            return proposal
                    .Participants.Select(participant =>
                        participant
                            .GetParentOfType<Planet>()
                            ?.GetRawDistanceTo(proposal.TargetPlanet)
                        ?? 0
                    )
                    .DefaultIfEmpty()
                    .Max() / distanceScale;
        }

        /// <summary>
        /// Returns the value of refreshing stale intelligence for an espionage mission.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The intelligence age measured in AI-turn intervals.</returns>
        private static double GetIntelAge(AITurnContext context, AIMissionProposal proposal)
        {
            int tickInterval = context.Game.Config.AI.TickInterval;
            int age = context.Assessment.GetPlanetIntelAge(proposal.TargetPlanet);
            return tickInterval > 0 && age < int.MaxValue ? (double)age / tickInterval : 0;
        }
    }
}
