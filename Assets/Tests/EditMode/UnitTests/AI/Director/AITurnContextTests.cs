using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public class AITurnContextTests
    {
        private Faction _faction;
        private GameRoot _game;

        /// <summary>
        /// Creates the game and faction used by each context test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _faction = new Faction { InstanceID = "faction" };
            _game.GetFactions().Add(_faction);
        }

        [Test]
        public void Constructor_WithFactionView_PreservesTurnInput()
        {
            GalaxyMap factionView = new GalaxyMap();

            GameSession session = GameSessionFactory.Create(_game, TestContent.Data);
            AITurnContext context = new AITurnContext(
                _game,
                _faction,
                session.Features.Commands,
                session.Queries,
                _game.Random,
                factionView
            );

            Assert.AreSame(factionView, context.FactionView);
        }

        [Test]
        public void AddProposal_WithNullProposal_DoesNotAddProposal()
        {
            AITurnContext context = AITestSceneBuilder.CreateContext(_game, _faction);

            context.AddProposal(null);

            Assert.AreEqual(0, context.Proposals.Count);
        }

        [Test]
        public void SetSelectedProposals_WithNewBatch_ReplacesExistingSelection()
        {
            AITurnContext context = AITestSceneBuilder.CreateContext(_game, _faction);
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
            AITurnContext context = AITestSceneBuilder.CreateContext(_game, _faction);

            context.AddResult(null);

            Assert.AreEqual(0, context.Results.Count);
        }

        [Test]
        public void AddResults_WithResultBatch_AddsNonNullResults()
        {
            AITurnContext context = AITestSceneBuilder.CreateContext(_game, _faction);
            BlockadeChangedResult result = new BlockadeChangedResult();

            context.AddResults(new GameResult[] { null, result });

            Assert.AreEqual(1, context.Results.Count);
            Assert.AreSame(result, context.Results[0]);
        }
    }
}
