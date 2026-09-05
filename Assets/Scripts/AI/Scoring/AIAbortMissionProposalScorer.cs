using Rebellion.AI.Director;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Scores mission-abort proposals selected through mandatory proposal priority.
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
        /// Returns the neutral score for a mandatory mission-abort proposal.
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
