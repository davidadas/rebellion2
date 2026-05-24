using Rebellion.AI.Director;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Scores AI proposals during the scoring phase.
    /// </summary>
    public interface IAIProposalScorer
    {
        /// <summary>
        /// Returns whether this scorer can score the proposal.
        /// </summary>
        /// <param name="proposal">The proposal to check.</param>
        /// <returns>True if this scorer can score the proposal.</returns>
        bool CanScore(AIProposal proposal);

        /// <summary>
        /// Returns the score for a proposal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>The proposal score.</returns>
        double Score(AITurnContext context, AIProposal proposal);
    }
}
