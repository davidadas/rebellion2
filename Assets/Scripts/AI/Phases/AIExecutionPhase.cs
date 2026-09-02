using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Executes proposals selected for the turn.
    /// </summary>
    public sealed class AIExecutionPhase : IAIIncrementalTurnPhase
    {
        /// <summary>
        /// Executes selected proposals that still pass validation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            foreach (object _ in ExecuteIncrementally(context)) { }
        }

        /// <summary>
        /// Executes selected proposals one at a time.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>A sequence containing one marker per executed proposal.</returns>
        public IEnumerable<object> ExecuteIncrementally(AITurnContext context)
        {
            if (context?.SelectedProposals == null)
                yield break;

            foreach (AIProposal proposal in context.SelectedProposals)
            {
                if (proposal?.CanExecute(context) == true)
                    proposal.Execute(context);
                yield return null;
            }
        }
    }
}
