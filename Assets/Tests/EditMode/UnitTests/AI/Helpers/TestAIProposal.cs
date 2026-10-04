using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;

namespace Rebellion.Tests.AI.Helpers
{
    public class TestAIProposal : AIProposal
    {
        private readonly string _sortKey;
        private readonly AIProposalPriority _priority;

        internal override AIProposalPriority Priority => _priority;

        public bool CanSelectResult { get; set; }
        public bool CanExecuteResult { get; set; }
        public int ExecuteCount { get; private set; }

        /// <summary>
        /// Initializes a new instance of the TestAIProposal class.
        /// </summary>
        /// <param name="sortKey">The sort key.</param>
        /// <param name="canSelect">Whether can select.</param>
        /// <param name="canExecute">Whether can execute.</param>
        /// <param name="priority">The priority.</param>
        internal TestAIProposal(
            string sortKey = "test",
            bool canSelect = true,
            bool canExecute = true,
            AIProposalPriority priority = AIProposalPriority.Optional
        )
        {
            _sortKey = sortKey;
            CanSelectResult = canSelect;
            CanExecuteResult = canExecute;
            _priority = priority;
        }

        /// <summary>
        /// Gets sort key.
        /// </summary>
        /// <returns>The requested sort key.</returns>
        public override string GetSortKey()
        {
            return _sortKey;
        }

        /// <summary>
        /// Checks whether the select condition is met.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>True when the select condition is met; otherwise false.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return CanSelectResult;
        }

        /// <summary>
        /// Checks whether the execute condition is met.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>True when the execute condition is met; otherwise false.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return CanExecuteResult;
        }

        /// <summary>
        /// Executes the requested operation.
        /// </summary>
        /// <param name="context">The context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            ExecuteCount++;
        }
    }
}
