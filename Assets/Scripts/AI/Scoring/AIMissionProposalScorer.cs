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

            if (!TryCreateMission(context, missionProposal, out Mission mission))
                return 0;

            double successProbability = context
                .Missions.GetMissionOdds(mission, missionProposal.MainParticipants)
                .SuccessProbability;
            if (!MeetsUprisingMissionProbabilityFloor(context, missionProposal, successProbability))
                return 0;

            double foilProbability = GetFoilProbability(context, missionProposal, mission);
            missionProposal.SetFoilProbability(foilProbability);
            double score = GetMissionScore(context, missionProposal, successProbability);
            score += GetPriorityBonus(context.Game.Config.AI.MissionPlanning, missionProposal);
            score -= foilProbability * context.Game.Config.AI.MissionPlanning.MissionFoilRiskWeight;
            score -= GetTravelPenalty(context, missionProposal);
            score -= GetOfficerReplacementPenalty(context, missionProposal);

            return score >= context.Game.Config.AI.MissionPlanning.MinimumMissionScore ? score : 0;
        }

        /// <summary>
        /// Returns the probability that known defenders foil a mission.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The mission proposal being evaluated.</param>
        /// <param name="mission">The mission created for probability evaluation.</param>
        /// <returns>The estimated foil probability.</returns>
        private static double GetFoilProbability(
            AITurnContext context,
            AIMissionProposal proposal,
            Mission mission
        )
        {
            if (proposal.TargetPlanet == null)
                return 0;

            return context.Missions.GetMissionFoilProbability(
                mission,
                context.Assessment.GetMissionDetectorCandidates(proposal.TargetPlanet)
            );
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

            return successProbability >= context.Game.Config.AI.MissionPlanning.MinimumMissionScore;
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
            score -= GetOfficerReplacementPenalty(context, proposal);
            return score;
        }

        /// <summary>
        /// Returns mission score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="successProbability">The calculated success probability.</param>
        /// <returns>The calculated value.</returns>
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
        /// Scores sabotage.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="successProbability">The calculated success probability.</param>
        /// <returns>The calculated value.</returns>
        private double ScoreSabotage(
            AITurnContext context,
            AIMissionProposal proposal,
            double successProbability
        )
        {
            return successProbability
                + context.SabotageTargets.GetPriorityBonus(
                    proposal.TargetPlanet,
                    proposal.SelectedTarget as IManufacturable
                );
        }

        /// <summary>
        /// Scores diplomacy.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="successProbability">The calculated success probability.</param>
        /// <returns>The calculated value.</returns>
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
        /// Returns jedi training value.
        /// </summary>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The calculated value.</returns>
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
        /// Attempts to create mission.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <param name="mission">The mission.</param>
        /// <returns>True when the condition is satisfied.</returns>
        private static bool TryCreateMission(
            AITurnContext context,
            AIMissionProposal proposal,
            out Mission mission
        )
        {
            return context.Missions.TryCreateMission(proposal.CreateRequest(), out mission);
        }

        /// <summary>
        /// Returns priority bonus.
        /// </summary>
        /// <param name="config">The applicable configuration.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The calculated value.</returns>
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
        /// Returns travel penalty.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The calculated value.</returns>
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
        /// Returns officer replacement penalty.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private int GetOfficerReplacementPenalty(AITurnContext context, AIMissionProposal proposal)
        {
            if (
                proposal.Participant is not Officer
                || proposal.TargetPlanet?.GetOwnerInstanceID() == null
                || proposal.TargetPlanet.GetOwnerInstanceID() == context.Faction.InstanceID
            )
                return 0;

            bool hasSpecialForcesReplacement = context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Troop)
                .Select(technology => technology.GetReference())
                .OfType<SpecialForces>()
                .Any(specialForces => specialForces.CanPerformMission(proposal.MissionTypeID));
            return hasSpecialForcesReplacement
                ? context.Game.Config.AI.MissionPlanning.HostileOfficerReplacementPenalty
                : 0;
        }

        /// <summary>
        /// Returns intel age score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private double GetIntelAgeScore(AITurnContext context, AIMissionProposal proposal)
        {
            int tickInterval = context.Game.Config.AI.TickInterval;
            int age = context.Assessment.GetPlanetIntelAge(proposal.TargetPlanet);
            return tickInterval > 0 && age < int.MaxValue ? (double)age / tickInterval : 0;
        }
    }
}
