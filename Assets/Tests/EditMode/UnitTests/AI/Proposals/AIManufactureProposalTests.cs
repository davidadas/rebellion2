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
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;
using OfficerRating = Rebellion.Game.Units.SkillRating;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIManufactureProposalTests
    {
        [Test]
        public void UsesDefensiveReserve_WithTrainingFacilityDemand_ReturnsTrue()
        {
            AIProductionDemand demand = new AIProductionDemand(
                "training-facility-demand",
                AIProductionDemandKind.TrainingFacility,
                ManufacturingType.Building,
                BuildingType.TrainingFacility,
                null,
                1,
                baseDemandPercent: 100
            );

            Assert.IsTrue(demand.UsesDefensiveReserve);
        }

        [Test]
        public void Execute_WithValidBuildingProposal_QueuesManufacturing()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "resource-world",
                empire.InstanceID,
                rawResourceNodes: 4
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building mine = AITestSceneBuilder.CreateBuildingTemplate(
                "mine-template",
                BuildingType.Mine
            );
            mine.MaintenanceCost = 0;
            AIManufactureProposal proposal = new AIManufactureProposal(
                CreateBuildingDemand(planet),
                planet,
                new Technology(mine),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Building].Count);
        }

        [Test]
        public void Execute_WithMaintenanceRecoveryAtZeroHeadroom_QueuesOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "resource-world",
                empire.InstanceID,
                rawResourceNodes: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building mine = AITestSceneBuilder.CreateBuildingTemplate(
                "mine-template",
                BuildingType.Mine
            );
            mine.MaintenanceCost = 10;
            AIManufactureProposal proposal = new AIManufactureProposal(
                CreateBuildingDemand(planet),
                planet,
                new Technology(mine),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            Assert.AreEqual(0, context.Faction.ProjectedMaintenanceHeadroom);

            proposal.Execute(context);

            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Building].Count);
            Assert.AreEqual(-10, context.AvailableProjectedMaintenanceHeadroom);
        }

        [TestCase(AIProductionDemandKind.Shipyard, 100, true)]
        [TestCase(AIProductionDemandKind.Shipyard, 101, false)]
        [TestCase(AIProductionDemandKind.PlanetaryDefense, 100, true)]
        [TestCase(AIProductionDemandKind.PlanetaryDefense, 101, false)]
        public void Execute_HeadroomBelowEconomyTarget_RespectsManufacturingReserve(
            AIProductionDemandKind kind,
            int maintenanceCost,
            bool expectedQueued
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 500;
            game.Config.AI.Selection.MaintenanceHeadroomTarget = 1500;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "producer",
                faction.InstanceID,
                energyCapacity: 40,
                rawResourceNodes: 12
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "constructor",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            for (int index = 0; index < 12; index++)
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
            BuildingType buildingType =
                kind == AIProductionDemandKind.PlanetaryDefense
                    ? BuildingType.Defense
                    : BuildingType.Shipyard;
            Building building = AITestSceneBuilder.CreateBuildingTemplate(
                "building",
                buildingType,
                buildingType == BuildingType.Shipyard
                    ? ManufacturingType.Ship
                    : ManufacturingType.None
            );
            building.MaintenanceCost = maintenanceCost;
            if (buildingType == BuildingType.Defense)
                building.ShieldStrength = 80;
            AIManufactureProposal proposal = new(
                new AIProductionDemand(
                    "production",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    producer,
                    1
                ),
                producer,
                new Technology(building),
                false
            );

            proposal.Execute(AITestSceneBuilder.CreateContext(game, faction));

            Assert.AreEqual(
                expectedQueued,
                producer
                    .GetManufacturingQueue()
                    .TryGetValue(ManufacturingType.Building, out var queue)
                    && queue.Count == 1
            );
        }

        [Test]
        public void Execute_WithMultiUnitFacilityDemand_QueuesOneBuilding()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID,
                energyCapacity: 12,
                rawResourceNodes: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            for (int index = 0; index < 2; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard-template",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.MaintenanceCost = 1;
            AIProductionDemand demand = new AIProductionDemand(
                "shipyard-demand",
                AIProductionDemandKind.Shipyard,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                planet,
                3,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shipyard),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            List<IManufacturable> queue = planet.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(1, proposal.GetMaintenanceCost());
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, queue.OfType<Building>().Count());
            Assert.IsTrue(
                queue.All(item => item.GetManufacturingStatus() == ManufacturingStatus.Building)
            );
            Assert.AreEqual(1, planet.GetTotalBuildingTypeCount(BuildingType.Shipyard));
        }

        /// <summary>
        /// Verifies that sequential orders share one turn maintenance budget.
        /// </summary>
        [Test]
        public void Execute_SequentialOrders_ConsumeTurnMaintenanceHeadroom()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID,
                rawResourceNodes: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "mine",
                BuildingType.Mine,
                ManufacturingType.None
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "refinery",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID,
                maintenanceCost: 30
            );
            AIProductionDemand demand = new AIProductionDemand(
                "fighter-demand",
                AIProductionDemandKind.PlanetaryStarfighterReserve,
                ManufacturingType.Ship,
                BuildingType.None,
                planet,
                1,
                baseDemandPercent: 100
            );
            AIManufactureProposal first = new AIManufactureProposal(
                demand,
                planet,
                new Technology(fighter),
                distributesDemand: true
            );
            AIManufactureProposal second = new AIManufactureProposal(
                demand,
                planet,
                new Technology(fighter),
                distributesDemand: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(50, context.AvailableProjectedMaintenanceHeadroom);

            first.Execute(context);
            second.Execute(context);

            Assert.AreEqual(20, context.AvailableProjectedMaintenanceHeadroom);
            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Ship].Count);
        }

        /// <summary>
        /// Verifies that a recovery order still debits maintenance before an ordinary order.
        /// </summary>
        [Test]
        public void Execute_RecoveryOrderBeforeOrdinaryOrder_DebitsSharedMaintenanceBudget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "production-world",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "mine",
                BuildingType.Mine,
                ManufacturingType.None
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "refinery",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Building mine = AITestSceneBuilder.CreateBuildingTemplate(
                "mine-template",
                BuildingType.Mine
            );
            mine.MaintenanceCost = 10;
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID,
                maintenanceCost: 45
            );
            AIManufactureProposal recovery = new AIManufactureProposal(
                CreateBuildingDemand(planet),
                planet,
                new Technology(mine),
                false
            );
            AIProductionDemand fighterDemand = new AIProductionDemand(
                "fighter-demand",
                AIProductionDemandKind.PlanetaryStarfighterReserve,
                ManufacturingType.Ship,
                BuildingType.None,
                planet,
                1,
                baseDemandPercent: 100
            );
            AIManufactureProposal ordinary = new AIManufactureProposal(
                fighterDemand,
                planet,
                new Technology(fighter),
                distributesDemand: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            Assert.AreEqual(50, context.AvailableProjectedMaintenanceHeadroom);
            Assert.AreEqual(10, recovery.GetMaintenanceCost());

            recovery.Execute(context);
            Assert.AreEqual(
                10,
                planet.GetManufacturingQueue()[ManufacturingType.Building][0].GetMaintenanceCost()
            );
            Assert.AreEqual(40, context.AvailableProjectedMaintenanceHeadroom);
            ordinary.Execute(context);

            Assert.AreEqual(40, context.AvailableProjectedMaintenanceHeadroom);
            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Building].Count);
            Assert.IsFalse(planet.GetManufacturingQueue().ContainsKey(ManufacturingType.Ship));
        }

        [Test]
        public void Execute_WithFacilityUpgrade_ReplacesExactlyOneFacility()
        {
            (
                GameRoot _,
                Faction _,
                Planet planet,
                Building replacement,
                Building remaining,
                AIManufactureProposal proposal,
                AITurnContext context
            ) = CreateFacilityUpgradeProposalScene(2, 0, 0);

            proposal.Execute(context);

            List<Building> queued = planet
                .GetManufacturingQueue()[ManufacturingType.Building]
                .OfType<Building>()
                .ToList();
            Assert.IsNull(replacement.GetParent());
            Assert.AreSame(planet, remaining.GetParent());
            Assert.AreEqual(1, queued.Count);
            Assert.AreEqual("advanced-shipyard", queued[0].GetDisplayName());
            Assert.AreEqual(ManufacturingStatus.Building, queued[0].ManufacturingStatus);
            Assert.AreEqual(2, planet.GetTotalBuildingTypeCount(BuildingType.Shipyard));
        }

        /// <summary>
        /// Verifies that a maintenance-neutral upgrade remains valid below the configured reserve.
        /// </summary>
        [Test]
        public void Execute_WithMaintenanceNeutralUpgrade_BelowReserve_QueuesUpgrade()
        {
            (
                GameRoot _,
                Faction _,
                Planet planet,
                Building replacement,
                Building _,
                AIManufactureProposal proposal,
                AITurnContext context
            ) = CreateFacilityUpgradeProposalScene(2, 10, 10);
            Assert.Less(
                context.AvailableProjectedMaintenanceHeadroom,
                proposal.GetMinimumMaintenanceHeadroom(context)
            );

            proposal.Execute(context);

            Assert.IsNull(replacement.GetParent());
            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Building].Count);
        }

        [Test]
        public void Execute_WithOnlyOneFacility_DoesNotRemoveIt()
        {
            (
                GameRoot _,
                Faction _,
                Planet planet,
                Building replacement,
                Building _,
                AIManufactureProposal proposal,
                AITurnContext context
            ) = CreateFacilityUpgradeProposalScene(1, 0, 0);

            proposal.Execute(context);

            Assert.AreSame(planet, replacement.GetParent());
            Assert.AreEqual(1, planet.GetTotalBuildingTypeCount(BuildingType.Shipyard));
            Assert.IsFalse(planet.GetManufacturingQueue().ContainsKey(ManufacturingType.Building));
        }

        [Test]
        public void CanExecute_WithFacilityUpgradeOnOverCapacityPlanet_ReturnsFalse()
        {
            (
                GameRoot _,
                Faction _,
                Planet planet,
                Building _,
                Building _,
                AIManufactureProposal proposal,
                AITurnContext context
            ) = CreateFacilityUpgradeProposalScene(2, 0, 0);
            planet.EnergyCapacity = planet.GetEnergyUsed() - 1;

            bool canExecute = proposal.CanExecute(context);

            Assert.IsFalse(canExecute);
        }

        [Test]
        public void GetMaintenanceCost_WithFacilityUpgrade_ReturnsNetIncrease()
        {
            (
                GameRoot _,
                Faction _,
                Planet _,
                Building _,
                Building _,
                AIManufactureProposal proposal,
                AITurnContext _
            ) = CreateFacilityUpgradeProposalScene(2, 10, 17);

            Assert.AreEqual(7, proposal.GetMaintenanceCost());
        }

        [Test]
        public void Execute_WithEmergencyShieldDemand_QueuesOneShield()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defense-world",
                empire.InstanceID,
                energyCapacity: 3
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield-template",
                BuildingType.Defense
            );
            shield.MaintenanceCost = 0;
            AIProductionDemand demand = new AIProductionDemand(
                "planetary-shield-demand",
                AIProductionDemandKind.PlanetaryDefense,
                ManufacturingType.Building,
                BuildingType.Defense,
                planet,
                2,
                baseDemandPercent: 100,
                isEmergencyDefense: true
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shield),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            List<IManufacturable> queue = planet.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, planet.GetTotalBuildingTypeCount(BuildingType.Defense));
        }

        [Test]
        public void Execute_WithMultiUnitGarrisonDemand_QueuesOneRegiment()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "garrison-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            Regiment regiment = AITestSceneBuilder.CreateRegiment(
                "regiment-template",
                empire.InstanceID
            );
            regiment.MaintenanceCost = 0;
            AIProductionDemand demand = new AIProductionDemand(
                "garrison-demand",
                AIProductionDemandKind.GarrisonRegimentReserve,
                ManufacturingType.Troop,
                BuildingType.None,
                planet,
                6,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(regiment),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            List<IManufacturable> queue = planet.GetManufacturingQueue()[ManufacturingType.Troop];
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, planet.GetAllRegiments().Count);
        }

        [Test]
        public void Execute_WithFullDestination_DoesNotReplaceExistingFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID,
                energyCapacity: 1
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Building replaceableShipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                headquarters,
                "existing-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "producer",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense
            );
            shield.MaintenanceCost = 0;
            AIProductionDemand demand = new AIProductionDemand(
                "headquarters-defense",
                AIProductionDemandKind.PlanetaryDefense,
                ManufacturingType.Building,
                BuildingType.Defense,
                headquarters,
                1,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(shield),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            Assert.AreSame(headquarters, replaceableShipyard.GetParent());
            Assert.AreEqual(0, headquarters.GetTotalBuildingTypeCount(BuildingType.Defense));
            Assert.IsFalse(
                producer.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
        }

        [Test]
        public void Execute_WithSpecialForcesProposal_QueuesRequestedUnitAtPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID
            );
            template.SetBaseRating(OfficerRating.Combat, 70);
            AIProductionDemand demand = new AIProductionDemand(
                "special-forces-demand",
                AIProductionDemandKind.SpecialForces,
                ManufacturingType.Troop,
                BuildingType.None,
                planet,
                1,
                template.GetTypeID()
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            SpecialForces queued = planet
                .GetManufacturingQueue()[ManufacturingType.Troop]
                .OfType<SpecialForces>()
                .Single();
            Assert.AreEqual("commandos", queued.GetTypeID());
            Assert.AreSame(planet, queued.GetParent());
            Assert.AreEqual(70, queued.GetBaseRating(OfficerRating.Combat));
        }

        [Test]
        public void Execute_WithDistributedStarfighterDemand_QueuesOneSquadron()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard-building",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                starfighterCapacity: 3
            );
            game.AttachNode(fleet, planet);
            game.AttachNode(carrier, fleet);
            Starfighter template = new Starfighter
            {
                InstanceID = "starfighter-template",
                TypeID = "starfighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingFactionInstanceIDs = new List<string> { empire.InstanceID },
                ConstructionCost = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            AIProductionDemand demand = new AIProductionDemand(
                "starfighter-demand",
                AIProductionDemandKind.FleetStarfighter,
                ManufacturingType.Ship,
                BuildingType.None,
                fleet,
                3,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template),
                distributesDemand: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            List<IManufacturable> queue = planet.GetManufacturingQueue()[ManufacturingType.Ship];
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, queue.OfType<Starfighter>().Count());
            Assert.AreEqual(1, fleet.GetCurrentStarfighterCount());
        }

        [Test]
        public void Execute_WithPlanetaryStarfighterDemand_QueuesOneSquadronAtPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard-building",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Starfighter template = AITestSceneBuilder.CreateStarfighter(
                "planetary-fighter",
                empire.InstanceID
            );
            AIProductionDemand demand = new AIProductionDemand(
                "planetary-starfighter-demand",
                AIProductionDemandKind.PlanetaryStarfighterReserve,
                ManufacturingType.Ship,
                BuildingType.None,
                planet,
                3,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template),
                distributesDemand: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            List<IManufacturable> queue = planet.GetManufacturingQueue()[ManufacturingType.Ship];
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, queue.OfType<Starfighter>().Count());
            Assert.AreEqual(1, planet.GetStarfighterCount());
            Assert.IsTrue(planet.GetAllStarfighters().All(item => item.GetParent() == planet));
        }

        [Test]
        public void CanSelect_WithPlanetaryStarfighterForBlockadedPlanet_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "blockaded-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "shipyard-building",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet blockadingFleet = EntityFactory.CreateFleet(
                "blockading-fleet",
                rebels.InstanceID
            );
            game.AttachNode(blockadingFleet, destination);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("blockading-ship", rebels.InstanceID),
                blockadingFleet
            );
            Starfighter template = AITestSceneBuilder.CreateStarfighter(
                "planetary-fighter",
                empire.InstanceID
            );
            AIProductionDemand demand = new AIProductionDemand(
                "planetary-starfighter-demand",
                AIProductionDemandKind.PlanetaryStarfighterReserve,
                ManufacturingType.Ship,
                BuildingType.None,
                destination,
                1,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(template),
                distributesDemand: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.IsFalse(proposal.CanSelect(context));

            proposal.Execute(context);

            Assert.IsFalse(producer.GetManufacturingQueue().ContainsKey(ManufacturingType.Ship));
        }

        [Test]
        public void CanExecute_WithDistributedBatchForMovingFleet_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(fleet, planet);
            CapitalShip template = AITestSceneBuilder.CreateCapitalShip(
                "capital-template",
                empire.InstanceID
            );
            AIProductionDemand demand = new AIProductionDemand(
                "capital-demand",
                AIProductionDemandKind.FleetCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                fleet,
                1,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template),
                distributesDemand: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            bool canExecute = proposal.CanExecute(context);

            Assert.IsFalse(canExecute);
        }

        [Test]
        public void Execute_WithFleetSeedDemandBeyondMinimum_CreatesBattleFleetAndQueuesCapitalShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            empire.HQInstanceID = planet.InstanceID;
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            CapitalShip template = AITestSceneBuilder.CreateCapitalShip(
                "corvette-template",
                empire.InstanceID
            );
            template.TypeID = "corvette";
            template.ManufacturingFactionInstanceIDs.Add(empire.InstanceID);
            for (
                int index = 0;
                index < game.Config.AI.FleetDeployment.MinimumBattleFleetCount;
                index++
            )
            {
                Fleet existingFleet = empire.CreateFleet(roleType: FleetRoleType.Battle);
                game.AttachNode(existingFleet, planet);
                game.AttachNode(
                    AITestSceneBuilder.CreateCapitalShip(
                        $"existing-capital-{index}",
                        empire.InstanceID
                    ),
                    existingFleet
                );
            }

            AIProductionDemand demand = new AIProductionDemand(
                "fleet-seed-demand",
                AIProductionDemandKind.FleetSeedCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                planet,
                1,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            IReadOnlyList<Fleet> fleets = game.GetSceneNodesByOwnerInstanceID<Fleet>(
                empire.InstanceID
            );
            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumBattleFleetCount + 1,
                fleets.Count
            );
            Fleet fleet = fleets.Single(candidate =>
                candidate
                    .GetChildren<CapitalShip>()
                    .Any(ship => ship.ManufacturingStatus == ManufacturingStatus.Building)
            );
            Assert.AreEqual(FleetRoleType.Battle, fleet.RoleType);
            Assert.AreSame(planet, fleet.GetParent());
            Assert.AreEqual(1, fleet.GetChildren<CapitalShip>().Count);
            Assert.AreEqual(
                ManufacturingStatus.Building,
                fleet.GetChildren<CapitalShip>()[0].ManufacturingStatus
            );
            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Ship].Count);
        }

        [Test]
        public void Execute_WithColonizationFleetSeedDemand_CreatesColonizationFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard-building",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            CapitalShip template = AITestSceneBuilder.CreateCapitalShip(
                "transport-template",
                empire.InstanceID,
                regimentCapacity: 2
            );
            template.TypeID = "transport";
            template.ManufacturingFactionInstanceIDs.Add(empire.InstanceID);
            AIProductionDemand demand = new AIProductionDemand(
                "colonization-fleet-seed-demand",
                AIProductionDemandKind.ColonizationFleetSeedCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                planet,
                1,
                baseDemandPercent: 100,
                capitalShipRole: AICapitalShipProductionRole.TroopTransport
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            Fleet fleet = game.GetSceneNodesByOwnerInstanceID<Fleet>(empire.InstanceID).Single();
            Assert.AreEqual(FleetRoleType.Colonization, fleet.RoleType);
            Assert.AreSame(planet, fleet.GetParent());
            Assert.AreEqual(1, fleet.GetChildren<CapitalShip>().Count);
            Assert.AreEqual(
                ManufacturingStatus.Building,
                fleet.GetChildren<CapitalShip>()[0].ManufacturingStatus
            );
        }

        [TestCase(MissionTypeIDs.Sabotage)]
        [TestCase(MissionTypeIDs.Espionage)]
        public void Execute_WithIdleSpecialForcesDemand_QueuesOneInterruptibleUnit(
            string missionTypeId
        )
        {
            var (game, faction, planet, template) = CreateSaboteurTrainingScene();
            template.AllowedMissionTypeIDs = new List<string> { missionTypeId };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal proposal = new AIManufactureProposal(
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

            proposal.Execute(context);

            SpecialForces unit = planet
                .GetManufacturingQueue()[ManufacturingType.Troop]
                .Cast<SpecialForces>()
                .Single();
            Assert.AreSame(
                unit,
                context.Assessment.GetReplaceableManufacturingOrder(planet, ManufacturingType.Troop)
            );
        }

        [Test]
        public void Execute_ContinuingExistingProduction_PreservesProgress()
        {
            var (game, faction, planet, template) = CreateSaboteurTrainingScene();
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            SpecialForces existing = (SpecialForces)template.CreateCopy();
            Assert.IsTrue(context.Manufacturing.Enqueue(planet, existing, planet));
            existing.ManufacturingProgress = 1;
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "demand",
                    AIProductionDemandKind.SpecialForces,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    planet,
                    1,
                    template.TypeID,
                    existingOrder: existing
                ),
                planet,
                new Technology(template),
                false
            );

            proposal.Execute(context);

            IManufacturable queued = planet
                .GetManufacturingQueue()[ManufacturingType.Troop]
                .First();
            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Troop].Count);
            Assert.AreSame(existing, queued);
            Assert.AreEqual(1, queued.ManufacturingProgress);
        }

        [Test]
        public void Execute_SelectedReplacementWithFullDestination_ReplacesUnfinishedDefense()
        {
            var (game, faction, producer, destination, shield) = CreateDefenseConstructionScene();
            destination.EnergyCapacity = 2;
            Building requiredShield = AITestSceneBuilder.AddProductionFacility(
                game,
                destination,
                "required-shield",
                BuildingType.Defense,
                ManufacturingType.None
            );
            requiredShield.ShieldStrength = 80;
            Building facility = AITestSceneBuilder.CreateBuildingTemplate(
                "training",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "required",
                    AIProductionDemandKind.TrainingFacility,
                    ManufacturingType.Building,
                    BuildingType.TrainingFacility,
                    destination,
                    1
                ),
                producer,
                new Technology(facility),
                false,
                replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                    producer,
                    ManufacturingType.Building
                )
            );

            Assert.IsTrue(proposal.CanSelect(context));
            proposal.Execute(context);

            IManufacturable queued = producer
                .GetManufacturingQueue()[ManufacturingType.Building]
                .Single();
            Assert.AreEqual(facility.TypeID, queued.GetTypeID());
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Execute_ProducerQueueChanges_PreservesUnselectedOrder(
            bool initiallyEmpty,
            bool matchingType
        )
        {
            var (game, faction, producer, destination, original) = CreateDefenseConstructionScene();
            destination.EnergyCapacity = 2;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            if (initiallyEmpty)
                Assert.IsTrue(
                    context.Manufacturing.CancelManufacturing(original, faction.InstanceID)
                );
            Building requested = AITestSceneBuilder.CreateBuildingTemplate(
                "training",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "required",
                    AIProductionDemandKind.TrainingFacility,
                    ManufacturingType.Building,
                    BuildingType.TrainingFacility,
                    destination,
                    1
                ),
                producer,
                new Technology(requested),
                false,
                replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                    producer,
                    ManufacturingType.Building
                )
            );
            Assert.IsTrue(proposal.CanSelect(context));
            if (!initiallyEmpty)
                Assert.IsTrue(
                    context.Manufacturing.CancelManufacturing(original, faction.InstanceID)
                );
            Building unexpected = matchingType
                ? (Building)original.CreateCopy()
                : AITestSceneBuilder.CreateBuildingTemplate("battery", BuildingType.Weapon);
            unexpected.OwnerInstanceID = faction.InstanceID;
            unexpected.ConstructionCost = 10;
            Assert.IsTrue(context.Manufacturing.Enqueue(producer, unexpected, destination));
            unexpected.ManufacturingProgress = 2;

            proposal.Execute(context);

            Assert.AreSame(
                unexpected,
                producer.GetManufacturingQueue()[ManufacturingType.Building].Single()
            );
        }

        [Test]
        public void Execute_MatchingEmergencyDefense_PreservesProgressAndProtectsOrder()
        {
            var (game, faction, producer, destination, shield) = CreateDefenseConstructionScene();
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "urgent",
                    AIProductionDemandKind.PlanetaryDefense,
                    ManufacturingType.Building,
                    BuildingType.Defense,
                    destination,
                    1,
                    isEmergencyDefense: true
                ),
                producer,
                new Technology(shield),
                false,
                replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                    producer,
                    ManufacturingType.Building
                )
            );

            proposal.Execute(context);

            Assert.AreSame(
                shield,
                producer.GetManufacturingQueue()[ManufacturingType.Building].Single()
            );
            Assert.AreEqual(3, shield.ManufacturingProgress);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Execute_DefenseDemand_QueuesOneShieldRegardlessOfUrgency(bool emergency)
        {
            var (game, faction, producer, destination, shield) = CreateDefenseConstructionScene();
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            Assert.IsTrue(context.Manufacturing.CancelManufacturing(shield, faction.InstanceID));
            destination.EnergyCapacity = 3;
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "defense",
                    AIProductionDemandKind.PlanetaryDefense,
                    ManufacturingType.Building,
                    BuildingType.Defense,
                    destination,
                    2,
                    isEmergencyDefense: emergency
                ),
                producer,
                new Technology(shield),
                false,
                replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                    producer,
                    ManufacturingType.Building
                )
            );

            proposal.Execute(context);

            List<IManufacturable> queue = producer.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(1, queue.Count);
            Assert.IsTrue(queue.All(item => item.ManufacturingProgress == 0));
        }

        [Test]
        public void CanSelect_RoutineDefenseWithExistingOrder_LeavesProductionUnchanged()
        {
            var (game, faction, producer, destination, shield) = CreateDefenseConstructionScene();
            destination.EnergyCapacity = 3;
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "second-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "filler",
                    AIProductionDemandKind.PlanetaryDefense,
                    ManufacturingType.Building,
                    BuildingType.Defense,
                    destination,
                    1
                ),
                producer,
                new Technology(shield),
                false,
                replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                    producer,
                    ManufacturingType.Building
                )
            );

            Assert.IsFalse(proposal.CanSelect(AITestSceneBuilder.CreateContext(game, faction)));
            Assert.AreSame(
                shield,
                producer.GetManufacturingQueue()[ManufacturingType.Building].Single()
            );
            Assert.AreEqual(3, shield.ManufacturingProgress);
        }

        [Test]
        public void CanSelect_CompletedDefense_DoesNotOfferReplacement()
        {
            var (game, faction, producer, destination, shield) = CreateDefenseConstructionScene();
            shield.ManufacturingStatus = ManufacturingStatus.Complete;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    "required",
                    AIProductionDemandKind.TrainingFacility,
                    ManufacturingType.Building,
                    BuildingType.TrainingFacility,
                    destination,
                    1
                ),
                producer,
                new Technology(
                    AITestSceneBuilder.CreateBuildingTemplate(
                        "training",
                        BuildingType.TrainingFacility,
                        ManufacturingType.Troop
                    )
                ),
                false,
                replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                    producer,
                    ManufacturingType.Building
                )
            );

            Assert.IsFalse(proposal.CanSelect(AITestSceneBuilder.CreateContext(game, faction)));
            Assert.AreSame(shield, destination.GetChildren<Building>().Single());
        }

        /// <summary>
        /// Creates a producer occupied by an unfinished planetary defense order.
        /// </summary>
        /// <returns>The game, faction, producer, destination, and unfinished defense.</returns>
        private static (
            GameRoot,
            Faction,
            Planet,
            Planet,
            Building
        ) CreateDefenseConstructionScene()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "producer",
                faction.InstanceID
            );
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                faction.InstanceID,
                energyCapacity: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense
            );
            shield.ShieldStrength = 80;
            shield.OwnerInstanceID = faction.InstanceID;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            Assert.IsTrue(context.Manufacturing.Enqueue(producer, shield, destination));
            shield.ManufacturingProgress = 3;
            return (game, faction, producer, destination, shield);
        }

        /// <summary>
        /// Creates an owned training lane and a manufactureable saboteur template.
        /// </summary>
        /// <returns>The game, owning faction, production planet, and saboteur template.</returns>
        private static (GameRoot, Faction, Planet, SpecialForces) CreateSaboteurTrainingScene()
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
            return (game, faction, planet, template);
        }

        /// <summary>
        /// Creates building demand.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <returns>The created building demand.</returns>
        private static AIProductionDemand CreateBuildingDemand(Planet destination)
        {
            return new AIProductionDemand(
                "mine-demand",
                AIProductionDemandKind.Mine,
                ManufacturingType.Building,
                BuildingType.Mine,
                destination,
                1,
                baseDemandPercent: 100
            );
        }

        /// <summary>
        /// Creates facility upgrade proposal scene.
        /// </summary>
        /// <param name="shipyardCount">The shipyard count.</param>
        /// <param name="existingMaintenance">The existing maintenance.</param>
        /// <param name="upgradeMaintenance">The upgrade maintenance.</param>
        /// <returns>The created facility upgrade proposal scene.</returns>
        private static (
            GameRoot game,
            Faction faction,
            Planet planet,
            Building replacement,
            Building remaining,
            AIManufactureProposal proposal,
            AITurnContext context
        ) CreateFacilityUpgradeProposalScene(
            int shipyardCount,
            int existingMaintenance,
            int upgradeMaintenance
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID,
                energyCapacity: shipyardCount + 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building replacement = null;
            Building remaining = null;
            for (int index = 0; index < shipyardCount; index++)
            {
                Building facility = AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"legacy-shipyard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship,
                    processRate: 4
                );
                facility.ResearchOrder = 0;
                facility.MaintenanceCost = existingMaintenance;
                facility.Upgrades.Add("advanced-shipyard");
                replacement ??= facility;
                if (index > 0)
                    remaining = facility;
            }

            Building advancedShipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "advanced-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            advancedShipyard.TypeID = "advanced-shipyard";
            advancedShipyard.ProcessRate = 2;
            advancedShipyard.ResearchOrder = 5;
            advancedShipyard.MaintenanceCost = upgradeMaintenance;
            AIProductionDemand demand = new AIProductionDemand(
                "facility-upgrade",
                AIProductionDemandKind.BuildingUpgrade,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                planet,
                1,
                baseDemandPercent: 100,
                buildingToReplace: replacement
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(advancedShipyard),
                false
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            return (game, empire, planet, replacement, remaining, proposal, context);
        }
    }
}
