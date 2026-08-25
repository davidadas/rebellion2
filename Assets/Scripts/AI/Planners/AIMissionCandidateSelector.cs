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

            int retainedAlternatives = Math.Max(
                1,
                context.Game.Config.AI.MissionPlanning.RetainedAlternativesPerMission
            );
            List<AIMissionProposal> alternatives = GetAlternatives(proposal);
            AIMissionProposal weakest = GetWeakest(alternatives);
            if (
                alternatives.Count >= retainedAlternatives
                && _scorer.GetScoreUpperBound(context, proposal) < weakest.Score
            )
                return;

            double score = _scorer.Score(context, proposal);
            if (score <= 0 && !proposal.CanExecute(context))
                return;

            proposal.SetScore(score);
            alternatives.Add(proposal);
            proposals.Add(proposal);
            if (alternatives.Count <= retainedAlternatives)
                return;

            weakest = GetWeakest(alternatives);
            alternatives.Remove(weakest);
            proposals.Remove(weakest);
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
        /// Returns the weakest retained proposal using deterministic tie-breaking.
        /// </summary>
        /// <param name="alternatives">The alternatives to inspect.</param>
        /// <returns>The weakest proposal, or null when the collection is empty.</returns>
        private static AIMissionProposal GetWeakest(IEnumerable<AIMissionProposal> alternatives)
        {
            return alternatives
                .OrderBy(proposal => proposal.Score)
                .ThenByDescending(proposal => proposal.GetSortKey(), StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }
}
