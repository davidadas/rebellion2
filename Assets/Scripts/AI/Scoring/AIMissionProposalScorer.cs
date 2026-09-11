using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Scores mission proposals.
    /// </summary>
    public sealed class AIMissionProposalScorer : IAIProposalScorer
    {
        private const double _maximumSuccessProbability = 100;

        /// <summary>
        /// Returns whether this scorer can score the proposal.
        /// </summary>
        /// <param name="proposal">The proposal to check.</param>
        /// <returns>True if the proposal is a mission proposal.</returns>
        public bool CanScore(AIProposal proposal)
        {
            return proposal is AIMissionProposal;
        }

        /// <summary>
        /// Returns the mission proposal score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>The mission proposal score.</returns>
        public double Score(AITurnContext context, AIProposal proposal)
        {
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

            if (
                missionProposal.MainParticipants.OfType<Officer>().Any()
                && odds.PersonnelLossProbability
                    > context.Game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability
            )
                return 0;

            double successProbability = odds.ObjectiveSuccessProbability;
            if (!MeetsUprisingMissionProbabilityFloor(context, missionProposal, successProbability))
                return 0;

            double foilProbability = odds.FoilProbability;
            missionProposal.SetFoilProbability(foilProbability);
            double score = GetMissionScore(context, missionProposal, successProbability);
            score += GetPriorityBonus(context.Game.Config.AI.MissionPlanning, missionProposal);
            score -= foilProbability * context.Game.Config.AI.MissionPlanning.MissionFoilRiskWeight;
            score -= GetTravelPenalty(context, missionProposal);

            return score >= context.Game.Config.AI.MissionPlanning.MinimumMissionScore ? score : 0;
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
            if (context?.Game?.Config == null || proposal == null)
                return 0;

            double score = GetMissionScore(context, proposal, _maximumSuccessProbability);
            score += GetPriorityBonus(context.Game.Config.AI.MissionPlanning, proposal);
            score -= GetTravelPenalty(context, proposal);
            return score;
        }

        /// <summary>
        /// Returns the mission-specific objective score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="successProbability">The calculated success probability.</param>
        /// <returns>The objective score before general bonuses and penalties.</returns>
        private double GetMissionScore(
            AITurnContext context,
            AIMissionProposal proposal,
            double successProbability
        )
        {
            return proposal.MissionTypeID switch
            {
                MissionTypeIDs.Diplomacy => ScoreDiplomacy(context, proposal, successProbability),
                MissionTypeIDs.Sabotage => ScoreSabotage(context, proposal, successProbability),
                MissionTypeIDs.Espionage => successProbability
                    + GetIntelAgeScore(context, proposal),
                MissionTypeIDs.JediTraining => successProbability + GetJediTrainingValue(proposal),
                _ => successProbability,
            };
        }

        /// <summary>
        /// Scores sabotage success and the strategic value of its selected target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="successProbability">The calculated success probability.</param>
        /// <returns>The sabotage objective score.</returns>
        private double ScoreSabotage(
            AITurnContext context,
            AIMissionProposal proposal,
            double successProbability
        )
        {
            return successProbability
                + GetSabotagePriorityBonus(
                    context,
                    proposal.TargetPlanet,
                    proposal.SelectedTarget as IManufacturable
                );
        }

        /// <summary>
        /// Calculates the configured scoring bonus for a sabotage target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The target planet.</param>
        /// <param name="target">The target unit or facility.</param>
        /// <returns>The target's scoring bonus.</returns>
        internal static int GetSabotagePriorityBonus(
            AITurnContext context,
            Planet planet,
            IManufacturable target
        )
        {
            GameConfig.AIMissionPlanningConfig config = context?.Game?.Config?.AI?.MissionPlanning;
            if (config == null || planet == null || target == null)
                return 0;

            bool isAttackTarget = context.Assessment.IsAttackPreparationTarget(planet);
            if (target is Building building)
            {
                int priorityBonus = config.SabotageInfrastructureBonus;
                if (IsPlanetaryDefenseBuilding(building))
                    priorityBonus += config.SabotageDefenseBonus;

                if (building.IsShieldGenerator())
                    priorityBonus += config.SabotageShieldBonus;

                if (isAttackTarget && IsPlanetaryDefenseBuilding(building))
                {
                    priorityBonus +=
                        config.SabotageAttackTargetBonus + config.SabotageAttackDefenseBonus;
                }

                return priorityBonus;
            }

            int unitPriorityBonus = target switch
            {
                Regiment when IsGarrisonedAtPlanet(planet, target) =>
                    config.SabotageGarrisonRegimentBonus
                        + (
                            HasOppositionSupportMajority(context, planet)
                                ? config.SabotageFavoredSupportRegimentBonus
                                : 0
                        ),
                Starfighter when IsGarrisonedAtPlanet(planet, target) =>
                    config.SabotageGarrisonStarfighterBonus,
                _ => config.SabotageOtherUnitBonus,
            };
            return isAttackTarget
                ? unitPriorityBonus + config.SabotageAttackTargetBonus
                : unitPriorityBonus;
        }

        /// <summary>
        /// Returns whether the AI faction has more support than the planet's owner.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when opposition support exceeds owner support.</returns>
        private static bool HasOppositionSupportMajority(AITurnContext context, Planet planet)
        {
            string ownerInstanceId = planet?.GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerInstanceId)
                && context.Assessment.GetFactionPopularSupport(planet)
                    > planet.GetPopularSupport(ownerInstanceId);
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
        /// <param name="successProbability">The calculated success probability.</param>
        /// <returns>The diplomacy objective score.</returns>
        private double ScoreDiplomacy(
            AITurnContext context,
            AIMissionProposal proposal,
            double successProbability
        )
        {
            int opposingSupport =
                proposal.TargetPlanet?.GetOpposingPopularSupport(context.Faction.InstanceID) ?? 0;
            return successProbability
                + opposingSupport
                    * context.Game.Config.AI.MissionPlanning.DiplomacySupportDeficitWeight;
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
        /// <param name="config">The applicable configuration.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The mission-type priority bonus.</returns>
        private int GetPriorityBonus(
            GameConfig.AIMissionPlanningConfig config,
            AIMissionProposal proposal
        )
        {
            return proposal.MissionTypeID switch
            {
                MissionTypeIDs.Reconnaissance => config.ReconnaissancePriorityBonus,
                MissionTypeIDs.Recruitment => config.RecruitmentPriorityBonus,
                MissionTypeIDs.Rescue => config.RescuePriorityBonus,
                MissionTypeIDs.SubdueUprising => config.SubdueUprisingPriorityBonus,
                MissionTypeIDs.Research => config.ResearchPriorityBonus,
                MissionTypeIDs.JediTraining => config.JediTrainingPriorityBonus,
                MissionTypeIDs.Espionage => config.EspionagePriorityBonus,
                MissionTypeIDs.Diplomacy => config.DiplomacyPriorityBonus,
                _ => 0,
            };
        }

        /// <summary>
        /// Returns the longest participant travel distance as a score penalty.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The normalized travel penalty.</returns>
        private double GetTravelPenalty(AITurnContext context, AIMissionProposal proposal)
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
        private double GetIntelAgeScore(AITurnContext context, AIMissionProposal proposal)
        {
            int tickInterval = context.Game.Config.AI.TickInterval;
            int age = context.Assessment.GetPlanetIntelAge(proposal.TargetPlanet);
            return tickInterval > 0 && age < int.MaxValue ? (double)age / tickInterval : 0;
        }
    }
}
