using System;
using System.Collections.Generic;
using Rebellion.AI.Proposals;
using Rebellion.Simulation;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Executes proposals selected for the turn.
    /// </summary>
    public sealed class AIExecutionPhase : IAIIncrementalTurnPhase
    {
        private readonly GameResultBus _results;

        /// <summary>
        /// Creates an execution phase that observes completed changes between incremental steps.
        /// </summary>
        /// <param name="results">The completed game-result bus, when execution can span frames.</param>
        public AIExecutionPhase(GameResultBus results = null)
        {
            _results = results;
        }

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

            using IDisposable resultObservation = _results?.ObserveAll(_ =>
                context.InvalidateManufacturingBudget()
            );
            foreach (AIProposal proposal in context.SelectedProposals)
            {
                if (proposal?.CanExecute(context) == true)
                {
                    proposal.Execute(context);
                    if (proposal is not AIManufactureProposal)
                        context.InvalidateManufacturingBudget();
                }
                yield return proposal;
            }
        }
    }
}
