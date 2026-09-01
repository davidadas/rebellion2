using Rebellion.AI.Director;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Scores mission-abort proposals ahead of optional strategic actions.
    /// </summary>
    public sealed class AIAbortMissionProposalScorer : IAIProposalScorer
    {
        /// <inheritdoc />
        public bool CanScore(AIProposal proposal)
        {
            return proposal is AIAbortMissionProposal;
        }

        /// <inheritdoc />
        public double Score(AITurnContext context, AIProposal proposal)
        {
            return proposal is AIAbortMissionProposal ? double.MaxValue : 0;
        }
    }
}
