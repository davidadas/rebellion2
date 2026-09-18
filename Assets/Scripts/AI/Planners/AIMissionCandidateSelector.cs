using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Retains the strongest bounded set of mission alternatives for each participant and mission.
    /// </summary>
    internal sealed class AIMissionCandidateSelector
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
            if (proposal == null)
                return;

            TryAddWithUpperBound(
                context,
                proposals,
                proposal,
                _scorer.GetScoreUpperBound(context, proposal)
            );
        }

        /// <summary>
        /// Scores a candidate set in descending upper-bound order so the exact bounded result can
        /// reject inferior candidates before calculating their full mission odds.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The complete proposal collection being built.</param>
        /// <param name="candidates">Candidates to evaluate.</param>
        internal void TryAddRange(
            AITurnContext context,
            ICollection<AIProposal> proposals,
            IEnumerable<AIMissionProposal> candidates
        )
        {
            if (candidates == null)
                return;

            List<AIMissionProposal> originalCandidates = candidates
                .Where(candidate => candidate != null)
                .ToList();
            foreach (
                IGrouping<
                    (string ParticipantId, string MissionTypeId),
                    AIMissionProposal
                > candidateGroup in originalCandidates.GroupBy(GetKey)
            )
            {
                List<AIMissionProposal> groupedCandidates = candidateGroup.ToList();
                List<(AIMissionProposal Proposal, double UpperBound)> orderedCandidates =
                    groupedCandidates
                        .Select(candidate =>
                            (
                                Proposal: candidate,
                                UpperBound: _scorer.GetScoreUpperBound(context, candidate)
                            )
                        )
                        .OrderByDescending(candidate => candidate.UpperBound)
                        .ThenBy(
                            candidate => candidate.Proposal.GetSortKey(),
                            StringComparer.Ordinal
                        )
                        .ToList();

                foreach ((AIMissionProposal proposal, double upperBound) in orderedCandidates)
                {
                    if (!TryAddWithUpperBound(context, proposals, proposal, upperBound))
                        break;
                }
            }

            RestoreOriginalRetainedOrder(proposals, originalCandidates);
        }

        /// <summary>
        /// Restores the traversal order of retained candidates after upper-bound evaluation so
        /// downstream random tie resolution receives the same input order as sequential scoring.
        /// </summary>
        /// <param name="proposals">The complete proposal collection being built.</param>
        /// <param name="originalCandidates">Candidates in their original traversal order.</param>
        private void RestoreOriginalRetainedOrder(
            ICollection<AIProposal> proposals,
            IReadOnlyList<AIMissionProposal> originalCandidates
        )
        {
            if (originalCandidates.Count == 0)
                return;

            HashSet<AIMissionProposal> originalCandidateSet = originalCandidates.ToHashSet();
            HashSet<AIMissionProposal> retainedCandidates = _alternatives
                .Values.SelectMany(alternatives => alternatives)
                .Where(originalCandidateSet.Contains)
                .ToHashSet();
            foreach (AIMissionProposal candidate in originalCandidates)
                proposals.Remove(candidate);
            foreach (AIMissionProposal candidate in originalCandidates)
            {
                if (retainedCandidates.Contains(candidate))
                    proposals.Add(candidate);
            }
        }

        /// <summary>
        /// Scores and retains one proposal using its precomputed score upper bound.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The complete proposal collection being built.</param>
        /// <param name="proposal">The candidate proposal.</param>
        /// <param name="upperBound">The highest score the proposal can attain.</param>
        /// <returns>
        /// True when later candidates with equal or lower upper bounds may still qualify; otherwise
        /// false.
        /// </returns>
        private bool TryAddWithUpperBound(
            AITurnContext context,
            ICollection<AIProposal> proposals,
            AIMissionProposal proposal,
            double upperBound
        )
        {
            if (proposal == null)
                return true;

            int retainedAlternatives = Math.Max(
                1,
                context.Game.Config.AI.MissionPlanning.RetainedAlternativesPerMission
            );
            List<AIMissionProposal> alternatives = GetAlternatives(proposal);
            AIMissionProposal lowestPriorityAlternative = FindLowestPriorityAlternative(
                alternatives
            );
            if (
                alternatives.Count >= retainedAlternatives
                && upperBound < lowestPriorityAlternative.Score
            )
                return false;

            double score = _scorer.Score(context, proposal);
            if (score <= 0)
                return true;

            proposal.SetScore(score);
            alternatives.Add(proposal);
            proposals.Add(proposal);
            if (alternatives.Count <= retainedAlternatives)
                return true;

            lowestPriorityAlternative = FindLowestPriorityAlternative(alternatives);
            alternatives.Remove(lowestPriorityAlternative);
            proposals.Remove(lowestPriorityAlternative);
            return true;
        }

        /// <summary>
        /// Returns the participant and mission identity used to group interchangeable candidates.
        /// </summary>
        /// <param name="proposal">The proposal to identify.</param>
        /// <returns>The candidate-group key.</returns>
        private static (string ParticipantId, string MissionTypeId) GetKey(
            AIMissionProposal proposal
        ) => (proposal.Participant?.InstanceID, proposal.MissionTypeID);

        /// <summary>
        /// Gets the retained alternatives for a participant and mission type.
        /// </summary>
        /// <param name="proposal">The proposal identifying the alternatives collection.</param>
        /// <returns>The mutable alternatives collection for the proposal.</returns>
        private List<AIMissionProposal> GetAlternatives(AIMissionProposal proposal)
        {
            (string ParticipantId, string MissionTypeId) key = GetKey(proposal);
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
