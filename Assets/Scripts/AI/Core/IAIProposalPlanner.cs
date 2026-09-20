using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Generates AI proposals for a faction turn.
    /// </summary>
    public interface IAIProposalPlanner
    {
        /// <summary>
        /// Returns proposals for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Generated proposals.</returns>
        List<AIProposal> Plan(AITurnContext context);
    }
}
