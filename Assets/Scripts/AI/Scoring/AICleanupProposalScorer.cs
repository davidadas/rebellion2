using Rebellion.AI.Director;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Scores mandatory cleanup proposals.
    /// </summary>
    public sealed class AICleanupProposalScorer : IAIProposalScorer
    {
        /// <summary>
        /// Returns whether this scorer can score the proposal.
        /// </summary>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True when the proposal aborts a mission.</returns>
        public bool CanScore(AIProposal proposal)
        {
            return proposal is AIAbortMissionProposal or AIFacilityRemovalProposal;
        }

        /// <summary>
        /// Returns the neutral score for a mandatory cleanup proposal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>Zero; mandatory ordering is handled explicitly during selection.</returns>
        public double Score(AITurnContext context, AIProposal proposal)
        {
            return 0;
        }
    }
}
