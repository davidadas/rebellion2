using Rebellion.AI.Director;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Executes proposals selected for the turn.
    /// </summary>
    public sealed class AIExecutionPhase : IAITurnPhase
    {
        /// <summary>
        /// Executes selected proposals that still pass validation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            if (context?.SelectedProposals == null)
                return;

            foreach (AIProposal proposal in context.SelectedProposals)
            {
                if (proposal?.CanExecute(context) == true)
                    proposal.Execute(context);
            }
        }
    }
}
