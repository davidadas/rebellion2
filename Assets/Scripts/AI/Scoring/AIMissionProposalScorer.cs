using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.AI.Scoring
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
                || proposal is not AIMissionProposal missionProposal
            )
                return 0;

            double score = GetMissionScore(context, missionProposal);
            score += GetPriorityBonus(context.Game.Config.AI.MissionPlanning, missionProposal);
            score -= GetTravelPenalty(context, missionProposal);
            score -= GetOfficerReplacementPenalty(context, missionProposal);

            return score >= context.Game.Config.AI.MissionPlanning.MinimumMissionScore ? score : 0;
        }

        private double GetMissionScore(AITurnContext context, AIMissionProposal proposal)
        {
            return proposal.MissionTypeID switch
            {
                MissionTypeIDs.Recruitment => ScoreRecruitment(context, proposal),
                MissionTypeIDs.Diplomacy => ScoreDiplomacy(context, proposal),
                MissionTypeIDs.Research => ScoreResearch(proposal),
                MissionTypeIDs.Sabotage => ScoreSabotage(context, proposal),
                MissionTypeIDs.Abduction => ScoreFromMissionTable(context, proposal),
                MissionTypeIDs.Assassination => ScoreFromMissionTable(context, proposal),
                MissionTypeIDs.Espionage => ScoreFromMissionTable(context, proposal)
                    + GetIntelAgeScore(context, proposal),
                MissionTypeIDs.Reconnaissance => ProbabilityTable.GuaranteedProbability,
                MissionTypeIDs.InciteUprising => ScoreInciteUprising(context, proposal),
                MissionTypeIDs.SubdueUprising => ScoreFromMissionTable(context, proposal),
                MissionTypeIDs.Rescue => ScoreFromMissionTable(context, proposal),
                MissionTypeIDs.JediTraining => ScoreJediTraining(proposal),
                _ => 0,
            };
        }

        private double ScoreSabotage(AITurnContext context, AIMissionProposal proposal)
        {
            return ScoreFromMissionTable(context, proposal)
                + context.Assessment.GetSabotageTargetPriorityBonus(
                    proposal.TargetPlanet,
                    proposal.SelectedTarget as IManufacturable
                );
        }

        private double ScoreRecruitment(AITurnContext context, AIMissionProposal proposal)
        {
            int leadership = GetParticipantRating(proposal.Participant, OfficerRating.Leadership);
            int opposingSupport =
                proposal.TargetPlanet?.GetOpposingPopularSupport(context.Faction.InstanceID) ?? 0;
            return LookupMissionTable(
                context,
                proposal.MissionTypeID,
                leadership - opposingSupport
            );
        }

        private double ScoreDiplomacy(AITurnContext context, AIMissionProposal proposal)
        {
            int diplomacy = GetParticipantRating(proposal.Participant, OfficerRating.Diplomacy);
            int opposingSupport =
                proposal.TargetPlanet?.GetOpposingPopularSupport(context.Faction.InstanceID) ?? 0;
            int score =
                diplomacy
                - opposingSupport
                + DiplomacyMission.GetTargetTroopState(proposal.TargetPlanet);
            double successScore = LookupMissionTable(context, proposal.MissionTypeID, score);
            return successScore
                + opposingSupport
                    * context.Game.Config.AI.MissionPlanning.DiplomacySupportDeficitWeight;
        }

        private double ScoreResearch(AIMissionProposal proposal)
        {
            return proposal.Discipline.HasValue && proposal.Participant is Officer officer
                ? officer.GetBaseRating(proposal.Discipline.Value)
                : 0;
        }

        private double ScoreInciteUprising(AITurnContext context, AIMissionProposal proposal)
        {
            int leadership = GetParticipantRating(proposal.Participant, OfficerRating.Leadership);
            int ownerSupport =
                proposal.TargetPlanet?.GetPopularSupport(proposal.TargetPlanet.GetOwnerInstanceID())
                ?? 0;
            int regimentDefense =
                proposal
                    .TargetPlanet?.GetAllRegiments()
                    .Where(regiment =>
                        regiment.GetOwnerInstanceID() != context.Faction.InstanceID
                        && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    )
                    .Sum(regiment => regiment.DefenseRating)
                ?? 0;
            return LookupMissionTable(
                context,
                proposal.MissionTypeID,
                leadership - ownerSupport - regimentDefense
            );
        }

        private double ScoreJediTraining(AIMissionProposal proposal)
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

        private double ScoreFromMissionTable(AITurnContext context, AIMissionProposal proposal)
        {
            return LookupMissionTable(
                context,
                proposal.MissionTypeID,
                GetParticipantRating(proposal.Participant, GetPrimaryMissionRating(proposal))
            );
        }

        private double LookupMissionTable(AITurnContext context, string missionTypeId, int score)
        {
            Dictionary<int, int> table =
                context.Game.Config.ProbabilityTables.Mission.GetSuccessTable(missionTypeId);
            return table == null || table.Count == 0
                ? score
                : new ProbabilityTable(table).Lookup(score);
        }

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

        private int GetOfficerReplacementPenalty(AITurnContext context, AIMissionProposal proposal)
        {
            if (proposal.Participant is not Officer || !IsHostileMission(proposal.MissionTypeID))
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

        private bool IsHostileMission(string missionTypeId)
        {
            return missionTypeId == MissionTypeIDs.Sabotage
                || missionTypeId == MissionTypeIDs.Abduction
                || missionTypeId == MissionTypeIDs.Assassination
                || missionTypeId == MissionTypeIDs.InciteUprising;
        }

        private double GetIntelAgeScore(AITurnContext context, AIMissionProposal proposal)
        {
            int tickInterval = context.Game.Config.AI.TickInterval;
            int age = context.Assessment.GetPlanetIntelAge(proposal.TargetPlanet);
            return tickInterval > 0 && age < int.MaxValue ? (double)age / tickInterval : 0;
        }

        private OfficerRating GetPrimaryMissionRating(AIMissionProposal proposal)
        {
            return proposal.MissionTypeID switch
            {
                MissionTypeIDs.Reconnaissance => OfficerRating.Espionage,
                MissionTypeIDs.Diplomacy => OfficerRating.Diplomacy,
                MissionTypeIDs.Recruitment => OfficerRating.Leadership,
                MissionTypeIDs.SubdueUprising => OfficerRating.Leadership,
                MissionTypeIDs.Abduction => OfficerRating.Combat,
                MissionTypeIDs.Assassination => OfficerRating.Combat,
                MissionTypeIDs.Espionage => OfficerRating.Espionage,
                MissionTypeIDs.Sabotage => OfficerRating.Combat,
                MissionTypeIDs.InciteUprising => OfficerRating.Leadership,
                MissionTypeIDs.Rescue => OfficerRating.Combat,
                MissionTypeIDs.Research => OfficerRating.None,
                MissionTypeIDs.JediTraining => OfficerRating.Diplomacy,
                _ => OfficerRating.None,
            };
        }

        private int GetParticipantRating(IMissionParticipant participant, OfficerRating rating)
        {
            return participant?.GetEffectiveRating(rating) ?? 0;
        }
    }
}
