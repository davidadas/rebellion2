using System;
using System.Collections.Generic;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.Game.Galaxy;

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
            List<AIProposal>
        > _alternatives =
            new Dictionary<(string ParticipantId, string MissionTypeId), List<AIProposal>>();

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
        /// <param name="proposal">The candidate mission used to group and bound alternatives.</param>
        /// <param name="preparation">Optional preparation action instead of an immediate mission.</param>
        internal void TryAdd(
            AITurnContext context,
            ICollection<AIProposal> proposals,
            AIMissionProposal proposal,
            AIMissionStagingProposal preparation = null
        )
        {
            if (!CanRetain(context, proposal, preparation?.Destination))
                return;

            int retainedAlternatives = Math.Max(
                1,
                context.Game.Config.AI.MissionPlanning.RetainedAlternativesPerMission
            );
            List<AIProposal> alternatives = GetAlternatives(proposal);
            AIProposal action = preparation ?? (AIProposal)proposal;
            double score = _scorer.Score(context, action);
            if (score <= 0)
                return;

            action.SetScore(score);
            alternatives.Add(action);
            proposals.Add(action);
            if (alternatives.Count <= retainedAlternatives)
                return;

            AIProposal lowestPriorityAlternative = FindLowestPriorityAlternative(alternatives);
            alternatives.Remove(lowestPriorityAlternative);
            proposals.Remove(lowestPriorityAlternative);
        }

        /// <summary>
        /// Checks whether a candidate's optimistic score can still enter its retained alternatives.
        /// </summary>
        /// <param name="context">The current planning scope.</param>
        /// <param name="proposal">The proposal whose score bound is being checked.</param>
        /// <param name="departure">Optional assembly planet for an incoming team.</param>
        /// <returns>False when the bound misses the minimum score or the retained alternatives.</returns>
        internal bool CanRetain(
            AITurnContext context,
            AIMissionProposal proposal,
            Planet departure = null
        )
        {
            if (proposal == null)
                return false;
            double upperBound = _scorer.GetScoreUpperBound(context, proposal, departure);
            if (
                upperBound <= 0
                || upperBound < context.Game.Config.AI.MissionPlanning.MinimumMissionScore
            )
                return false;
            int retainedAlternatives = Math.Max(
                1,
                context.Game.Config.AI.MissionPlanning.RetainedAlternativesPerMission
            );
            List<AIProposal> alternatives = GetAlternatives(proposal);
            return alternatives.Count < retainedAlternatives
                || upperBound >= FindLowestPriorityAlternative(alternatives).Score;
        }

        /// <summary>
        /// Gets the retained alternatives for a participant and mission type.
        /// </summary>
        /// <param name="proposal">The proposal identifying the alternatives collection.</param>
        /// <returns>The mutable alternatives collection for the proposal.</returns>
        private List<AIProposal> GetAlternatives(AIMissionProposal proposal)
        {
            (string ParticipantId, string MissionTypeId) key = (
                proposal.Participant?.InstanceID,
                proposal.MissionTypeID
            );
            if (!_alternatives.TryGetValue(key, out List<AIProposal> alternatives))
            {
                alternatives = new List<AIProposal>();
                _alternatives.Add(key, alternatives);
            }

            return alternatives;
        }

        /// <summary>
        /// Finds the retained proposal with the lowest selection priority.
        /// </summary>
        /// <param name="alternatives">The alternatives to inspect.</param>
        /// <returns>The lowest-priority proposal, or null when the collection is empty.</returns>
        private static AIProposal FindLowestPriorityAlternative(
            IEnumerable<AIProposal> alternatives
        )
        {
            AIProposal lowestPriority = null;
            foreach (AIProposal proposal in alternatives)
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
        private static bool HasLowerRetentionPriority(AIProposal candidate, AIProposal other)
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
