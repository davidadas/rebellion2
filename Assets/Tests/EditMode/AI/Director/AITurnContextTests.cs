using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public class AITurnContextTests
    {
        [Test]
        public void AddProposal_WithNullProposal_DoesNotAddProposal()
        {
            AITurnContext context = new AITurnContext(null, null, null, null, null, null, null);

            context.AddProposal(null);

            Assert.AreEqual(0, context.Proposals.Count);
        }

        [Test]
        public void SetSelectedProposals_WithNewBatch_ReplacesExistingSelection()
        {
            AITurnContext context = new AITurnContext(null, null, null, null, null, null, null);
            TestAIProposal first = new TestAIProposal("first");
            TestAIProposal second = new TestAIProposal("second");
            context.SetSelectedProposals(new List<AIProposal> { first });

            context.SetSelectedProposals(new List<AIProposal> { null, second });

            Assert.AreEqual(1, context.SelectedProposals.Count);
            Assert.AreSame(second, context.SelectedProposals[0]);
        }
    }
}
