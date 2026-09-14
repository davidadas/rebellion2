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
            double score = GetMissionScore(context, missionProposal, successProbability);
            score += GetPriorityValue(utility.Priority, missionProposal);
            score -= AIUtility.EvaluateRaw(foilProbability, utility.Objective.FoilRisk);
            score -= AIUtility.EvaluateRaw(
                GetTravelCost(context, missionProposal),
                utility.Objective.TravelCost
            );
            score -= AIUtility.Evaluate(
                HasOfficerReplacementRisk(context, missionProposal) ? 1 : 0,
                utility.Objective.OfficerRisk
            );

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

            GameConfig.AIMissionUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility;
            double score = GetMissionScore(context, proposal, _maximumSuccessProbability);
            score += GetPriorityValue(utility.Priority, proposal);
            score -= AIUtility.EvaluateRaw(
                GetTravelCost(context, proposal),
                utility.Objective.TravelCost
            );
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
            GameConfig.AIMissionObjectiveUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility
                .Objective;
            double successValue = AIUtility.EvaluateRaw(successProbability, utility.Success);
            return proposal.MissionTypeID switch
            {
                MissionTypeIDs.Diplomacy => ScoreDiplomacy(context, proposal, successValue),
                MissionTypeIDs.Sabotage => ScoreSabotage(context, proposal, successValue),
                MissionTypeIDs.Espionage => successValue
                    + AIUtility.EvaluateRaw(GetIntelAge(context, proposal), utility.IntelAge),
                MissionTypeIDs.JediTraining => successValue
                    + AIUtility.EvaluateRaw(GetJediTrainingValue(proposal), utility.TrainingValue),
                _ => successValue,
            };
        }

        /// <summary>
        /// Scores sabotage success and the strategic value of its selected target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="successValue">The calculated success utility.</param>
        /// <returns>The sabotage objective score.</returns>
        private double ScoreSabotage(
            AITurnContext context,
            AIMissionProposal proposal,
            double successValue
        )
        {
            return successValue
                + GetSabotageTargetValue(
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
        internal static double GetSabotageTargetValue(
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
            if (utility == null || planet == null || target == null)
                return 0;

            bool isAttackTarget = context.Assessment.IsAttackPreparationTarget(planet);
            if (target is Building building)
            {
                double value = AIUtility.Evaluate(1, utility.Infrastructure);
                if (IsPlanetaryDefenseBuilding(building))
                    value += AIUtility.Evaluate(1, utility.Defense);

                if (building.IsShieldGenerator())
                    value += AIUtility.Evaluate(1, utility.Shield);

                if (isAttackTarget && IsPlanetaryDefenseBuilding(building))
                {
                    value +=
                        AIUtility.Evaluate(1, utility.AttackTarget)
                        + AIUtility.Evaluate(1, utility.AttackDefense);
                }

                return value;
            }

            double unitValue = target switch
            {
                Regiment when IsGarrisonedAtPlanet(planet, target) => AIUtility.Evaluate(
                    1,
                    utility.GarrisonRegiment
                )
                    + AIUtility.Evaluate(
                        HasOppositionSupportMajority(context, planet) ? 1 : 0,
                        utility.FavoredSupportRegiment
                    ),
                Starfighter when IsGarrisonedAtPlanet(planet, target) => AIUtility.Evaluate(
                    1,
                    utility.GarrisonStarfighter
                ),
                _ => AIUtility.Evaluate(1, utility.OtherUnit),
            };
            return isAttackTarget
                ? unitValue + AIUtility.Evaluate(1, utility.AttackTarget)
                : unitValue;
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
        /// <param name="successValue">The calculated success utility.</param>
        /// <returns>The diplomacy objective score.</returns>
        private double ScoreDiplomacy(
            AITurnContext context,
            AIMissionProposal proposal,
            double successValue
        )
        {
            int opposingSupport =
                proposal.TargetPlanet?.GetOpposingPopularSupport(context.Faction.InstanceID) ?? 0;
            GameConfig.AIDiplomacyUtilityConfig utility = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .Utility
                .Diplomacy;
            bool isCoreWorld =
                proposal.TargetPlanet?.GetParentOfType<PlanetSector>()?.SectorType
                == PlanetSectorType.Core;
            return successValue
                + AIUtility.Evaluate(isCoreWorld ? 1 : 0, utility.CoreWorld)
                + AIUtility.EvaluateRaw(opposingSupport, utility.SupportDeficit);
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
        private static double GetPriorityValue(
            GameConfig.AIMissionPriorityUtilityConfig utility,
            AIMissionProposal proposal
        )
        {
            GameConfig.AIConsiderationConfig consideration = proposal.MissionTypeID switch
            {
                MissionTypeIDs.Reconnaissance => utility.Reconnaissance,
                MissionTypeIDs.Recruitment => utility.Recruitment,
                MissionTypeIDs.Rescue => utility.Rescue,
                MissionTypeIDs.SubdueUprising => utility.SubdueUprising,
                MissionTypeIDs.Research => utility.Research,
                MissionTypeIDs.JediTraining => utility.JediTraining,
                MissionTypeIDs.Espionage => utility.Espionage,
                MissionTypeIDs.Diplomacy => utility.Diplomacy,
                _ => null,
            };
            return AIUtility.Evaluate(1, consideration);
        }

        /// <summary>
        /// Penalizes risking an officer on hostile work that unlocked special forces can perform.
        /// </summary>
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
