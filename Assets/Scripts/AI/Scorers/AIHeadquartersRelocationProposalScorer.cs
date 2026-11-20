using Rebellion.AI.Proposals;

namespace Rebellion.AI.Scorers
{
    /// <summary>
    /// Scores headquarters relocation urgency from the known orbital strength deficit.
    /// </summary>
    public sealed class AIHeadquartersRelocationProposalScorer : IAIProposalScorer
    {
        /// <summary>
        /// Returns whether this scorer handles the supplied proposal.
        /// </summary>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True for headquarters-relocation proposals.</returns>
        public bool CanScore(AIProposal proposal)
        {
            return proposal is AIHeadquartersRelocationProposal;
        }

        /// <summary>
        /// Returns normalized relocation urgency from zero through one.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The relocation proposal to score.</param>
        /// <returns>The unfulfilled share of the known incoming threat.</returns>
        public double Score(AITurnContext context, AIProposal proposal)
        {
            if (proposal is not AIHeadquartersRelocationProposal relocation)
                return 0;

            return 1 - AIUtility.Fulfillment(relocation.DefenseStrength, relocation.ThreatStrength);
        }
    }
}
