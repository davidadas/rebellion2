using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Proposals;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AIExecutionPhaseTests
    {
        [Test]
        public void Execute_WithSelectedExecutableProposal_ExecutesProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: true);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(1, proposal.ExecuteCount);
        }

        [Test]
        public void Execute_WithSelectedNonExecutableProposal_DoesNotExecuteProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: false);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(0, proposal.ExecuteCount);
        }

        private static AITurnContext CreateContext()
        {
            return new AITurnContext(null, null, null, null, null, null, null);
        }
    }
}
