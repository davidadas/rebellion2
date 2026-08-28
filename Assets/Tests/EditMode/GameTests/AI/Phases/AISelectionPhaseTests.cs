using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Planners.Demand;
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
        public void Select_WithManufactureProposalBeyondMaintenanceHeadroom_DoesNotSelectProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "p1",
                empire.InstanceID
            );
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
            AIDemand demand = new AIDemand(
                "shipyard-demand",
                AIDemandKind.Shipyard,
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

        [Test]
        public void Select_WithDiscretionaryProductionBelowRefinedReserve_DoesNotSelectProposal()
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                producer,
                AIDemandKind.PlanetaryDefense,
                BuildingType.Defense
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_WithReserveEligibleProductionBelowRefinedReserve_SelectsProposal()
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                producer,
                AIDemandKind.Refinery,
                BuildingType.Refinery
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [Test]
        public void Select_WithUnavailablePreferredManufacturingProducer_SelectsNextProducer()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "producer-system");
            Planet preferredProducer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "preferred-producer",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 2
            );
            Planet fallbackProducer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fallback-producer",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                preferredProducer,
                "preferred-construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                fallbackProducer,
                "fallback-construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building mine = AITestSceneBuilder.CreateBuildingTemplate(
                "mine-template",
                BuildingType.Mine,
                ManufacturingType.Building
            );
            AIDemand preferredDemand = new AIDemand(
                "preferred-demand",
                AIDemandKind.Mine,
                ManufacturingType.Building,
                BuildingType.Mine,
                preferredProducer,
                1,
                100
            );
            AIDemand flexibleDemand = new AIDemand(
                "flexible-demand",
                AIDemandKind.Mine,
                ManufacturingType.Building,
                BuildingType.Mine,
                fallbackProducer,
                1,
                90
            );
            AIManufactureProposal preferredProposal = new AIManufactureProposal(
                preferredDemand,
                preferredProducer,
                new Technology(mine)
            );
            AIManufactureProposal flexibleProposal = new AIManufactureProposal(
                flexibleDemand,
                new[] { preferredProducer, fallbackProducer },
                new Technology(mine),
                false
            );
            preferredProposal.SetScore(100);
            flexibleProposal.SetScore(90);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(preferredProposal);
            context.AddProposal(flexibleProposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(
                new AIProposal[] { preferredProposal, flexibleProposal },
                selected
            );
            Assert.AreSame(fallbackProducer, flexibleProposal.ProducerPlanet);
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

        /// <summary>
        /// Creates a manufacturing context below its configured refined-material reserve.
        /// </summary>
        /// <param name="producer">The planet containing the construction facility.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateRefinedMaterialReserveContext(out Planet producer)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.RefinedMaterialReservePercent = 50;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "reserve-system");
            producer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "reserve-producer",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            for (int index = 0; index < 2; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    producer,
                    $"mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    producer,
                    $"refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }

            empire.RefinedMaterialStockpile = 0;
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>
        /// Creates a scored building-production proposal.
        /// </summary>
        /// <param name="producer">The planet producing the building.</param>
        /// <param name="kind">The demand kind represented by the proposal.</param>
        /// <param name="buildingType">The building type to manufacture.</param>
        /// <returns>The building-production proposal.</returns>
        private static AIManufactureProposal CreateManufactureProposal(
            Planet producer,
            AIDemandKind kind,
            BuildingType buildingType
        )
        {
            Building building = AITestSceneBuilder.CreateBuildingTemplate(
                $"reserve-{buildingType}",
                buildingType,
                ManufacturingType.None
            );
            building.MaintenanceCost = 0;
            AIDemand demand = new AIDemand(
                $"reserve-{kind}",
                kind,
                ManufacturingType.Building,
                buildingType,
                producer,
                1,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(building)
            );
            proposal.SetScore(100);
            return proposal;
        }

        private static AITurnContext CreateEmptyContext()
        {
            return new AITurnContext(null, null, null, null, null, null, null, null);
        }
    }
}
