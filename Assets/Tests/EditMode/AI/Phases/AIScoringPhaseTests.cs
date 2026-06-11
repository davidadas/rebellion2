using System;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AIScoringPhaseTests
    {
        [Test]
        public void Execute_WithSupportedProposal_AssignsScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            officer.Ratings[OfficerRating.Diplomacy] = 90;
            game.AttachNode(officer, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(new AIMissionProposal(officer, MissionType.Diplomacy, planet));

            new AIScoringPhase().Execute(context);

            Assert.IsTrue(context.Proposals[0].HasScore);
            Assert.Greater(context.Proposals[0].Score, 0);
        }

        [Test]
        public void Execute_WithInjectedScorer_AssignsScore()
        {
            TestAIProposal proposal = new TestAIProposal();
            AITurnContext context = new AITurnContext(null, null, null, null, null, null, null);
            context.AddProposal(proposal);
            AIScoringPhase phase = new AIScoringPhase(
                new IAIProposalScorer[] { new TestProposalScorer() }
            );

            phase.Execute(context);

            Assert.IsTrue(proposal.HasScore);
            Assert.AreEqual(42, proposal.Score);
        }

        [Test]
        public void Execute_WithUnsupportedProposal_ThrowsInvalidOperationException()
        {
            AITurnContext context = new AITurnContext(null, null, null, null, null, null, null);
            context.AddProposal(new TestAIProposal());

            Assert.Throws<InvalidOperationException>(() => new AIScoringPhase().Execute(context));
        }

        private sealed class TestProposalScorer : IAIProposalScorer
        {
            public bool CanScore(AIProposal proposal)
            {
                return proposal is TestAIProposal;
            }

            public double Score(AITurnContext context, AIProposal proposal)
            {
                return 42;
            }
        }
    }
}
