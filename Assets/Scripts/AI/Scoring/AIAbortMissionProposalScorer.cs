using Rebellion.AI.Director;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Scores mission-abort proposals ahead of optional strategic actions.
    /// </summary>
    public sealed class AIAbortMissionProposalScorer : IAIProposalScorer
    {
        /// <summary>
        /// Returns whether this scorer can score the proposal.
        /// </summary>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True when the proposal aborts a mission.</returns>
        public bool CanScore(AIProposal proposal)
        {
            return proposal is AIAbortMissionProposal;
        }

        /// <summary>
        /// Returns a score that selects mission abortions before optional actions.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>The highest score for an abort proposal; otherwise zero.</returns>
        public double Score(AITurnContext context, AIProposal proposal)
        {
            return proposal is AIAbortMissionProposal ? double.MaxValue : 0;
        }
    }
}
