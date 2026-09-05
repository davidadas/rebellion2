using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;

namespace Rebellion.Tests.AI.Helpers
{
    public class TestAIProposal : AIProposal
    {
        private readonly List<string> _claimKeys;
        private readonly string _sortKey;
        private readonly AIProposalPriority _priority;

        internal override AIProposalPriority Priority => _priority;

        public bool CanSelectResult { get; set; }
        public bool CanExecuteResult { get; set; }
        public int ExecuteCount { get; private set; }

        internal TestAIProposal(
            string sortKey = "test",
            IEnumerable<string> claimKeys = null,
            bool canSelect = true,
            bool canExecute = true,
            AIProposalPriority priority = AIProposalPriority.Optional
        )
        {
            _sortKey = sortKey;
            _claimKeys = new List<string>(claimKeys ?? new string[0]);
            CanSelectResult = canSelect;
            CanExecuteResult = canExecute;
            _priority = priority;
        }

        public override IReadOnlyList<string> GetClaimKeys()
        {
            return _claimKeys;
        }

        public override string GetSortKey()
        {
            return _sortKey;
        }

        public override bool CanSelect(AITurnContext context)
        {
            return CanSelectResult;
        }

        public override bool CanExecute(AITurnContext context)
        {
            return CanExecuteResult;
        }

        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            ExecuteCount++;
        }
    }
}
