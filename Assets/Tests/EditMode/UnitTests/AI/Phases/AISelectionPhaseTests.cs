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
        public void Select_WithBuildingProposalsBeyondDestinationEnergy_SelectsHighestValueFit()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 1,
                rawResourceNodes: 1
            );
            Planet firstProducer = AddBuildingProducer(game, sector, empire, "first-producer");
            Planet secondProducer = AddBuildingProducer(game, sector, empire, "second-producer");
            AIManufactureProposal higher = CreateBuildingProposal(
                destination,
                firstProducer,
                AIProductionRequirementKind.Mine,
                BuildingType.Mine,
                100
            );
            AIManufactureProposal lower = CreateBuildingProposal(
                destination,
                secondProducer,
                AIProductionRequirementKind.Refinery,
                BuildingType.Refinery,
                90
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(lower);
            context.AddProposal(higher);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { higher }, selected);
        }

        [Test]
        public void Select_WithEqualScoredConflictingProposals_UsesPersistedRandomTieBreaker()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            AITurnContext context = AITestSceneBuilder.CreateContext(
                game,
                empire,
                random: new SequenceRNG(intValues: new[] { 0 })
            );
            TestAIProposal first = new TestAIProposal("first", new[] { "claim:shared" });
            TestAIProposal second = new TestAIProposal("second", new[] { "claim:shared" });
            first.SetScore(10);
            second.SetScore(10);
            context.AddProposal(first);
            context.AddProposal(second);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { second }, selected);
        }

        [Test]
        public void Select_WithMandatoryProposal_SelectsItBeforeHigherScoredOptionalProposal()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal optional = new TestAIProposal("optional", new[] { "claim:shared" });
            TestAIProposal mandatory = new TestAIProposal(
                "mandatory",
                new[] { "claim:shared" },
                priority: AIProposalPriority.Mandatory
            );
            optional.SetScore(100);
            mandatory.SetScore(0);
            context.AddProposal(optional);
            context.AddProposal(mandatory);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(mandatory, selected[0]);
        }

        [Test]
        public void Select_WithZeroScoreFleetOrderCleanup_SelectsProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            FleetOrder order = new FleetOrder { OrderType = FleetOrderType.Colonize };
            fleet.Order = order;
            game.AttachNode(fleet, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIClearFleetOrderProposal proposal = new AIClearFleetOrderProposal(fleet, order);
            proposal.SetScore(0);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
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
            AIProductionRequirement demand = new AIProductionRequirement(
                "shipyard-demand",
                AIProductionRequirementKind.Shipyard,
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

        [TestCase(AIProductionRequirementKind.Mine, BuildingType.Mine)]
        [TestCase(AIProductionRequirementKind.Refinery, BuildingType.Refinery)]
        public void Select_WithEconomyRecoveryBelowMaintenanceReserve_SelectsProposal(
            AIProductionRequirementKind kind,
            BuildingType buildingType
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "p1",
                empire.InstanceID,
                rawResourceNodes: buildingType == BuildingType.Mine ? 1 : 0
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            game.Config.AI.Selection.MaintenanceHeadroomReserve =
                context.Assessment.ProjectedMaintenanceHeadroom + 100;
            Building economyBuilding = AITestSceneBuilder.CreateBuildingTemplate(
                $"{kind}-template",
                buildingType,
                ManufacturingType.None
            );
            economyBuilding.MaintenanceCost = 10;
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionRequirement(
                    $"{kind}-demand",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    planet,
                    1,
                    100
                ),
                planet,
                new Technology(economyBuilding)
            );
            proposal.SetScore(100);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [Test]
        public void Select_WithFacilityExpansionAtDistinctProducers_SelectsBothProposals()
        {
            AITurnContext context = CreateFacilityExpansionContext(
                allocationPercent: 30,
                out AIManufactureProposal first,
                out AIManufactureProposal second
            );
            context.AddProposal(first);
            context.AddProposal(second);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEquivalent(new[] { first, second }, selected);
        }

        [Test]
        public void Select_WithFacilityExpansionBeyondLegacyBudget_SelectsBothProposals()
        {
            AITurnContext context = CreateFacilityExpansionContext(
                allocationPercent: 10,
                out AIManufactureProposal first,
                out AIManufactureProposal second
            );
            context.AddProposal(first);
            context.AddProposal(second);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEquivalent(new[] { first, second }, selected);
        }

        [Test]
        public void Select_WithPartiallyAffordableFacilityBatch_SelectsAffordablePrefix()
        {
            AITurnContext context = CreateFacilityExpansionContext(
                allocationPercent: 100,
                out AIManufactureProposal proposal,
                out AIManufactureProposal _,
                quantity: 4
            );
            context.Game.Config.AI.Selection.MaintenanceHeadroomReserve =
                context.Assessment.ProjectedMaintenanceHeadroom - 25;
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual(2, ((AIManufactureProposal)selected[0]).GetManufacturingCount());
            Assert.AreEqual(4, proposal.GetManufacturingCount());
        }

        [Test]
        public void Select_WithDiscretionaryProductionBelowRefinedReserve_SelectsProposal()
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.PlanetaryDefense,
                BuildingType.Defense
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [TestCase(AIProductionRequirementKind.Mine, BuildingType.Mine)]
        [TestCase(AIProductionRequirementKind.Refinery, BuildingType.Refinery)]
        [TestCase(AIProductionRequirementKind.ColonizationFleetSeedCapitalShip, BuildingType.None)]
        public void Select_WithStrategicReserveExemptionBelowRefinedReserve_SelectsProposal(
            AIProductionRequirementKind demandKind,
            BuildingType buildingType
        )
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                demandKind,
                buildingType
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [TestCase(AIProductionRequirementKind.FleetCapitalShip)]
        [TestCase(AIProductionRequirementKind.FleetSeedCapitalShip)]
        [TestCase(AIProductionRequirementKind.FleetStarfighter)]
        [TestCase(AIProductionRequirementKind.FleetRegiment)]
        [TestCase(AIProductionRequirementKind.GarrisonRegimentReserve)]
        public void Select_WithMilitaryProductionBelowRefinedReserve_SelectsProposal(
            AIProductionRequirementKind demandKind
        )
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                demandKind,
                BuildingType.None
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [TestCase(
            AIProductionRequirementKind.ConstructionFacility,
            BuildingType.ConstructionFacility
        )]
        [TestCase(AIProductionRequirementKind.Shipyard, BuildingType.Shipyard)]
        [TestCase(AIProductionRequirementKind.TrainingFacility, BuildingType.TrainingFacility)]
        public void Select_WithStrategicFacilityBelowRefinedReserve_SelectsProposal(
            AIProductionRequirementKind demandKind,
            BuildingType buildingType
        )
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                demandKind,
                buildingType
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [Test]
        public void Select_WithShipyardAndHigherScoredDefense_DrainsDefenseProduction()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal defense = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.PlanetaryDefense,
                BuildingType.Defense,
                score: 100
            );
            AIManufactureProposal shipyard = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.Shipyard,
                BuildingType.Shipyard,
                score: 1
            );
            context.AddProposal(defense);
            context.AddProposal(shipyard);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_WithQueuedDefenseAndStrategicManufacturing_DrainsDefenseProduction()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal higherScore = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.PlanetaryDefense,
                BuildingType.Defense,
                constructionCost: 30,
                score: 100
            );
            AIManufactureProposal lowerScore = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.Shipyard,
                BuildingType.Shipyard,
                constructionCost: 30,
                score: 90
            );
            context.AddProposal(lowerScore);
            context.AddProposal(higherScore);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_WithHigherScoredDefense_DrainsForStrategicManufacturing()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal higherTotalCost = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.PlanetaryDefense,
                BuildingType.Defense,
                constructionCost: 60,
                score: 100
            );
            AIManufactureProposal affordable = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.Shipyard,
                BuildingType.Shipyard,
                constructionCost: 40,
                score: 90
            );
            context.AddProposal(higherTotalCost);
            context.AddProposal(affordable);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_WithLongProductionOrder_UsesAverageHorizonConsumption()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.PlanetaryDefense,
                BuildingType.Defense,
                constructionCost: 400,
                score: 100
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [Test]
        public void Select_WithHigherScoredReplacement_DrainsCurrentProduction()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal replacement = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.Shipyard,
                BuildingType.Shipyard,
                score: 200
            );
            AIManufactureProposal continuation = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.PlanetaryDefense,
                BuildingType.Defense,
                score: 100
            );
            context.AddProposal(replacement);
            context.AddProposal(continuation);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Select_WithLowerScoredStrategicReplacement_DrainsDefenseProduction()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal continuation = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.PlanetaryDefense,
                BuildingType.Defense,
                score: 200
            );
            AIManufactureProposal replacement = CreateManufactureProposal(
                context,
                producer,
                AIProductionRequirementKind.Shipyard,
                BuildingType.Shipyard,
                score: 100
            );
            context.AddProposal(continuation);
            context.AddProposal(replacement);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
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
            AIProductionRequirement preferredDemand = new AIProductionRequirement(
                "preferred-demand",
                AIProductionRequirementKind.Mine,
                ManufacturingType.Building,
                BuildingType.Mine,
                preferredProducer,
                1,
                100
            );
            AIProductionRequirement flexibleDemand = new AIProductionRequirement(
                "flexible-demand",
                AIProductionRequirementKind.Mine,
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

            Assert.AreEqual(2, selected.Count);
            Assert.AreSame(preferredProposal, selected[0]);
            Assert.AreSame(fallbackProducer, ((AIManufactureProposal)selected[1]).ProducerPlanet);
            Assert.AreSame(preferredProducer, flexibleProposal.ProducerPlanet);
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
                rawResourceNodes: 3
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
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
        /// Creates a manufacturing context with a bounded refined-material commitment budget.
        /// </summary>
        /// <param name="producer">The planet containing the construction facilities.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateRefinedMaterialCommitmentContext(out Planet producer)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.RefinedMaterialReservePercent = 50;
            empire.Settings.RefinementMultiplier = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "commitment-system");
            producer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "commitment-producer",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 2
            );
            for (int index = 0; index < 2; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    producer,
                    $"construction-yard-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
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

            Building queued = AITestSceneBuilder.CreateBuildingTemplate(
                "reserve-Defense",
                BuildingType.Defense,
                ManufacturingType.None
            );
            queued.OwnerInstanceID = empire.InstanceID;
            queued.ConstructionCost = 100;
            queued.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(queued, producer);
            producer.AddToManufacturingQueue(queued);
            empire.RefinedMaterialStockpile = 209;
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>
        /// Adds a planet with one building-production facility.
        /// </summary>
        /// <param name="game">The game receiving the planet.</param>
        /// <param name="sector">The containing sector.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="planetId">The planet identifier.</param>
        /// <returns>The building producer planet.</returns>
        private static Planet AddBuildingProducer(
            GameRoot game,
            PlanetSector sector,
            Faction faction,
            string planetId
        )
        {
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                planetId,
                faction.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                $"{planetId}-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            return planet;
        }

        /// <summary>
        /// Creates a scored building proposal with distinct producer and destination planets.
        /// </summary>
        /// <param name="destination">The planet receiving the building.</param>
        /// <param name="producer">The planet manufacturing the building.</param>
        /// <param name="kind">The demand category.</param>
        /// <param name="buildingType">The manufactured building type.</param>
        /// <param name="score">The proposal score.</param>
        /// <returns>The scored manufacturing proposal.</returns>
        private static AIManufactureProposal CreateBuildingProposal(
            Planet destination,
            Planet producer,
            AIProductionRequirementKind kind,
            BuildingType buildingType,
            double score
        )
        {
            Building building = AITestSceneBuilder.CreateBuildingTemplate(
                $"{buildingType}-template",
                buildingType,
                ManufacturingType.None
            );
            building.MaintenanceCost = 0;
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionRequirement(
                    $"{kind}-demand",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    destination,
                    1,
                    100
                ),
                producer,
                new Technology(building)
            );
            proposal.SetScore(score);
            return proposal;
        }

        /// <summary>
        /// Creates a scored building-production proposal.
        /// </summary>
        /// <param name="producer">The planet producing the building.</param>
        /// <param name="kind">The demand kind represented by the proposal.</param>
        /// <param name="buildingType">The building type to manufacture.</param>
        /// <returns>The building-production proposal.</returns>
        /// <param name="context">The context.</param>
        /// <param name="constructionCost">The construction cost.</param>
        /// <param name="score">The score.</param>
        private static AIManufactureProposal CreateManufactureProposal(
            AITurnContext context,
            Planet producer,
            AIProductionRequirementKind kind,
            BuildingType buildingType,
            int constructionCost = 10,
            double score = 100
        )
        {
            IManufacturable product;
            ManufacturingType manufacturingType;
            Rebellion.SceneGraph.ContainerNode destination = producer;
            switch (kind)
            {
                case AIProductionRequirementKind.FleetCapitalShip:
                case AIProductionRequirementKind.FleetSeedCapitalShip:
                case AIProductionRequirementKind.ColonizationFleetSeedCapitalShip:
                    product = AITestSceneBuilder.CreateCapitalShip(
                        $"reserve-{kind}",
                        context.Faction.InstanceID
                    );
                    manufacturingType = ManufacturingType.Ship;
                    break;
                case AIProductionRequirementKind.FleetStarfighter:
                    product = AITestSceneBuilder.CreateStarfighter(
                        $"reserve-{kind}",
                        context.Faction.InstanceID
                    );
                    manufacturingType = ManufacturingType.Ship;
                    break;
                case AIProductionRequirementKind.FleetRegiment:
                case AIProductionRequirementKind.GarrisonRegimentReserve:
                    product = AITestSceneBuilder.CreateRegiment(
                        $"reserve-{kind}",
                        context.Faction.InstanceID
                    );
                    manufacturingType = ManufacturingType.Troop;
                    break;
                default:
                    product = AITestSceneBuilder.CreateBuildingTemplate(
                        $"reserve-{buildingType}",
                        buildingType,
                        ManufacturingType.None
                    );
                    manufacturingType = ManufacturingType.Building;
                    break;
            }

            product.ConstructionCost = constructionCost;
            product.MaintenanceCost = 0;

            if (
                kind
                is AIProductionRequirementKind.FleetCapitalShip
                    or AIProductionRequirementKind.FleetStarfighter
                    or AIProductionRequirementKind.FleetRegiment
            )
            {
                Fleet fleet = EntityFactory.CreateFleet(
                    $"reserve-fleet-{kind}",
                    context.Faction.InstanceID
                );
                CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                    $"reserve-carrier-{kind}",
                    context.Faction.InstanceID,
                    regimentCapacity: 4,
                    starfighterCapacity: 4
                );
                context.Game.AttachNode(fleet, producer);
                context.Game.AttachNode(carrier, fleet);
                destination = fleet;
            }
            AIProductionRequirement demand = new AIProductionRequirement(
                $"reserve-{kind}",
                kind,
                manufacturingType,
                buildingType,
                destination,
                1,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(product)
            );
            proposal.SetScore(score);
            return proposal;
        }

        /// <summary>
        /// Creates two independent production-facility proposals backed by separate producers.
        /// </summary>
        /// <param name="allocationPercent">Shared maintenance allocation for production facilities.</param>
        /// <param name="first">The first facility proposal.</param>
        /// <param name="second">The second facility proposal.</param>
        /// <param name="quantity">The quantity represented by the first proposal.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateFacilityExpansionContext(
            int allocationPercent,
            out AIManufactureProposal first,
            out AIManufactureProposal second,
            int quantity = 1
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.ProductionFacilityMaintenanceAllocationPercent =
                allocationPercent;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "facility-system");
            Planet firstPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "first-producer",
                empire.InstanceID,
                rawResourceNodes: 1
            );
            Planet secondPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "second-producer",
                empire.InstanceID,
                rawResourceNodes: 1
            );
            foreach (Planet planet in new[] { firstPlanet, secondPlanet })
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"{planet.InstanceID}-construction-yard",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"{planet.InstanceID}-mine",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"{planet.InstanceID}-refinery",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }

            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard-template",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.MaintenanceCost = 10;
            first = CreateFacilityExpansionProposal(firstPlanet, shipyard, 100, quantity);
            second = CreateFacilityExpansionProposal(secondPlanet, shipyard, 90);
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>
        /// Creates a scored shipyard-expansion proposal at one planet.
        /// </summary>
        /// <param name="planet">The producer and destination planet.</param>
        /// <param name="shipyard">The shipyard template.</param>
        /// <param name="score">The proposal score.</param>
        /// <param name="quantity">The proposed shipyard count.</param>
        /// <returns>The facility-expansion proposal.</returns>
        private static AIManufactureProposal CreateFacilityExpansionProposal(
            Planet planet,
            Building shipyard,
            float score,
            int quantity = 1
        )
        {
            AIProductionRequirement demand = new AIProductionRequirement(
                $"shipyard-{planet.InstanceID}",
                AIProductionRequirementKind.Shipyard,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                planet,
                quantity,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shipyard)
            );
            proposal.SetScore(score);
            return proposal;
        }

        /// <summary>
        /// Creates empty context.
        /// </summary>
        /// <returns>The created empty context.</returns>
        private static AITurnContext CreateEmptyContext()
        {
            return new AITurnContext(null, null, null, null, null, null, null, null);
        }
    }
}
