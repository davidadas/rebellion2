using System;
using System.Collections.Generic;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Retains the strongest bounded set of mission alternatives for each participant and mission.
    /// </summary>
    internal sealed class AIMissionCandidatePool
    {
        // Candidate State.
        private readonly AIMissionProposalScorer _scorer = new AIMissionProposalScorer();
        private readonly Dictionary<
            (string ParticipantId, string MissionTypeId),
            List<AIMissionProposal>
        > _alternatives =
            new Dictionary<(string ParticipantId, string MissionTypeId), List<AIMissionProposal>>();

        /// <summary>
        /// Clears candidates retained from the previous planning turn.
        /// </summary>
        internal void Reset()
        {
            _alternatives.Clear();
        }

        /// <summary>
        /// Scores and conditionally retains one mission proposal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The complete proposal collection being built.</param>
        /// <param name="proposal">The candidate proposal.</param>
        internal void TryAdd(
            AITurnContext context,
            ICollection<AIProposal> proposals,
            AIMissionProposal proposal
        )
        {
            if (!CanRetain(context, proposal))
                return;

            int retainedAlternatives = Math.Max(
                1,
                context.Game.Config.AI.MissionPlanning.RetainedAlternativesPerMission
            );
            List<AIMissionProposal> alternatives = GetAlternatives(proposal);
            double score = _scorer.Score(context, proposal);
            if (score <= 0)
                return;

            proposal.SetScore(score);
            alternatives.Add(proposal);
            proposals.Add(proposal);
            if (alternatives.Count <= retainedAlternatives)
                return;

            AIMissionProposal lowestPriorityAlternative = FindLowestPriorityAlternative(
                alternatives
            );
            alternatives.Remove(lowestPriorityAlternative);
            proposals.Remove(lowestPriorityAlternative);
        }

        /// <summary>
        /// Checks whether a candidate's optimistic score can still enter its retained alternatives.
        /// </summary>
        /// <param name="context">The current planning scope.</param>
        /// <param name="proposal">The proposal whose score bound is being checked.</param>
        /// <returns>False only when its upper bound is strictly below an already retained alternative.</returns>
        internal bool CanRetain(AITurnContext context, AIMissionProposal proposal)
        {
            if (proposal == null)
                return false;
            int retainedAlternatives = Math.Max(
                1,
                context.Game.Config.AI.MissionPlanning.RetainedAlternativesPerMission
            );
            List<AIMissionProposal> alternatives = GetAlternatives(proposal);
            return alternatives.Count < retainedAlternatives
                || _scorer.GetScoreUpperBound(context, proposal)
                    >= FindLowestPriorityAlternative(alternatives).Score;
        }

        /// <summary>
        /// Gets the retained alternatives for a participant and mission type.
        /// </summary>
        /// <param name="proposal">The proposal identifying the alternatives collection.</param>
        /// <returns>The mutable alternatives collection for the proposal.</returns>
        private List<AIMissionProposal> GetAlternatives(AIMissionProposal proposal)
        {
            (string ParticipantId, string MissionTypeId) key = (
                proposal.Participant?.InstanceID,
                proposal.MissionTypeID
            );
            if (!_alternatives.TryGetValue(key, out List<AIMissionProposal> alternatives))
            {
                alternatives = new List<AIMissionProposal>();
                _alternatives.Add(key, alternatives);
            }

            return alternatives;
        }

        /// <summary>
        /// Finds the retained proposal with the lowest selection priority.
        /// </summary>
        /// <param name="alternatives">The alternatives to inspect.</param>
        /// <returns>The lowest-priority proposal, or null when the collection is empty.</returns>
        private static AIMissionProposal FindLowestPriorityAlternative(
            IEnumerable<AIMissionProposal> alternatives
        )
        {
            AIMissionProposal lowestPriority = null;
            foreach (AIMissionProposal proposal in alternatives)
            {
                if (lowestPriority == null || HasLowerRetentionPriority(proposal, lowestPriority))
                    lowestPriority = proposal;
            }

            return lowestPriority;
        }

        /// <summary>
        /// Compares two proposals using the inverse of the final selection order.
        /// </summary>
        /// <param name="candidate">The proposal being compared.</param>
        /// <param name="other">The proposal currently considered lowest priority.</param>
        /// <returns>True when the candidate has a lower score, or loses the deterministic sort-key tie.</returns>
        private static bool HasLowerRetentionPriority(
            AIMissionProposal candidate,
            AIMissionProposal other
        )
        {
            int scoreComparison = candidate.Score.CompareTo(other.Score);
            return scoreComparison != 0
                ? scoreComparison < 0
                : string.Compare(
                    candidate.GetSortKey(),
                    other.GetSortKey(),
                    StringComparison.Ordinal
                ) > 0;
        }
    }
}
