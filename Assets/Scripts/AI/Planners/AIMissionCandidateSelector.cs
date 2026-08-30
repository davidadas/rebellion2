using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Retains the strongest safe mission alternatives for each participant and mission.
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
        /// Queues one mission proposal for deferred scoring and risk evaluation.
        /// </summary>
        /// <param name="proposal">The candidate proposal.</param>
        internal void Add(AIMissionProposal proposal)
        {
            if (proposal == null)
                return;

            GetAlternatives(proposal).Add(proposal);
        }

        /// <summary>
        /// Evaluates queued candidates in descending score-bound order and retains the strongest
        /// candidates that satisfy officer-risk policy.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal collection receiving retained candidates.</param>
        internal void Flush(AITurnContext context, ICollection<AIProposal> proposals)
        {
            int retainedAlternatives = Math.Max(
                1,
                context.Game.Config.AI.MissionPlanning.RetainedAlternativesPerMission
            );
            foreach (List<AIMissionProposal> alternatives in _alternatives.Values)
            {
                List<AIMissionProposal> retained = new List<AIMissionProposal>();
                foreach (
                    var candidate in alternatives
                        .Select(proposal =>
                            (
                                Proposal: proposal,
                                UpperBound: _scorer.GetScoreUpperBound(context, proposal)
                            )
                        )
                        .OrderByDescending(candidate => candidate.UpperBound)
                        .ThenBy(
                            candidate => candidate.Proposal.GetSortKey(),
                            StringComparer.Ordinal
                        )
                )
                {
                    AIMissionProposal weakest = GetWeakest(retained);
                    if (
                        retained.Count >= retainedAlternatives
                        && candidate.UpperBound < weakest.Score
                    )
                        break;

                    AIMissionProposal proposal = candidate.Proposal;
                    proposal.SetScore(_scorer.Score(context, proposal));
                    if (proposal.Score <= 0 && !proposal.CanExecute(context))
                        continue;

                    if (!_scorer.AllowsPersonnelRisk(context, proposal))
                        continue;

                    retained.Add(proposal);
                    if (retained.Count > retainedAlternatives)
                        retained.Remove(GetWeakest(retained));
                }

                foreach (AIMissionProposal proposal in retained)
                    proposals.Add(proposal);
            }

            _alternatives.Clear();
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
