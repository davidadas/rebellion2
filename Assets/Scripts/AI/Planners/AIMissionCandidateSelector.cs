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
        /// Scores queued candidates cheaply and retains the strongest candidates that satisfy
        /// officer-risk policy.
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
                foreach (AIMissionProposal proposal in alternatives)
                    proposal.SetScore(_scorer.Score(context, proposal));

                int retained = 0;
                foreach (
                    AIMissionProposal proposal in alternatives
                        .Where(proposal => proposal.Score > 0 || proposal.CanExecute(context))
                        .OrderByDescending(proposal => proposal.Score)
                        .ThenBy(proposal => proposal.GetSortKey(), StringComparer.Ordinal)
                )
                {
                    if (!_scorer.AllowsPersonnelRisk(context, proposal))
                        continue;

                    proposals.Add(proposal);
                    retained++;
                    if (retained >= retainedAlternatives)
                        break;
                }
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
    }
}
