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
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Scorers
{
    [TestFixture]
    public class AIProductionProposalScorerTests
    {
        [TestCase(1, 1, 1)]
        [TestCase(0.4, 0.04, 0.1)]
        public void Score_IdleProducer_HasNoContinuationOrSwitchingPenalty(
            double completionWeight,
            double interruptionWeight,
            double demandWeight
        )
        {
            AITurnContext context = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            AIManufactureProposal replacement = AITestSceneBuilder.CreateProductionChoices(
                context,
                order
            )[1];
            Assert.IsTrue(
                context.Manufacturing.CancelManufacturing(order, context.Faction.InstanceID)
            );
            context.Game.Config.AI.Selection.ProductionUtility.CompletionTime.Weight = 0;
            context.Game.Config.AI.Selection.ProductionUtility.InterruptionCost.Weight = 0;
            context.Game.Config.AI.Selection.DemandUtility.Weight = demandWeight;
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();
            double initial = scorer.Score(context, replacement);

            context.Game.Config.AI.Selection.ProductionUtility.CompletionTime.Weight =
                completionWeight;
            context.Game.Config.AI.Selection.ProductionUtility.InterruptionCost.Weight =
                interruptionWeight;

            Assert.AreEqual(initial, scorer.Score(context, replacement));
        }

        [Test]
        public void Score_ContinuingOrderMakesProgress_IncreasesContinuationValue()
        {
            AITurnContext context = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            context.Game.Config.AI.Selection.ProductionUtility.CompletionTime.Weight = 0.2;
            context.Game.Config.AI.Selection.ProductionUtility.CompletionTime.SaturationValue = 100;
            AIManufactureProposal continuation = AITestSceneBuilder.CreateProductionChoices(
                context,
                order
            )[0];
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();
            double initial = scorer.Score(context, continuation);

            order.ManufacturingProgress = 90;

            Assert.Greater(scorer.Score(context, continuation), initial);
        }

        [Test]
        public void Score_ReplacingUnneededOrderWithProgress_DoesNotAddInterruptionPenalty()
        {
            AITurnContext context = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            context.Game.Config.AI.Selection.ProductionUtility.InterruptionCost.Weight = 1;
            context.Game.Config.AI.Selection.ProductionUtility.InterruptionCost.SaturationValue =
                100;
            AIManufactureProposal replacement = AITestSceneBuilder.CreateProductionChoices(
                context,
                order
            )[1];
            context.SetProductionDemands(new[] { replacement.Demand });
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();
            double initial = scorer.Score(context, replacement);

            order.ManufacturingProgress = 90;

            Assert.AreEqual(initial, scorer.Score(context, replacement));
        }

        [Test]
        public void Score_ReplacingOrderWithMoreProgress_IncreasesSwitchingCost()
        {
            AITurnContext context = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            context.Game.Config.AI.Selection.ProductionUtility.InterruptionCost.Weight = 0.2;
            context.Game.Config.AI.Selection.ProductionUtility.InterruptionCost.SaturationValue =
                100;
            AIManufactureProposal replacement = AITestSceneBuilder.CreateProductionChoices(
                context,
                order
            )[1];
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();
            double initial = scorer.Score(context, replacement);

            order.ManufacturingProgress = 90;

            Assert.Less(scorer.Score(context, replacement), initial);
        }

        /// <summary>
        /// Removes the initial-foundation bonus once a construction yard is committed.
        /// </summary>
        /// <param name="status">The committed yard's manufacturing state.</param>
        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Delivering)]
        public void Score_ConstructionYardAlreadyCommitted_DoesNotAddFoundationBonus(
            ManufacturingStatus status
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "outer-rim");
            sector.SectorType = PlanetSectorType.OuterRim;
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID
            );
            Building committed = AITestSceneBuilder.AddProductionFacility(
                game,
                destination,
                "committed-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            committed.ManufacturingStatus = status;
            Building template = AITestSceneBuilder.CreateBuildingTemplate(
                "construction-template",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "construction-demand",
                    AIProductionDemandKind.ConstructionFacility,
                    ManufacturingType.Building,
                    BuildingType.ConstructionFacility,
                    destination,
                    1,
                    targetCount: 5,
                    baseDemandPercent: 100
                ),
                destination,
                new Technology(template),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();
            game.Config.AI.Selection.ProductionUtility.ColonyFoundation.Weight = 0;
            double withoutBonus = scorer.Score(context, proposal);
            game.Config.AI.Selection.ProductionUtility.ColonyFoundation.Weight = 1;

            Assert.AreEqual(withoutBonus, scorer.Score(context, proposal));
        }

        [Test]
        public void GetDemandPressure_ShieldsElsewhereExceedPortfolio_KeepsLocalDefenseRequest()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.AI.Infrastructure.StaticDefensePortfolioPercent = 1;
            game.Config.AI.Infrastructure.DemandUtility.FacilityPortfolio.Weight = 1000;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID
            );
            Planet defended = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "defended",
                faction.InstanceID
            );
            for (int index = 0; index < 4; index++)
            {
                Building shield = AITestSceneBuilder.AddProductionFacility(
                    game,
                    defended,
                    $"shield-{index}",
                    BuildingType.Defense,
                    ManufacturingType.None
                );
                shield.ShieldStrength = 80;
            }
            AIProductionDemand demand = new(
                "missing-shield",
                AIProductionDemandKind.PlanetaryDefense,
                ManufacturingType.Building,
                BuildingType.Defense,
                destination,
                1,
                targetCount: 1,
                baseDemandPercent: 50
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);

            Assert.Greater(AIProductionProposalScorer.GetDemandPressure(context, demand), 0);
        }

        [Test]
        public void Score_FacilityRemovalProposal_ReturnsZero()
        {
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();

            double score = scorer.Score(
                null,
                new AIFacilityRemovalProposal(null, BuildingType.Shipyard, 1, 0)
            );

            Assert.Zero(score);
        }

        [Test]
        public void Score_WithDifferentBaseDemandPercent_PreservesUrgencyDifference()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.DemandUtility.Weight = 1;
            game.Config.AI.Selection.ProductionUtility.TravelCost.Weight = 0;
            game.Config.AI.Selection.ProductionUtility.HeadroomRisk.Weight = 0;
            game.Config.AI.Selection.ProductionUtility.Shortfall.Weight = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "producer",
                empire.InstanceID
            );
            Building buildingTemplate = AITestSceneBuilder.CreateBuildingTemplate(
                "construction-yard",
                BuildingType.ConstructionFacility
            );
            buildingTemplate.OwnerInstanceID = empire.InstanceID;
            Technology building = new Technology(buildingTemplate);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();

            double lowScore = scorer.Score(
                context,
                CreateBuildingProposal(producer, building, 100)
            );
            double highScore = scorer.Score(
                context,
                CreateBuildingProposal(producer, building, 500)
            );

            Assert.Greater(highScore, lowScore);
        }

        [Test]
        public void Score_WithFleetReinforcement_DeductsTravelPenalty()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.ProductionUtility.TravelCost.Weight = 1;
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet nearProducer = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "near",
                empire.InstanceID,
                positionX: 0,
                positionY: 0
            );
            Planet farProducer = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "far",
                empire.InstanceID,
                positionX: 240,
                positionY: 0
            );
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "fleet-world",
                empire.InstanceID,
                positionX: 1,
                positionY: 0
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, fleetPlanet);
            AIProductionDemand demand = new AIProductionDemand(
                "fleet-regiment-demand",
                AIProductionDemandKind.FleetRegiment,
                ManufacturingType.Troop,
                BuildingType.None,
                fleet,
                1,
                baseDemandPercent: 100
            );
            Technology regiment = new Technology(
                AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();

            double nearScore = scorer.Score(
                context,
                new AIManufactureProposal(demand, nearProducer, regiment, false)
            );
            double farScore = scorer.Score(
                context,
                new AIManufactureProposal(demand, farProducer, regiment, false)
            );

            Assert.Greater(nearScore, farScore);
            Assert.Zero(farScore);
        }

        [TestCase(AIProductionDemandKind.Mine, BuildingType.Mine)]
        [TestCase(AIProductionDemandKind.Refinery, BuildingType.Refinery)]
        public void Score_WithEconomyRecoveryBelowMaintenanceReserve_ReturnsPositiveScore(
            AIProductionDemandKind kind,
            BuildingType buildingType
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.DemandUtility.Weight = 1;
            game.Config.AI.Selection.ProductionUtility.TravelCost.Weight = 0;
            game.Config.AI.Selection.ProductionUtility.HeadroomRisk.Weight = 0;
            game.Config.AI.Selection.ProductionUtility.Shortfall.Weight = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "producer",
                empire.InstanceID
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            game.Config.AI.Selection.MaintenanceHeadroomReserve =
                context.Assessment.ProjectedMaintenanceHeadroom + 100;
            Building template = AITestSceneBuilder.CreateBuildingTemplate(
                $"{kind}-template",
                buildingType,
                ManufacturingType.None
            );
            template.MaintenanceCost = 10;
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    $"{kind}-demand",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    producer,
                    1,
                    baseDemandPercent: 100
                ),
                producer,
                new Technology(template),
                false
            );

            double score = new AIProductionProposalScorer().Score(context, proposal);

            Assert.Greater(score, 0);
        }

        [Test]
        public void Score_WithOuterRimEconomyDestination_ReturnsPositiveScore()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.ProductionUtility.TravelCost.Weight = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "outer-rim");
            sector.SectorType = PlanetSectorType.OuterRim;
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "economy-world",
                empire.InstanceID
            );
            Building template = AITestSceneBuilder.CreateBuildingTemplate(
                "refinery",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "refinery-demand",
                    AIProductionDemandKind.Refinery,
                    ManufacturingType.Building,
                    BuildingType.Refinery,
                    destination,
                    1,
                    baseDemandPercent: 100
                ),
                destination,
                new Technology(template),
                false
            );

            double score = new AIProductionProposalScorer().Score(
                AITestSceneBuilder.CreateContext(game, empire),
                proposal
            );

            Assert.Greater(score, 0);
        }

        [Test]
        public void GetDemandPressure_WithPrimaryFacilityHub_AddsPrimaryHubPriority()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.DemandUtility.PrimaryHub.Weight = 0.5;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "training-hub",
                empire.InstanceID
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIProductionDemand primaryDemand = new AIProductionDemand(
                "primary-training-hub",
                AIProductionDemandKind.TrainingFacility,
                ManufacturingType.Building,
                BuildingType.TrainingFacility,
                destination,
                4,
                targetCount: 4,
                baseDemandPercent: 100,
                infrastructureHubIndex: 0
            );
            AIProductionDemand secondaryDemand = new AIProductionDemand(
                "secondary-training-hub",
                AIProductionDemandKind.TrainingFacility,
                ManufacturingType.Building,
                BuildingType.TrainingFacility,
                destination,
                4,
                targetCount: 4,
                baseDemandPercent: 100,
                infrastructureHubIndex: 1
            );

            double primaryPressure = AIProductionProposalScorer.GetDemandPressure(
                context,
                primaryDemand
            );
            double secondaryPressure = AIProductionProposalScorer.GetDemandPressure(
                context,
                secondaryDemand
            );

            Assert.AreEqual(50, primaryPressure - secondaryPressure);
        }

        /// <summary>
        /// Raises priority for combat reinforcements when headquarters defense is short.
        /// </summary>
        /// <param name="kind">The combat reinforcement being manufactured.</param>
        [TestCase(AIProductionDemandKind.FleetCapitalShip)]
        [TestCase(AIProductionDemandKind.FleetStarfighter)]
        public void Score_HeadquartersDefenseBelowTarget_AddsReinforcementPriority(
            AIProductionDemandKind kind
        )
        {
            AITurnContext context = CreateHeadquartersReinforcementContext(kind, out var proposal);
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();
            context.Game.Config.AI.Selection.ProductionUtility.HeadquartersReinforcement.Weight = 0;
            double withoutPriority = scorer.Score(context, proposal);
            context.Game.Config.AI.Selection.ProductionUtility.HeadquartersReinforcement.Weight = 1;

            Assert.Greater(scorer.Score(context, proposal), withoutPriority);
        }

        /// <summary>
        /// Does not reward additional combat strength already covered by committed ships.
        /// </summary>
        /// <param name="status">The reinforcement's current production state.</param>
        [TestCase(ManufacturingStatus.Complete)]
        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Delivering)]
        public void Score_HeadquartersDefenseCoveredByCommittedShips_DoesNotAddPriority(
            ManufacturingStatus status
        )
        {
            AITurnContext context = CreateHeadquartersReinforcementContext(
                AIProductionDemandKind.FleetCapitalShip,
                out var proposal
            );
            CapitalShip reinforcement = AITestSceneBuilder.CreateCapitalShip(
                "committed-reinforcement",
                context.Faction.InstanceID,
                combatStrength: 2000
            );
            reinforcement.ManufacturingStatus = status;
            if (status == ManufacturingStatus.Delivering)
                reinforcement.Movement = new MovementState { TransitTicks = 10 };
            context.Game.AttachNode(reinforcement, proposal.Demand.DestinationFleet);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            Assert.Zero(new AIProductionProposalScorer().Score(context, proposal));
        }

        /// <summary>
        /// Does not apply headquarters defense priority to other fleet assignments.
        /// </summary>
        /// <param name="orderType">The fleet's assigned action.</param>
        /// <param name="isHeadquarters">Whether the defended planet is the faction headquarters.</param>
        [TestCase(FleetOrderType.Attack, true)]
        [TestCase(FleetOrderType.Defend, false)]
        public void Score_FleetNotDefendingHeadquarters_DoesNotAddPriority(
            FleetOrderType orderType,
            bool isHeadquarters
        )
        {
            AITurnContext context = CreateHeadquartersReinforcementContext(
                AIProductionDemandKind.FleetCapitalShip,
                out var proposal
            );
            proposal.Demand.DestinationFleet.Order.OrderType = orderType;
            if (!isHeadquarters)
                context.Faction.HQInstanceID = "other-headquarters";
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            Assert.Zero(new AIProductionProposalScorer().Score(context, proposal));
        }

        /// <summary>
        /// Reduces the reinforcement bonus as the remaining defense gap closes.
        /// </summary>
        [Test]
        public void Score_HeadquartersDefenseGapShrinks_ReducesPriority()
        {
            AITurnContext context = CreateHeadquartersReinforcementContext(
                AIProductionDemandKind.FleetCapitalShip,
                out var proposal
            );
            AIProductionProposalScorer scorer = new AIProductionProposalScorer();
            double before = scorer.Score(context, proposal);
            context.Game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "additional-escort",
                    context.Faction.InstanceID,
                    combatStrength: 100
                ),
                proposal.Demand.DestinationFleet
            );
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            Assert.Less(scorer.Score(context, proposal), before);
        }

        /// <summary>
        /// Creates an understrength headquarters fleet with other scoring considerations disabled.
        /// </summary>
        /// <param name="kind">The reinforcement demand category.</param>
        /// <param name="proposal">The proposal to reinforce the headquarters fleet.</param>
        /// <returns>The faction turn used to evaluate reinforcement priority.</returns>
        private static AITurnContext CreateHeadquartersReinforcementContext(
            AIProductionDemandKind kind,
            out AIManufactureProposal proposal
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.HeadquartersDefenseCombatPercent = 0;
            game.Config.AI.Selection.DemandUtility.Weight = 0;
            game.Config.AI.Selection.ProductionUtility.TravelCost.Weight = 0;
            game.Config.AI.Selection.ProductionUtility.HeadroomRisk.Weight = 0;
            game.Config.AI.Selection.ProductionUtility.Shortfall.Weight = 0;
            game.Config.AI.Selection.ProductionUtility.HeadquartersReinforcement.Weight = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "headquarters-sector");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "headquarters",
                faction.InstanceID
            );
            faction.HQInstanceID = headquarters.InstanceID;
            headquarters.IsHeadquarters = true;
            Fleet fleet = EntityFactory.CreateFleet("headquarters-fleet", faction.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                TargetPlanetId = headquarters.InstanceID,
            };
            game.AttachNode(fleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("escort", faction.InstanceID, 50),
                fleet
            );
            Technology product =
                kind == AIProductionDemandKind.FleetStarfighter
                    ? new Technology(
                        AITestSceneBuilder.CreateStarfighter("fighter", faction.InstanceID)
                    )
                    : new Technology(
                        AITestSceneBuilder.CreateCapitalShip("hull", faction.InstanceID)
                    );
            proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "headquarters-reinforcement",
                    kind,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    fleet,
                    1,
                    baseDemandPercent: 90,
                    targetCount: 100,
                    deficitCount: 90
                ),
                headquarters,
                product,
                false
            );
            return AITestSceneBuilder.CreateContext(game, faction);
        }

        /// <summary>
        /// Creates a building-production proposal for a test scenario.
        /// </summary>
        /// <param name="producer">The producer value.</param>
        /// <param name="building">The building value.</param>
        /// <param name="baseDemandPercent">The configured base demand percentage.</param>
        /// <returns>The operation result.</returns>
        private static AIManufactureProposal CreateBuildingProposal(
            Planet producer,
            Technology building,
            int baseDemandPercent
        )
        {
            return new AIManufactureProposal(
                new AIProductionDemand(
                    $"building-{baseDemandPercent}",
                    AIProductionDemandKind.ConstructionFacility,
                    ManufacturingType.Building,
                    BuildingType.ConstructionFacility,
                    producer,
                    1,
                    baseDemandPercent: baseDemandPercent,
                    targetCount: 1
                ),
                producer,
                building,
                false
            );
        }
    }
}
