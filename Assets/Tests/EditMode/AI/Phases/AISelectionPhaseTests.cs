using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AISelectionPhaseTests
    {
        [Test]
        public void Select_WithScoredProposals_ReturnsHighestScoreFirst()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal lowerScore = new TestAIProposal("lower", new[] { "claim:lower" });
            TestAIProposal higherScore = new TestAIProposal("higher", new[] { "claim:higher" });
            lowerScore.SetScore(10);
            higherScore.SetScore(20);
            context.AddProposal(lowerScore);
            context.AddProposal(higherScore);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(2, selected.Count);
            Assert.AreSame(higherScore, selected[0]);
            Assert.AreSame(lowerScore, selected[1]);
        }

        [Test]
        public void Select_WithConflictingClaims_SelectsOnlyHighestScoredProposal()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal lowerScore = new TestAIProposal("lower", new[] { "claim:shared" });
            TestAIProposal higherScore = new TestAIProposal("higher", new[] { "claim:shared" });
            lowerScore.SetScore(10);
            higherScore.SetScore(20);
            context.AddProposal(lowerScore);
            context.AddProposal(higherScore);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(higherScore, selected[0]);
        }

        [Test]
        public void Select_WithUnscoredProposal_DoesNotSelectProposal()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal", new[] { "claim" });
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Select_WithNonPositiveScore_DoesNotSelectProposal()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal", new[] { "claim" });
            proposal.SetScore(0);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Execute_StoresSelectedProposalsOnContext()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal", new[] { "claim" });
            proposal.SetScore(10);
            context.AddProposal(proposal);

            new AISelectionPhase().Execute(context);

            Assert.AreEqual(1, context.SelectedProposals.Count);
            Assert.AreSame(proposal, context.SelectedProposals[0]);
        }

        [Test]
        public void Select_WithManufactureProposalBeyondMaintenanceHeadroom_DoesNotSelectProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard-template",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.MaintenanceCost = 10;
            AIProductionDemand demand = new AIProductionDemand(
                "shipyard-demand",
                AIProductionDemandKind.Shipyard,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                planet,
                1,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shipyard)
            );
            proposal.SetScore(100);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        private static AITurnContext CreateEmptyContext()
        {
            return new AITurnContext(null, null, null, null, null, null, null);
        }
    }
}
