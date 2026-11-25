using System.Collections.Generic;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds proposals in resumable, ordered work units within the current faction turn.
    /// </summary>
    internal interface IAIIncrementalProposalPlanner : IAIProposalPlanner
    {
        /// <summary>
        /// Builds one planner's complete proposal set without publishing partial alternatives.
        /// </summary>
        /// <param name="context">The current faction's planning context.</param>
        /// <param name="proposals">The planner-owned output, published only after enumeration completes.</param>
        /// <returns>Cooperative checkpoints between completed work units.</returns>
        IEnumerable<object> PlanIncrementally(AITurnContext context, List<AIProposal> proposals);
    }
}
