using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
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
            if (context?.Faction == null || proposal is not AIMissionProposal missionProposal)
                return 0;

            return missionProposal.MissionTypeID switch
            {
                RecruitmentMission.MissionTypeID => ScoreRecruitment(context, missionProposal),
                DiplomacyMission.MissionTypeID => ScoreDiplomacy(context, missionProposal),
                ResearchMission.MissionTypeID => ScoreResearch(missionProposal),
                SabotageMission.MissionTypeID => ScorePrimaryRating(missionProposal),
                AbductionMission.MissionTypeID => ScoreTargetedOfficerMission(missionProposal),
                AssassinationMission.MissionTypeID => ScoreTargetedOfficerMission(missionProposal),
                EspionageMission.MissionTypeID => ScorePrimaryRating(missionProposal),
                ReconnaissanceMission.MissionTypeID => ScorePrimaryRating(missionProposal),
                InciteUprisingMission.MissionTypeID => ScorePrimaryRating(missionProposal),
                SubdueUprisingMission.MissionTypeID => ScorePrimaryRating(missionProposal),
                RescueMission.MissionTypeID => ScoreTargetedOfficerMission(missionProposal),
                JediTrainingMission.MissionTypeID => ScorePrimaryRating(missionProposal),
                _ => 0,
            };
        }

        /// <summary>
        /// Returns the proposal score from the mission's primary participant rating.
        /// </summary>
        /// <param name="proposal">The mission proposal to score.</param>
        /// <returns>The primary rating score.</returns>
        private double ScorePrimaryRating(AIMissionProposal proposal)
        {
            return GetParticipantRating(proposal.Participant, GetPrimaryMissionRating(proposal));
        }

        /// <summary>
        /// Returns the recruitment proposal score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The mission proposal to score.</param>
        /// <returns>The recruitment proposal score.</returns>
        private double ScoreRecruitment(AITurnContext context, AIMissionProposal proposal)
        {
            int leadership = GetParticipantRating(proposal.Participant, OfficerRating.Leadership);
            int opposingSupport =
                proposal.TargetPlanet?.GetOpposingPopularSupport(context.Faction.InstanceID) ?? 0;
            ProbabilityTable table = new ProbabilityTable(
                context.Game.Config.ProbabilityTables.Mission.Recruitment
            );
            return table.Lookup(leadership - opposingSupport);
        }

        /// <summary>
        /// Returns the diplomacy proposal score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The mission proposal to score.</param>
        /// <returns>The diplomacy proposal score.</returns>
        private double ScoreDiplomacy(AITurnContext context, AIMissionProposal proposal)
        {
            return ScorePrimaryRating(proposal)
                - context.Assessment.GetFactionPopularSupport(proposal.TargetPlanet)
                + context.Assessment.GetPlanetMissionSupportPressure(proposal.TargetPlanet);
        }

        /// <summary>
        /// Returns the research proposal score.
        /// </summary>
        /// <param name="proposal">The mission proposal to score.</param>
        /// <returns>The research proposal score.</returns>
        private double ScoreResearch(AIMissionProposal proposal)
        {
            if (proposal.Discipline.HasValue && proposal.Participant is Officer officer)
                return officer.GetBaseRating(proposal.Discipline.Value);

            return 0;
        }

        /// <summary>
        /// Returns the targeted-officer proposal score.
        /// </summary>
        /// <param name="proposal">The mission proposal to score.</param>
        /// <returns>The targeted-officer proposal score.</returns>
        private double ScoreTargetedOfficerMission(AIMissionProposal proposal)
        {
            return ScorePrimaryRating(proposal) - GetTargetCombatRating(proposal.TargetOfficer);
        }

        /// <summary>
        /// Returns the mission rating used by the mission's success roll.
        /// </summary>
        /// <param name="proposal">The mission proposal to inspect.</param>
        /// <returns>The primary mission participant rating.</returns>
        private OfficerRating GetPrimaryMissionRating(AIMissionProposal proposal)
        {
            return proposal.MissionTypeID switch
            {
                ReconnaissanceMission.MissionTypeID => OfficerRating.Espionage,
                DiplomacyMission.MissionTypeID => OfficerRating.Diplomacy,
                RecruitmentMission.MissionTypeID => OfficerRating.Leadership,
                SubdueUprisingMission.MissionTypeID => OfficerRating.Leadership,
                AbductionMission.MissionTypeID => OfficerRating.Combat,
                AssassinationMission.MissionTypeID => OfficerRating.Combat,
                EspionageMission.MissionTypeID => OfficerRating.Espionage,
                SabotageMission.MissionTypeID => OfficerRating.Combat,
                InciteUprisingMission.MissionTypeID => OfficerRating.Leadership,
                RescueMission.MissionTypeID => OfficerRating.Combat,
                ResearchMission.MissionTypeID => OfficerRating.None,
                JediTrainingMission.MissionTypeID => OfficerRating.Diplomacy,
                _ => OfficerRating.Espionage,
            };
        }

        /// <summary>
        /// Returns a participant mission rating value.
        /// </summary>
        /// <param name="participant">The participant to inspect.</param>
        /// <param name="rating">The mission rating to read.</param>
        /// <returns>The participant's rating value, or zero if no participant exists.</returns>
        private int GetParticipantRating(IMissionParticipant participant, OfficerRating rating)
        {
            return participant?.GetEffectiveRating(rating) ?? 0;
        }

        /// <summary>
        /// Returns the target officer's combat rating.
        /// </summary>
        /// <param name="targetOfficer">The target officer to inspect.</param>
        /// <returns>The target officer combat rating, or zero if no target exists.</returns>
        private int GetTargetCombatRating(Officer targetOfficer)
        {
            return targetOfficer?.GetEffectiveRating(OfficerRating.Combat) ?? 0;
        }
    }
}
