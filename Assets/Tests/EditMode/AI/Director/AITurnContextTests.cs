using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Results;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public class AITurnContextTests
    {
        [Test]
        public void AddProposal_WithNullProposal_DoesNotAddProposal()
        {
            AITurnContext context = new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );

            context.AddProposal(null);

            Assert.AreEqual(0, context.Proposals.Count);
        }

        [Test]
        public void SetSelectedProposals_WithNewBatch_ReplacesExistingSelection()
        {
            AITurnContext context = new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );
            TestAIProposal first = new TestAIProposal("first");
            TestAIProposal second = new TestAIProposal("second");
            context.SetSelectedProposals(new List<AIProposal> { first });

            context.SetSelectedProposals(new List<AIProposal> { null, second });

            Assert.AreEqual(1, context.SelectedProposals.Count);
            Assert.AreSame(second, context.SelectedProposals[0]);
        }

        [Test]
        public void AddResult_WithNullResult_DoesNotAddResult()
        {
            AITurnContext context = new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );

            context.AddResult(null);

            Assert.AreEqual(0, context.Results.Count);
        }

        [Test]
        public void AddResults_WithResultBatch_AddsNonNullResults()
        {
            AITurnContext context = new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );
            PlanetStatChangedResult result = new PlanetStatChangedResult();

            context.AddResults(new GameResult[] { null, result });

            Assert.AreEqual(1, context.Results.Count);
            Assert.AreSame(result, context.Results[0]);
        }
    }
}
