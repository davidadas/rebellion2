using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AISelectionPhaseTests
    {
        [Test]
        public void Select_IndustryUsesAllocatedSpace_PreservesShieldAndRejectsExtraEconomy()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: 7,
                rawResourceNodes: 1
            );
            Planet industryProducer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "industry",
                faction.InstanceID
            );
            Planet defenseProducer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "defense",
                faction.InstanceID
            );
            for (int index = 0; index < 6; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    industryProducer,
                    $"constructor-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            AITestSceneBuilder.AddProductionFacility(
                game,
                defenseProducer,
                "constructor",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            Assert.IsTrue(
                context.StrategicPlan.TryAllocateInfrastructureHub(hub, BuildingType.Shipyard, 6)
            );
            Building yard = AITestSceneBuilder.CreateBuildingTemplate(
                "yard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense
            );
            shield.ShieldStrength = 80;
            Building mine = AITestSceneBuilder.CreateBuildingTemplate("mine", BuildingType.Mine);
            AIManufactureProposal industry = new(
                new AIProductionDemand(
                    "industry",
                    AIProductionDemandKind.Shipyard,
                    ManufacturingType.Building,
                    BuildingType.Shipyard,
                    hub,
                    6
                ),
                industryProducer,
                new Technology(yard),
                false
            );
            AIManufactureProposal defense = new(
                new AIProductionDemand(
                    "defense",
                    AIProductionDemandKind.PlanetaryDefense,
                    ManufacturingType.Building,
                    BuildingType.Defense,
                    hub,
                    1
                ),
                defenseProducer,
                new Technology(shield),
                false
            );
            AIManufactureProposal economy = new(
                new AIProductionDemand(
                    "economy",
                    AIProductionDemandKind.Mine,
                    ManufacturingType.Building,
                    BuildingType.Mine,
                    hub,
                    1
                ),
                defenseProducer,
                new Technology(mine),
                false
            );
            industry.SetScore(100);
            defense.SetScore(1);
            economy.SetScore(1000);
            context.AddProposal(economy);
            context.AddProposal(industry);
            context.AddProposal(defense);

            var selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(2, selected.Count);
            Assert.IsFalse(
                selected
                    .OfType<AIManufactureProposal>()
                    .Any(proposal => proposal.Demand.Kind == AIProductionDemandKind.Mine)
            );
            foreach (var proposal in selected)
                proposal.Execute(context);
            Assert.AreEqual(6, hub.GetTotalBuildingTypeCount(BuildingType.Shipyard));
            Assert.AreEqual(1, hub.GetTotalBuildingTypeCount(BuildingType.Defense));
            Assert.AreEqual(0, hub.GetTotalBuildingTypeCount(BuildingType.Mine));
        }

        [Test]
        public void Select_MultipleRoutineDefenseRequests_StartsOnlyOneFillerPerLane()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "producer",
                faction.InstanceID
            );
            for (int index = 0; index < 2; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"yard-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense
            );
            shield.ShieldStrength = 80;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            for (int index = 0; index < 2; index++)
            {
                AIManufactureProposal proposal = new AIManufactureProposal(
                    new AIProductionDemand(
                        $"filler-{index}",
                        AIProductionDemandKind.PlanetaryDefense,
                        ManufacturingType.Building,
                        BuildingType.Defense,
                        planet,
                        1
                    ),
                    planet,
                    new Technology(shield),
                    false
                );
                proposal.SetScore(1);
                context.AddProposal(proposal);
            }

            var selected = new AISelectionPhase().Select(context);
            Assert.AreEqual(1, selected.Count);
            selected.Single().Execute(context);

            Assert.IsTrue(
                planet
                    .GetManufacturingQueue()[ManufacturingType.Building]
                    .Single()
                    .IsAIProductionFiller
            );
        }

        [TestCase(false, BuildingType.Mine)]
        [TestCase(true, BuildingType.Defense)]
        public void Select_StaticDefenseAndEconomicExpansion_UsesImmediateThreatPriority(
            bool emergency,
            BuildingType expectedType
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                faction.InstanceID,
                rawResourceNodes: 1
            );
            Planet producer = AddBuildingProducer(game, sector, faction, "producer");
            AIManufactureProposal economy = CreateBuildingProposal(
                destination,
                producer,
                AIProductionDemandKind.Mine,
                BuildingType.Mine,
                100
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense,
                ManufacturingType.None
            );
            shield.MaintenanceCost = 0;
            AIManufactureProposal defense = new AIManufactureProposal(
                new AIProductionDemand(
                    "defense",
                    AIProductionDemandKind.PlanetaryDefense,
                    ManufacturingType.Building,
                    BuildingType.Defense,
                    destination,
                    1,
                    isEmergencyDefense: emergency
                ),
                producer,
                new Technology(shield),
                false
            );
            defense.SetScore(1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            context.AddProposal(defense);
            context.AddProposal(economy);

            List<AIManufactureProposal> selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIManufactureProposal>()
                .ToList();

            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual(expectedType, selected.Single().Demand.BuildingType);
        }

        [Test]
        public void Select_WithMultipleIdleTrainingLanes_StartsOneFillerOnEachPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            List<Planet> planets = new();
            for (int index = 0; index < 2; index++)
            {
                Planet planet = AITestSceneBuilder.AddPlanet(
                    game,
                    sector,
                    $"training-{index}",
                    faction.InstanceID
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"facility-{index}",
                    BuildingType.TrainingFacility,
                    ManufacturingType.Troop
                );
                planets.Add(planet);
            }
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                faction.InstanceID,
                MissionTypeIDs.Sabotage
            );
            template.ConstructionCost = 10;
            faction.ResearchQueue[ManufacturingType.Troop] = new List<Technology> { new(template) };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            context.SetProductionDemands(
                new Rebellion.AI.Demands.AIProductionDemandGenerator()
                    .BuildDemands(context)
                    .Where(demand => demand.Kind == AIProductionDemandKind.IdleSpecialForces)
            );
            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);
            foreach (AIProposal proposal in proposals)
                proposal.SetScore(50);
            context.AddProposals(proposals);

            List<AIManufactureProposal> selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIManufactureProposal>()
                .ToList();
            foreach (AIManufactureProposal proposal in selected)
                proposal.Execute(context);

            CollectionAssert.AreEquivalent(
                planets,
                selected.Select(proposal => proposal.ProducerPlanet)
            );
            foreach (Planet planet in planets)
            {
                SpecialForces order = planet
                    .GetManufacturingQueue()[ManufacturingType.Troop]
                    .OfType<SpecialForces>()
                    .Single();
                Assert.IsTrue(order.IsAIProductionFiller);
                Assert.AreEqual(planet.InstanceID, order.ProducerPlanetID);
                Assert.AreSame(
                    planet,
                    selected.Single(proposal => proposal.ProducerPlanet == planet).Destination
                );
            }
        }

        [Test]
        public void Select_WithFillerScoredAboveNormalDemand_SelectsNormalDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "training",
                faction.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                faction.InstanceID,
                MissionTypeIDs.Sabotage
            );
            template.ConstructionCost = 10;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal normal = new AIManufactureProposal(
                new AIProductionDemand(
                    "normal",
                    AIProductionDemandKind.SpecialForces,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    planet,
                    1,
                    template.TypeID
                ),
                planet,
                new Technology(template),
                true
            );
            AIManufactureProposal filler = new AIManufactureProposal(
                new AIProductionDemand(
                    "filler",
                    AIProductionDemandKind.IdleSpecialForces,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    planet,
                    1,
                    template.TypeID
                ),
                planet,
                new Technology(template),
                false
            );
            normal.SetScore(1);
            filler.SetScore(100);
            context.AddProposals(new List<AIProposal> { filler, normal });

            List<AIProposal> selected = new Rebellion.AI.Phases.AISelectionPhase().Select(context);

            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual(
                AIProductionDemandKind.SpecialForces,
                ((AIManufactureProposal)selected[0]).Demand.Kind
            );
        }

        [Test]
        public void Select_WithScoredProposals_ReturnsHighestScoreFirst()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal lowerScore = new TestAIProposal("lower");
            TestAIProposal higherScore = new TestAIProposal("higher");
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
        public void Select_WithConflictingFleetReservations_SelectsOnlyHighestScoredProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetRoleProposal lowerScore = new AIFleetRoleProposal(
                fleet,
                FleetRoleType.Colonization
            );
            AIFleetRoleProposal higherScore = new AIFleetRoleProposal(fleet, FleetRoleType.Battle);
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
                AIProductionDemandKind.Mine,
                BuildingType.Mine,
                100
            );
            AIManufactureProposal lower = CreateBuildingProposal(
                destination,
                secondProducer,
                AIProductionDemandKind.Refinery,
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
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, planet);
            AIFleetRoleProposal first = new AIFleetRoleProposal(fleet, FleetRoleType.Colonization);
            AIFleetRoleProposal second = new AIFleetRoleProposal(fleet, FleetRoleType.Battle);
            first.SetScore(10);
            second.SetScore(10);
            context.AddProposal(first);
            context.AddProposal(second);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { second }, selected);
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
            TestAIProposal proposal = new TestAIProposal("proposal");
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Select_WithNonPositiveScore_DoesNotSelectProposal()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal");
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
            AIProductionDemand demand = new AIProductionDemand(
                "shipyard-demand",
                AIProductionDemandKind.Shipyard,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                planet,
                1,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shipyard),
                false
            );
            proposal.SetScore(100);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Select_HigherScoringMandatoryManufacturingCompetesWithRelocation_SelectsOnlyRelocation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            empire.Settings.Headquarters = new HeadquartersSettings
            {
                FacilityTypeID = "mobile-headquarters",
                IsMobile = true,
            };
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "origin",
                empire.InstanceID,
                energyCapacity: 4
            );
            origin.IsHeadquarters = true;
            empire.HQInstanceID = origin.InstanceID;
            Building headquarters = AITestSceneBuilder.CreateBuildingTemplate(
                "mobile-headquarters",
                BuildingType.Headquarters
            );
            headquarters.OwnerInstanceID = empire.InstanceID;
            game.AttachNode(headquarters, origin);
            AITestSceneBuilder.AddProductionFacility(
                game,
                origin,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                origin,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                origin,
                "refinery",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 1,
                rawResourceNodes: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                destination,
                "mine",
                BuildingType.Mine,
                ManufacturingType.None
            );
            Fleet incoming = EntityFactory.CreateFleet("incoming", rebels.InstanceID);
            incoming.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(incoming, origin);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("incoming-ship", rebels.InstanceID, 200),
                incoming
            );
            Fleet defender = EntityFactory.CreateFleet("defender", empire.InstanceID);
            defender.RoleType = FleetRoleType.Battle;
            game.AttachNode(defender, origin);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("defender-ship", empire.InstanceID, 100),
                defender
            );
            AITestSceneBuilder.RevealPlanet(game, empire, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIHeadquartersRelocationProposal relocation = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();
            AIManufactureProposal manufacture = CreateManufactureProposal(
                context,
                origin,
                AIProductionDemandKind.ColonizationFleetSeedCapitalShip,
                BuildingType.None,
                score: 0.99,
                maintenanceCost: 1
            );
            relocation.SetScore(
                new AIHeadquartersRelocationProposalScorer().Score(context, relocation)
            );
            context.AddProposal(relocation);
            context.AddProposal(manufacture);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.Greater(manufacture.Score, relocation.Score);
            Assert.Greater(relocation.MaintenanceHeadroomCost, 0);
            CollectionAssert.AreEqual(new[] { relocation }, selected);
        }

        [TestCase(AIProductionDemandKind.Mine, BuildingType.Mine)]
        [TestCase(AIProductionDemandKind.Refinery, BuildingType.Refinery)]
        public void Select_WithEconomyRecoveryBelowMaintenanceReserve_SelectsProposal(
            AIProductionDemandKind kind,
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
                new AIProductionDemand(
                    $"{kind}-demand",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    planet,
                    1,
                    baseDemandPercent: 100
                ),
                planet,
                new Technology(economyBuilding),
                false
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
        public void Select_WithPartiallyAffordableFacilityBatch_PreservesFullBatchOrdering()
        {
            AITurnContext context = CreateFacilityExpansionContext(
                allocationPercent: 100,
                out AIManufactureProposal proposal,
                out AIManufactureProposal _,
                quantity: 4
            );
            context.Game.Config.AI.Selection.MaintenanceHeadroomReserve =
                context.Assessment.ProjectedMaintenanceHeadroom - 25;
            TestAIProposal independent = new TestAIProposal("independent");
            independent.SetScore(90);
            context.AddProposal(proposal);
            context.AddProposal(independent);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(2, selected.Count);
            Assert.AreEqual(2, ((AIManufactureProposal)selected[0]).GetManufacturingCount());
            Assert.AreEqual(4, proposal.GetManufacturingCount());
            Assert.Less(selected[0].Score, independent.Score);
            Assert.AreSame(independent, selected[1]);
        }

        [Test]
        public void Select_WithDiscretionaryProductionBelowRefinedReserve_SelectsProposal()
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [TestCase(AIProductionDemandKind.Mine, BuildingType.Mine)]
        [TestCase(AIProductionDemandKind.Refinery, BuildingType.Refinery)]
        [TestCase(AIProductionDemandKind.ColonizationFleetSeedCapitalShip, BuildingType.None)]
        public void Select_WithStrategicReserveExemptionBelowRefinedReserve_SelectsProposal(
            AIProductionDemandKind demandKind,
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

        [TestCase(AIProductionDemandKind.FleetCapitalShip)]
        [TestCase(AIProductionDemandKind.FleetSeedCapitalShip)]
        [TestCase(AIProductionDemandKind.FleetStarfighter)]
        [TestCase(AIProductionDemandKind.FleetRegiment)]
        [TestCase(AIProductionDemandKind.GarrisonRegimentReserve)]
        public void Select_WithMilitaryProductionBelowRefinedReserve_SelectsProposal(
            AIProductionDemandKind demandKind
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

        [Test]
        public void Select_WithMultipleIdleShipyardFighters_SelectsConfiguredConcurrentCount()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.IdleShipyardFighterReserveCount = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet higherPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "higher-planet",
                empire.InstanceID
            );
            Planet lowerPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "lower-planet",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                higherPlanet,
                "higher-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                lowerPlanet,
                "lower-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID
            );
            AIManufactureProposal higher = CreateIdleShipyardFighterProposal(
                higherPlanet,
                fighter,
                20
            );
            AIManufactureProposal lower = CreateIdleShipyardFighterProposal(
                lowerPlanet,
                fighter,
                10
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(lower);
            context.AddProposal(higher);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { higher }, selected);
        }

        [Test]
        public void Select_WithRegularAndIdlePlanetaryFighters_CountsBothAgainstIdleLimit()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.IdleShipyardFighterReserveCount = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet regularPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "regular-planet",
                empire.InstanceID
            );
            Planet idlePlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "idle-planet",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                regularPlanet,
                "regular-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                idlePlanet,
                "idle-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID
            );
            AIManufactureProposal regular = CreatePlanetaryStarfighterProposal(
                regularPlanet,
                fighter,
                20,
                usesIdleShipyardCapacity: false
            );
            AIManufactureProposal idle = CreatePlanetaryStarfighterProposal(
                idlePlanet,
                fighter,
                10,
                usesIdleShipyardCapacity: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(idle);
            context.AddProposal(regular);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { regular }, selected);
        }

        [TestCase(AIProductionDemandKind.ConstructionFacility, BuildingType.ConstructionFacility)]
        [TestCase(AIProductionDemandKind.Shipyard, BuildingType.Shipyard)]
        [TestCase(AIProductionDemandKind.TrainingFacility, BuildingType.TrainingFacility)]
        public void Select_WithStrategicFacilityBelowRefinedReserve_SelectsProposal(
            AIProductionDemandKind demandKind,
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
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                score: 100
            );
            AIManufactureProposal shipyard = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.Shipyard,
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
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                constructionCost: 30,
                score: 100
            );
            AIManufactureProposal lowerScore = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.Shipyard,
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
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                constructionCost: 60,
                score: 100
            );
            AIManufactureProposal affordable = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.Shipyard,
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
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                constructionCost: 400,
                score: 100,
                isEmergencyDefense: true
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
                AIProductionDemandKind.Shipyard,
                BuildingType.Shipyard,
                score: 200
            );
            AIManufactureProposal continuation = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.PlanetaryDefense,
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
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                score: 200
            );
            AIManufactureProposal replacement = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.Shipyard,
                BuildingType.Shipyard,
                score: 100
            );
            context.AddProposal(continuation);
            context.AddProposal(replacement);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_WithUnavailablePreferredManufacturingProducer_PreservesPrimaryOrdering()
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
            AIProductionDemand preferredDemand = new AIProductionDemand(
                "preferred-demand",
                AIProductionDemandKind.Mine,
                ManufacturingType.Building,
                BuildingType.Mine,
                preferredProducer,
                1,
                baseDemandPercent: 100
            );
            AIProductionDemand flexibleDemand = new AIProductionDemand(
                "flexible-demand",
                AIProductionDemandKind.Mine,
                ManufacturingType.Building,
                BuildingType.Mine,
                fallbackProducer,
                1,
                baseDemandPercent: 90
            );
            AIManufactureProposal preferredProposal = new AIManufactureProposal(
                preferredDemand,
                preferredProducer,
                new Technology(mine),
                false
            );
            AIManufactureProposal flexibleProposal = new AIManufactureProposal(
                flexibleDemand,
                new[] { preferredProducer, fallbackProducer },
                new Technology(mine),
                false
            );
            preferredProposal.SetScore(100);
            flexibleProposal.SetScore(90);
            TestAIProposal independent = new TestAIProposal("independent");
            independent.SetScore(80);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(preferredProposal);
            context.AddProposal(flexibleProposal);
            context.AddProposal(independent);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(3, selected.Count);
            Assert.AreSame(preferredProposal, selected[0]);
            AIManufactureProposal selectedFallback = (AIManufactureProposal)selected[1];
            Assert.AreSame(fallbackProducer, selectedFallback.ProducerPlanet);
            Assert.AreEqual(
                new AIProductionProposalScorer().Score(context, selectedFallback),
                selectedFallback.Score
            );
            Assert.Less(selectedFallback.Score, independent.Score);
            Assert.AreSame(independent, selected[2]);
            Assert.AreSame(preferredProducer, flexibleProposal.ProducerPlanet);
        }

        [Test]
        public void Execute_Default_StoresSelectedProposalsOnContext()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal");
            proposal.SetScore(10);
            context.AddProposal(proposal);

            new AISelectionPhase().Execute(context);

            Assert.AreEqual(1, context.SelectedProposals.Count);
            Assert.AreSame(proposal, context.SelectedProposals[0]);
        }

        [Test]
        public void Select_FasterBusyShipyard_WaitsWithoutChangingExistingQueue()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal request
            );
            CapitalShip current = QueueSchedulingShip(context, strong, 4);
            context.AddProposal(request);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
            CollectionAssert.AreEqual(
                new[] { current },
                strong.GetManufacturingQueue()[ManufacturingType.Ship]
            );
            Assert.IsNull(
                Rebellion.Simulation.ManufacturingQueries.GetManufacturingOrder(
                    weak,
                    ManufacturingType.Ship
                )
            );
        }

        [Test]
        public void Select_SlowerBusyShipyard_StartsAtIdleProducer()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal request
            );
            QueueSchedulingShip(context, strong, 1000);
            context.AddProposal(request);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(weak, ((AIManufactureProposal)selected[0]).ProducerPlanet);
        }

        [Test]
        public void Select_TwoRequests_AllocatesDistinctLanesInScoreOrder()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal higher
            );
            AIManufactureProposal lower = new AIManufactureProposal(
                higher.Demand,
                new[] { strong, weak },
                new Technology(
                    AITestSceneBuilder.CreateCapitalShip("small-ship", context.Faction.InstanceID)
                ),
                true
            );
            lower.SetScore(50);
            context.AddProposal(lower);
            context.AddProposal(higher);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(2, selected.Count);
            Assert.AreSame(strong, ((AIManufactureProposal)selected[0]).ProducerPlanet);
            Assert.AreSame(weak, ((AIManufactureProposal)selected[1]).ProducerPlanet);
        }

        [Test]
        public void Select_BatchedUnitRequest_ExecutesOneUnit()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet _,
                out Planet strong,
                out AIManufactureProposal request
            );
            AIManufactureProposal batch = new AIManufactureProposal(
                request.Demand.WithQuantity(5),
                strong,
                request.Product,
                true
            );
            batch.SetScore(100);
            context.AddProposal(batch);

            AIManufactureProposal selected = (AIManufactureProposal)
                new AISelectionPhase().Select(context)[0];
            selected.Execute(context);

            Assert.AreEqual(1, strong.GetManufacturingQueue()[ManufacturingType.Ship].Count);
        }

        [Test]
        public void Select_LaneOccupiedBeforeExecution_PreservesNewWork()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet _,
                out Planet strong,
                out AIManufactureProposal request
            );
            context.AddProposal(request);
            AIManufactureProposal selected = (AIManufactureProposal)
                new AISelectionPhase().Select(context)[0];
            CapitalShip current = QueueSchedulingShip(context, strong, 10);

            selected.Execute(context);

            CollectionAssert.AreEqual(
                new[] { current },
                strong.GetManufacturingQueue()[ManufacturingType.Ship]
            );
        }

        [Test]
        public void Select_ChangedRequestOnNextTick_StartsNewProductAfterLaneDrains()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet _,
                out Planet strong,
                out AIManufactureProposal request
            );
            CapitalShip current = QueueSchedulingShip(context, strong, 4);
            context.AddProposal(request);
            Assert.IsEmpty(new AISelectionPhase().Select(context));
            strong.GetManufacturingQueue()[ManufacturingType.Ship].Clear();
            context.Game.DetachNode(current);
            AITurnContext next = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            CapitalShip replacement = AITestSceneBuilder.CreateCapitalShip(
                "replacement",
                context.Faction.InstanceID
            );
            AIManufactureProposal changed = new AIManufactureProposal(
                request.Demand,
                strong,
                new Technology(replacement),
                true
            );
            changed.SetScore(100);
            next.AddProposal(changed);

            AIManufactureProposal selected = (AIManufactureProposal)
                new AISelectionPhase().Select(next)[0];
            selected.Execute(next);

            Assert.AreEqual(
                "replacement",
                strong.GetManufacturingQueue()[ManufacturingType.Ship][0].GetTypeID()
            );
        }

        [Test]
        public void Select_TwoLargeRequests_StartsOneAndForecastsNextAtFastYard()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal higher
            );
            AIManufactureProposal lower = new AIManufactureProposal(
                higher.Demand,
                new[] { weak, strong },
                higher.Product,
                true
            );
            lower.SetScore(50);
            context.AddProposal(higher);
            context.AddProposal(lower);
            List<AIProposal> selected = new AISelectionPhase().Select(context);
            selected[0].Execute(context);
            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual(1, strong.GetManufacturingQueue()[ManufacturingType.Ship].Count);
            Assert.IsNull(
                Rebellion.Simulation.ManufacturingQueries.GetManufacturingOrder(
                    weak,
                    ManufacturingType.Ship
                )
            );
        }

        [Test]
        public void Select_FasterFutureShipyards_WaitsWithoutCreatingUnit()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal request
            );
            foreach (Building facility in strong.GetProductionFacilities(ManufacturingType.Ship))
            {
                facility.ManufacturingStatus = ManufacturingStatus.Delivering;
                facility.Movement = new MovementState { TransitTicks = 5 };
            }
            context.AddProposal(request);
            Assert.IsEmpty(new AISelectionPhase().Select(context));
            Assert.IsNull(
                Rebellion.Simulation.ManufacturingQueries.GetManufacturingOrder(
                    weak,
                    ManufacturingType.Ship
                )
            );
            Assert.IsNull(
                Rebellion.Simulation.ManufacturingQueries.GetManufacturingOrder(
                    strong,
                    ManufacturingType.Ship
                )
            );
        }

        [Test]
        public void Select_DistantStrongProducer_PrefersEarlierLocalDelivery()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal request
            );
            strong.PositionX = 1000;
            context.Game.Config.Movement.DistanceDivisor = 1;
            ((CapitalShip)request.Product.GetReference()).Hyperdrive = 100;
            context.AddProposal(request);
            List<AIProposal> selected = new AISelectionPhase().Select(context);
            Assert.AreSame(weak, ((AIManufactureProposal)selected[0]).ProducerPlanet);
        }

        [Test]
        public void Select_UnownedProductTemplate_StartsWithoutMutatingTemplate()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet _,
                out Planet strong,
                out AIManufactureProposal request
            );
            request.Product.GetReference().OwnerInstanceID = null;
            context.AddProposal(request);
            List<AIProposal> selected = new AISelectionPhase().Select(context);
            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(strong, ((AIManufactureProposal)selected[0]).ProducerPlanet);
            Assert.IsNull(request.Product.GetReference().GetOwnerInstanceID());
        }

        [Test]
        public void Select_FleetWithExpensiveHullAndHigherScoredFighter_AssignsFastYardToHull()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal hull
            );
            hull.SetScore(50);
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                context.Faction.InstanceID,
                starfighterCapacity: 10
            );
            context.Game.AttachNode(carrier, hull.Destination);
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                context.Faction.InstanceID
            );
            fighter.ConstructionCost = 5;
            AIManufactureProposal fighterRequest = new AIManufactureProposal(
                new AIProductionDemand(
                    "fighters",
                    AIProductionDemandKind.FleetStarfighter,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    hull.Destination,
                    1
                ),
                new[] { strong, weak },
                new Technology(fighter),
                true
            );
            fighterRequest.SetScore(100);
            context.AddProposal(fighterRequest);
            context.AddProposal(hull);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(2, selected.Count);
            AIManufactureProposal selectedHull = selected
                .OfType<AIManufactureProposal>()
                .Single(proposal => proposal.Product.GetReference() is CapitalShip);
            AIManufactureProposal selectedFighter = selected
                .OfType<AIManufactureProposal>()
                .Single(proposal => proposal.Product.GetReference() is Starfighter);
            Assert.AreSame(strong, selectedHull.ProducerPlanet);
            Assert.AreSame(weak, selectedFighter.ProducerPlanet);
            Assert.AreSame(hull.Product, selectedHull.Product);
            Assert.AreSame(fighterRequest.Product, selectedFighter.Product);
        }

        [Test]
        public void Select_LowerFleetRequestBetweenPriorityFleetRequests_SchedulesPriorityFleetTogether()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal first
            );
            CapitalShip small = AITestSceneBuilder.CreateCapitalShip(
                "small",
                context.Faction.InstanceID
            );
            small.ConstructionCost = 1;
            AIManufactureProposal sameFleet = new AIManufactureProposal(
                first.Demand,
                new[] { strong, weak },
                new Technology(small),
                true
            );
            sameFleet.SetScore(10);
            Fleet other = EntityFactory.CreateFleet("other", context.Faction.InstanceID);
            context.Game.AttachNode(other, weak);
            AIManufactureProposal otherFleet = new AIManufactureProposal(
                new AIProductionDemand(
                    "other-request",
                    AIProductionDemandKind.FleetCapitalShip,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    other,
                    1
                ),
                new[] { strong, weak },
                new Technology(small),
                true
            );
            otherFleet.SetScore(90);
            context.AddProposal(first);
            context.AddProposal(otherFleet);
            context.AddProposal(sameFleet);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(2, selected.Count);
            Assert.AreSame(first.Destination, ((AIManufactureProposal)selected[1]).Destination);
        }

        [Test]
        public void Select_UnbuiltCarrierWithFighterRequests_StartsOnlyTheCarrier()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal hull
            );
            ((CapitalShip)hull.Product.GetReference()).StarfighterCapacity = 2;
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                context.Faction.InstanceID
            );
            AIManufactureProposal request = new AIManufactureProposal(
                new AIProductionDemand(
                    "fighters",
                    AIProductionDemandKind.FleetStarfighter,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    hull.Destination,
                    1
                ),
                new[] { weak, strong },
                new Technology(fighter),
                true
            );
            request.SetScore(100);
            context.AddProposal(hull);
            context.AddProposal(request);
            List<AIProposal> selected = new AISelectionPhase().Select(context);
            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(hull.Product, ((AIManufactureProposal)selected[0]).Product);
            Assert.IsEmpty(((Fleet)hull.Destination).GetChildren<CapitalShip>());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Select_FleetNeedsTwoLongAndThreeShortProducts_ExchangesAssignmentsForEarlierCompletion(
            bool carried
        )
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal first
            );
            if (carried)
            {
                CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                    "carrier",
                    context.Faction.InstanceID,
                    starfighterCapacity: 5
                );
                context.Game.AttachNode(carrier, first.Destination);
                Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                    "long-fighter",
                    context.Faction.InstanceID
                );
                first = new AIManufactureProposal(
                    new AIProductionDemand(
                        "fighters",
                        AIProductionDemandKind.FleetStarfighter,
                        ManufacturingType.Ship,
                        BuildingType.None,
                        first.Destination,
                        1
                    ),
                    new[] { weak, strong },
                    new Technology(fighter),
                    true
                );
                first.SetScore(100);
            }
            context.Game.Config.AI.TickInterval = 1;
            context.Game.Config.Movement.SameSectorMinTransitTicks = 0;
            List<Building> yards = strong.GetProductionFacilities(ManufacturingType.Ship);
            for (int index = 1; index < yards.Count; index++)
                context.Game.DetachNode(yards[index]);
            first.Product.GetReference().ConstructionCost = 3;
            context.AddProposal(first);
            AIManufactureProposal second = new AIManufactureProposal(
                first.Demand,
                new[] { weak, strong },
                first.Product,
                true
            );
            second.SetScore(100);
            context.AddProposal(second);
            IManufacturable small = carried
                ? AITestSceneBuilder.CreateStarfighter("short-fighter", context.Faction.InstanceID)
                : AITestSceneBuilder.CreateCapitalShip("short", context.Faction.InstanceID);
            small.ConstructionCost = 2;
            Technology technology = new Technology(small);
            for (int index = 0; index < 3; index++)
            {
                AIManufactureProposal request = new AIManufactureProposal(
                    first.Demand,
                    new[] { weak, strong },
                    technology,
                    true
                );
                request.SetScore(100);
                context.AddProposal(request);
            }
            List<AIProposal> selected = new AISelectionPhase().Select(context);
            Assert.AreEqual(2, selected.Count);
            CollectionAssert.AreEquivalent(
                new[] { first.Product, technology },
                new[]
                {
                    ((AIManufactureProposal)selected[0]).Product,
                    ((AIManufactureProposal)selected[1]).Product,
                }
            );
        }

        [TestCase(5, 2, 2)]
        [TestCase(5, 1, 1)]
        [TestCase(30, 2, 0)]
        public void Select_ExistingCarrierAndEqualFleetCompletion_PrefersEarlierDeliveries(
            int fighterCost,
            int existingCapacity,
            int expectedFighters
        )
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal hull
            );
            context.Game.Config.AI.TickInterval = 1;
            context.Game.Config.Movement.SameSectorMinTransitTicks = 0;
            List<Building> yards = strong.GetProductionFacilities(ManufacturingType.Ship);
            for (int index = 1; index < yards.Count; index++)
                context.Game.DetachNode(yards[index]);
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "available-carrier",
                context.Faction.InstanceID,
                starfighterCapacity: existingCapacity
            );
            context.Game.AttachNode(carrier, hull.Destination);
            hull.Product.GetReference().ConstructionCost = 20;
            ((CapitalShip)hull.Product.GetReference()).StarfighterCapacity = 1;
            context.AddProposal(hull);
            AIManufactureProposal otherHull = new AIManufactureProposal(
                hull.Demand,
                new[] { weak, strong },
                hull.Product,
                true
            );
            otherHull.SetScore(100);
            context.AddProposal(otherHull);
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "early-fighter",
                context.Faction.InstanceID
            );
            fighter.ConstructionCost = fighterCost;
            for (int index = 0; index < 2; index++)
            {
                AIManufactureProposal request = new AIManufactureProposal(
                    new AIProductionDemand(
                        $"fighter-{index}",
                        AIProductionDemandKind.FleetStarfighter,
                        ManufacturingType.Ship,
                        BuildingType.None,
                        hull.Destination,
                        1
                    ),
                    new[] { weak, strong },
                    new Technology(fighter),
                    true
                );
                request.SetScore(100);
                context.AddProposal(request);
            }

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(2, selected.Count);
            Assert.AreEqual(
                expectedFighters,
                selected
                    .OfType<AIManufactureProposal>()
                    .Count(proposal => proposal.Product.GetReference() is Starfighter)
            );
        }

        [TestCase(5, true)]
        [TestCase(30, false)]
        public void Select_PriorityFleetWaitingForCarrier_UsesOnlyNonDelayingIdleGaps(
            int cost,
            bool starts
        )
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal lower
            );
            context.Game.Config.AI.TickInterval = 25;
            lower.Product.GetReference().ConstructionCost = cost;
            CapitalShip carrier = QueueSchedulingShip(context, strong, 5);
            carrier.StarfighterCapacity = 2;
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                context.Faction.InstanceID
            );
            AIManufactureProposal higher = new AIManufactureProposal(
                new AIProductionDemand(
                    "higher",
                    AIProductionDemandKind.FleetStarfighter,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    carrier.GetParentOfType<Fleet>(),
                    1
                ),
                new[] { weak },
                new Technology(fighter),
                true
            );
            higher.SetScore(200);
            context.AddProposal(higher);
            context.AddProposal(lower);
            List<AIProposal> selected = new AISelectionPhase().Select(context);
            Assert.AreEqual(starts ? 1 : 0, selected.Count);
            if (starts)
            {
                Assert.AreSame(lower.Product, ((AIManufactureProposal)selected[0]).Product);
                Assert.AreSame(weak, ((AIManufactureProposal)selected[0]).ProducerPlanet);
            }
        }

        [TestCase(5, true)]
        [TestCase(30, false)]
        public void Select_PriorityFleetWaitingForCarrier_PlanetaryProductionUsesOnlyNonDelayingIdleGaps(
            int cost,
            bool starts
        )
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out _
            );
            context.Game.Config.AI.TickInterval = 25;
            CapitalShip carrier = QueueSchedulingShip(context, strong, 5);
            carrier.StarfighterCapacity = 2;
            Starfighter fleetFighter = AITestSceneBuilder.CreateStarfighter(
                "fleet-fighter",
                context.Faction.InstanceID
            );
            AIManufactureProposal higher = new AIManufactureProposal(
                new AIProductionDemand(
                    "higher",
                    AIProductionDemandKind.FleetStarfighter,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    carrier.GetParentOfType<Fleet>(),
                    1
                ),
                weak,
                new Technology(fleetFighter),
                true
            );
            higher.SetScore(200);
            Starfighter planetFighter = AITestSceneBuilder.CreateStarfighter(
                "planet-fighter",
                context.Faction.InstanceID
            );
            planetFighter.ConstructionCost = cost;
            AIManufactureProposal lower = new AIManufactureProposal(
                new AIProductionDemand(
                    "lower",
                    AIProductionDemandKind.PlanetaryStarfighterReserve,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    weak,
                    1
                ),
                weak,
                new Technology(planetFighter),
                true
            );
            lower.SetScore(100);
            context.AddProposal(higher);
            context.AddProposal(lower);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(starts ? 1 : 0, selected.Count);
            if (starts)
                Assert.AreSame(lower.Product, ((AIManufactureProposal)selected[0]).Product);
        }

        /// <summary>Creates two producers and a single ranked capital-ship request.</summary>
        /// <param name="weak">The single-shipyard producer.</param>
        /// <param name="strong">The five-shipyard producer.</param>
        /// <param name="request">The scored fleet production request.</param>
        /// <returns>The faction's production turn context.</returns>
        private static AITurnContext CreateUnitSchedulingContext(
            out Planet weak,
            out Planet strong,
            out AIManufactureProposal request
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "scheduling-sector");
            weak = AITestSceneBuilder.AddPlanet(game, sector, "weak", empire.InstanceID);
            strong = AITestSceneBuilder.AddPlanet(game, sector, "strong", empire.InstanceID);
            AITestSceneBuilder.AddProductionFacility(
                game,
                weak,
                "weak-yard",
                BuildingType.Shipyard,
                ManufacturingType.Ship,
                processRate: 1
            );
            for (int index = 0; index < 5; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    strong,
                    $"strong-yard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship,
                    processRate: 1
                );
            Fleet fleet = EntityFactory.CreateFleet("scheduling-fleet", empire.InstanceID);
            game.AttachNode(fleet, weak);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "requested-ship",
                empire.InstanceID
            );
            ship.ConstructionCost = 100;
            AIProductionDemand demand = new AIProductionDemand(
                "scheduling-request",
                AIProductionDemandKind.FleetCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                fleet,
                1
            );
            request = new AIManufactureProposal(
                demand,
                new[] { weak, strong },
                new Technology(ship),
                true
            );
            request.SetScore(100);
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>Commits a capital ship to a producer's current manufacturing queue.</summary>
        /// <param name="context">The faction's turn context.</param>
        /// <param name="producer">The planet manufacturing the ship.</param>
        /// <param name="cost">The ship's construction cost.</param>
        /// <returns>The queued ship.</returns>
        private static CapitalShip QueueSchedulingShip(
            AITurnContext context,
            Planet producer,
            int cost
        )
        {
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "current-ship",
                context.Faction.InstanceID
            );
            ship.InstanceID = "current-ship";
            ship.ConstructionCost = cost;
            ship.ManufacturingStatus = ManufacturingStatus.Building;
            ship.ProducerPlanetID = producer.InstanceID;
            Fleet fleet = EntityFactory.CreateFleet("current-fleet", context.Faction.InstanceID);
            context.Game.AttachNode(fleet, producer);
            context.Game.AttachNode(ship, fleet);
            producer.AddToManufacturingQueue(ship);
            return ship;
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
        /// Creates one scored opportunistic planetary-fighter proposal.
        /// </summary>
        /// <param name="planet">Planet producing and receiving the fighter.</param>
        /// <param name="fighter">Fighter technology to manufacture.</param>
        /// <param name="score">Proposal score.</param>
        /// <returns>The configured proposal.</returns>
        private static AIManufactureProposal CreateIdleShipyardFighterProposal(
            Planet planet,
            Starfighter fighter,
            double score
        ) => CreatePlanetaryStarfighterProposal(planet, fighter, score, true);

        /// <summary>
        /// Creates one scored planetary-fighter proposal.
        /// </summary>
        /// <param name="planet">Planet producing and receiving the fighter.</param>
        /// <param name="fighter">Fighter technology to manufacture.</param>
        /// <param name="score">Proposal score.</param>
        /// <param name="usesIdleShipyardCapacity">Whether the proposal uses idle capacity.</param>
        /// <returns>The configured proposal.</returns>
        private static AIManufactureProposal CreatePlanetaryStarfighterProposal(
            Planet planet,
            Starfighter fighter,
            double score,
            bool usesIdleShipyardCapacity
        )
        {
            AIProductionDemand demand = new AIProductionDemand(
                $"idle-{planet.InstanceID}",
                AIProductionDemandKind.PlanetaryStarfighterReserve,
                ManufacturingType.Ship,
                BuildingType.None,
                planet,
                1,
                targetCount: 1,
                baseDemandPercent: 1,
                usesIdleShipyardCapacity: usesIdleShipyardCapacity
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(fighter),
                true
            );
            proposal.SetScore(score);
            return proposal;
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
        /// <param name="maintenanceCost">The manufactured building's maintenance cost.</param>
        /// <returns>The scored manufacturing proposal.</returns>
        private static AIManufactureProposal CreateBuildingProposal(
            Planet destination,
            Planet producer,
            AIProductionDemandKind kind,
            BuildingType buildingType,
            double score,
            int maintenanceCost = 0
        )
        {
            Building building = AITestSceneBuilder.CreateBuildingTemplate(
                $"{buildingType}-template",
                buildingType,
                ManufacturingType.None
            );
            building.MaintenanceCost = maintenanceCost;
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    $"{kind}-demand",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    destination,
                    1,
                    baseDemandPercent: 100
                ),
                producer,
                new Technology(building),
                false
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
        /// <param name="isEmergencyDefense">Whether the defense is essential rather than spare-capacity filler.</param>
        /// <param name="maintenanceCost">The manufactured product's maintenance cost.</param>
        private static AIManufactureProposal CreateManufactureProposal(
            AITurnContext context,
            Planet producer,
            AIProductionDemandKind kind,
            BuildingType buildingType,
            int constructionCost = 10,
            double score = 100,
            bool isEmergencyDefense = false,
            int maintenanceCost = 0
        )
        {
            IManufacturable product;
            ManufacturingType manufacturingType;
            Rebellion.SceneGraph.ContainerNode destination = producer;
            switch (kind)
            {
                case AIProductionDemandKind.FleetCapitalShip:
                case AIProductionDemandKind.FleetSeedCapitalShip:
                case AIProductionDemandKind.ColonizationFleetSeedCapitalShip:
                    product = AITestSceneBuilder.CreateCapitalShip(
                        $"reserve-{kind}",
                        context.Faction.InstanceID
                    );
                    manufacturingType = ManufacturingType.Ship;
                    break;
                case AIProductionDemandKind.FleetStarfighter:
                    product = AITestSceneBuilder.CreateStarfighter(
                        $"reserve-{kind}",
                        context.Faction.InstanceID
                    );
                    manufacturingType = ManufacturingType.Ship;
                    break;
                case AIProductionDemandKind.FleetRegiment:
                case AIProductionDemandKind.GarrisonRegimentReserve:
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
            product.MaintenanceCost = maintenanceCost;

            if (
                kind
                is AIProductionDemandKind.FleetCapitalShip
                    or AIProductionDemandKind.FleetStarfighter
                    or AIProductionDemandKind.FleetRegiment
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
            AIProductionDemand demand = new AIProductionDemand(
                $"reserve-{kind}",
                kind,
                manufacturingType,
                buildingType,
                destination,
                1,
                baseDemandPercent: 100,
                isEmergencyDefense: isEmergencyDefense
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(product),
                false
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
            AIProductionDemand demand = new AIProductionDemand(
                $"shipyard-{planet.InstanceID}",
                AIProductionDemandKind.Shipyard,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                planet,
                quantity,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shipyard),
                false
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
            return new AITurnContext(
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
        }
    }
}
