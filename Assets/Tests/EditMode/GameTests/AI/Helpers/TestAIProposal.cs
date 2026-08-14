using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;

namespace Rebellion.Tests.AI.Helpers
{
    public class TestAIProposal : AIProposal
    {
        private readonly List<string> _claimKeys;
        private readonly string _sortKey;

        public TestAIProposal(
            string sortKey = "test",
            IEnumerable<string> claimKeys = null,
            bool canSelect = true,
            bool canExecute = true
        )
        {
            _sortKey = sortKey;
            _claimKeys = new List<string>(claimKeys ?? new string[0]);
            CanSelectResult = canSelect;
            CanExecuteResult = canExecute;
        }

        public bool CanSelectResult { get; set; }
        public bool CanExecuteResult { get; set; }
        public int ExecuteCount { get; private set; }

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
            ExecuteCount++;
        }
    }
}
