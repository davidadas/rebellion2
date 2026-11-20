using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI
{
    [TestFixture]
    public class AITurnContextTests
    {
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
                null,
                null,
                factionView
            );

            Assert.AreSame(factionView, context.FactionView);
        }

        [Test]
        public void SetProductionDemands_DeferredCurrentBatch_PreservesInput()
        {
            AITurnContext context = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            AIManufactureProposal[] choices = AITestSceneBuilder.CreateProductionChoices(
                context,
                order
            );

            context.SetProductionDemands(
                context.ProductionDemands.Where(demand => demand.ExistingOrder != null)
            );

            CollectionAssert.AreEqual(new[] { choices[0].Demand }, context.ProductionDemands);
        }

        [Test]
        public void SetProductionDemands_ReplacingBatch_PreservesPreviousSnapshot()
        {
            AITurnContext context = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            AIManufactureProposal[] choices = AITestSceneBuilder.CreateProductionChoices(
                context,
                order
            );
            IReadOnlyList<AIProductionDemand> previous = context.ProductionDemands;

            context.SetProductionDemands(Array.Empty<AIProductionDemand>());

            CollectionAssert.AreEqual(choices.Select(choice => choice.Demand), previous);
        }

        [Test]
        public void GetMissionOdds_ValidProposal_ReturnsObjectiveProbability()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            SpecialForces participant = AITestSceneBuilder.CreateSpecialForces(
                "participant",
                empire.InstanceID
            );
            participant.AllowedMissionTypeIDs.Add(MissionTypeIDs.Reconnaissance);
            game.AttachNode(participant, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { participant },
                MissionTypeIDs.Reconnaissance,
                target
            );

            MissionOdds odds = context.GetMissionOdds(proposal);

            Assert.IsNotNull(odds);
            Assert.AreEqual(100, odds.ObjectiveSuccessProbability);
        }

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
    }
}
