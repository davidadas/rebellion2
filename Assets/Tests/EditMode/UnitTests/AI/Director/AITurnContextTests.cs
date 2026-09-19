using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public class AITurnContextTests
    {
        /// <summary>
        /// Verifies constructor with faction view preserves turn input.
        /// </summary>
        [Test]
        public void Constructor_WithFactionView_PreservesTurnInput()
        {
            GalaxyMap factionView = new GalaxyMap();

            AITurnContext context = new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                factionView
            );

            Assert.AreSame(factionView, context.FactionView);
        }

        /// <summary>
        /// Verifies add proposal with null proposal does not add proposal.
        /// </summary>
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

        /// <summary>
        /// Verifies set selected proposals with new batch replaces existing selection.
        /// </summary>
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

        /// <summary>
        /// Verifies add result with null result does not add result.
        /// </summary>
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

        /// <summary>
        /// Verifies add results with result batch adds non null results.
        /// </summary>
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
            BlockadeChangedResult result = new BlockadeChangedResult();

            context.AddResults(new GameResult[] { null, result });

            Assert.AreEqual(1, context.Results.Count);
            Assert.AreSame(result, context.Results[0]);
        }

        /// <summary>
        /// Verifies committed manufacturing maintenance reduces the turn-scoped available headroom.
        /// </summary>
        [Test]
        public void CommitManufacturingMaintenance_WithPositiveCost_ReducesAvailableHeadroom()
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
            int initialHeadroom = context.AvailableProjectedMaintenanceHeadroom;

            context.CommitManufacturingMaintenance(7);

            Assert.AreEqual(initialHeadroom - 7, context.AvailableProjectedMaintenanceHeadroom);
        }
    }
}
