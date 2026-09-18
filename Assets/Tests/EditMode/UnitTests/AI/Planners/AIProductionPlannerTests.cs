using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIProductionPlannerTests
    {
        /// <summary>
        /// Verifies a claimed planet does not bypass shared infrastructure demand.
        /// </summary>
        [Test]
        public void Plan_WithClaimedUncolonizedPlanet_DoesNotAddColonyManufactureProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "producer",
                empire.InstanceID,
                rawResourceNodes: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Planet colony = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "colony",
                empire.InstanceID,
                rawResourceNodes: 2
            );
            colony.IsColonized = false;
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("garrison", empire.InstanceID),
                colony
            );
            Building mine = AITestSceneBuilder.CreateBuildingTemplate(
                "mine-template",
                BuildingType.Mine
            );
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(mine),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context).ToList();

            Assert.IsFalse(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(item => item.Demand.Kind == AIDemandKind.Colony)
            );
        }

        /// <summary>
        /// Verifies plan with mine demand and unlocked mine adds manufacture proposal.
        /// </summary>
        [Test]
        public void Plan_WithMineDemandAndUnlockedMine_AddsManufactureProposal()
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
            AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "resource-expansion-world",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 4
            );
            empire.PendingRawMaterialFacilityIDs.Add("construction-yard");
            Building mine = AITestSceneBuilder.CreateBuildingTemplate(
                "mine-template",
                BuildingType.Mine
            );
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(mine),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(proposal => proposal.Demand.Kind == AIDemandKind.Mine)
            );
        }

        /// <summary>
        /// Verifies non-hub Outer Rim construction remains available while the primary hub expands.
        /// </summary>
        [Test]
        public void Plan_WithIncompleteOuterRimHub_UsesNonHubProducerForMine()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.FacilitySectorHubTargetCount = 5;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "outer-rim");
            sector.SectorType = PlanetSectorType.OuterRim;
            Planet primary = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "primary",
                empire.InstanceID,
                energyCapacity: 6
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                primary,
                "primary-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Planet secondary = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "secondary",
                empire.InstanceID,
                energyCapacity: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                secondary,
                "secondary-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "resource-world",
                empire.InstanceID,
                energyCapacity: 4,
                rawResourceNodes: 4
            );
            empire.PendingRawMaterialFacilityIDs.Add("primary-yard");
            Building mine = AITestSceneBuilder.CreateBuildingTemplate(
                "mine-template",
                BuildingType.Mine
            );
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(mine),
            };

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(AITestSceneBuilder.CreateContext(game, empire))
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.Mine);

            Assert.AreSame(secondary, proposal.ProducerPlanet);
        }

        /// <summary>
        /// Verifies plan with advanced shipyard unlocked selects faster facility.
        /// </summary>
        [Test]
        public void Plan_WithAdvancedShipyardUnlocked_SelectsFasterFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 2
            );
            planet.SetPopularSupport(empire.InstanceID, 100);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AddResourceEconomy(game, planet);
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.ProcessRate = 4;
            shipyard.ResearchOrder = 0;
            Building advancedShipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "advanced-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            advancedShipyard.ProcessRate = 2;
            advancedShipyard.ResearchOrder = 5;
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(shipyard),
                new Technology(advancedShipyard),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.Shipyard);

            Assert.AreSame(advancedShipyard, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with facility upgrade exactly at net maintenance budget selects upgrade.
        /// </summary>
        [Test]
        public void Plan_WithFacilityUpgradeExactlyAtNetMaintenanceBudget_SelectsUpgrade()
        {
            (
                GameRoot game,
                Faction empire,
                Planet _,
                Building replacement,
                Building advancedShipyard,
                int maintenanceBudget
            ) = CreateFacilityUpgradeScene(0);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.BuildingUpgrade);

            Assert.AreSame(replacement, proposal.Demand.BuildingToReplace);
            Assert.AreSame(advancedShipyard, proposal.Product.GetReference());
            Assert.AreEqual(maintenanceBudget, proposal.GetMaintenanceCost());
        }

        /// <summary>
        /// Verifies plan with facility upgrade one over net maintenance budget does not add upgrade.
        /// </summary>
        [Test]
        public void Plan_WithFacilityUpgradeOneOverNetMaintenanceBudget_DoesNotAddUpgrade()
        {
            (GameRoot game, Faction empire, Planet _, Building _, Building _, int _) =
                CreateFacilityUpgradeScene(1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(item => item.Demand.Kind == AIDemandKind.BuildingUpgrade)
            );
        }

        /// <summary>
        /// Verifies plan without authored facility upgrade does not add upgrade.
        /// </summary>
        [Test]
        public void Plan_WithoutAuthoredFacilityUpgrade_DoesNotAddUpgrade()
        {
            (GameRoot game, Faction empire, Planet planet, Building _, Building _, int _) =
                CreateFacilityUpgradeScene(0);
            foreach (Building facility in planet.GetChildren<Building>())
                facility.Upgrades.Clear();
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(item => item.Demand.Kind == AIDemandKind.BuildingUpgrade)
            );
        }

        /// <summary>
        /// Verifies plan with shipyard exactly at maintenance budget selects shipyard.
        /// </summary>
        [Test]
        public void Plan_WithShipyardExactlyAtMaintenanceBudget_SelectsShipyard()
        {
            (GameRoot game, Faction empire, Building shipyard, Building _) =
                CreateShipyardSelectionScene(3, 4);
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(shipyard),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.Shipyard);

            Assert.AreEqual(100, empire.ProjectedMaintenanceHeadroom);
            Assert.AreSame(shipyard, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with primary hub and global headroom selects faster shipyard.
        /// </summary>
        [Test]
        public void Plan_WithPrimaryHubAndGlobalHeadroom_SelectsFasterShipyard()
        {
            (GameRoot game, Faction empire, Building _, Building fasterShipyard) =
                CreateShipyardSelectionScene(3, 4);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.Shipyard);

            Assert.AreSame(fasterShipyard, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with primary hub and global headroom adds shipyard proposal.
        /// </summary>
        [Test]
        public void Plan_WithPrimaryHubAndGlobalHeadroom_AddsShipyardProposal()
        {
            (GameRoot game, Faction empire, Building _, Building overBudgetShipyard) =
                CreateShipyardSelectionScene(3, 4);
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(overBudgetShipyard),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(item => item.Demand.Kind == AIDemandKind.Shipyard)
            );
        }

        /// <summary>
        /// Verifies plan with facility expansion queues one facility.
        /// </summary>
        [Test]
        public void Plan_WithFacilityExpansion_QueuesOneFacility()
        {
            (GameRoot game, Faction empire, Planet planet, Building _) = CreateShipyardBatchScene(
                constructionFacilityCount: 4,
                energyCapacity: 20,
                shipyardMaintenance: 1
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = GetShipyardProposal(context);

            Assert.AreEqual(1, proposal.Demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies plan with remaining shared facility budget adds proposal.
        /// </summary>
        [Test]
        public void Plan_WithRemainingSharedFacilityBudget_AddsProposal()
        {
            (GameRoot game, Faction empire, Planet _, Building _) = CreateShipyardBatchScene(
                constructionFacilityCount: 2,
                energyCapacity: 20,
                shipyardMaintenance: 1
            );
            Planet committedPlanet = AITestSceneBuilder.AddPlanet(
                game,
                game.GetSceneNodesByType<PlanetSector>().Single(),
                "committed-world",
                empire.InstanceID,
                energyCapacity: 4
            );
            Building committedShipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "committed-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.None
            );
            committedShipyard.OwnerInstanceID = empire.InstanceID;
            committedShipyard.MaintenanceCost = 1;
            game.AttachNode(committedShipyard, committedPlanet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = GetShipyardProposal(context);

            Assert.AreEqual(1, proposal.Demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies plan with headroom below facility allocation but enough for primary hub adds proposal.
        /// </summary>
        [Test]
        public void Plan_WithHeadroomBelowFacilityAllocationButEnoughForPrimaryHub_AddsProposal()
        {
            (GameRoot game, Faction empire, Planet planet, Building _) = CreateShipyardBatchScene(
                constructionFacilityCount: 2,
                energyCapacity: 20,
                shipyardMaintenance: 1
            );
            Building maintenanceBurden = AITestSceneBuilder.CreateBuildingTemplate(
                "maintenance-burden",
                BuildingType.Defense,
                ManufacturingType.None
            );
            maintenanceBurden.OwnerInstanceID = empire.InstanceID;
            maintenanceBurden.MaintenanceCost = 98;
            game.AttachNode(maintenanceBurden, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);

            Assert.AreEqual(2, empire.ProjectedMaintenanceHeadroom);
            Assert.IsTrue(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(item => item.Demand.Kind == AIDemandKind.Shipyard)
            );
        }

        /// <summary>
        /// Verifies plan with busy construction queue adds counted facility proposal.
        /// </summary>
        [Test]
        public void Plan_WithConstructionQueueCoveringPlanningHorizon_DoesNotAddFacilityProposal()
        {
            (GameRoot game, Faction empire, Planet planet, Building _) = CreateShipyardBatchScene(
                constructionFacilityCount: 4,
                energyCapacity: 20,
                shipyardMaintenance: 1
            );
            game.Config.AI.Infrastructure.ProductionQueueTargetPlanningIntervals = 1;
            Building queuedBuilding = AITestSceneBuilder.CreateBuildingTemplate(
                "queued-building",
                BuildingType.Defense,
                ManufacturingType.None
            );
            queuedBuilding.OwnerInstanceID = empire.InstanceID;
            queuedBuilding.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(queuedBuilding, planet);
            planet.AddToManufacturingQueue(queuedBuilding);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIManufactureProposal> proposals = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Where(item => item.Demand.Kind == AIDemandKind.Shipyard)
                .ToList();

            Assert.IsEmpty(proposals);
        }

        /// <summary>
        /// Verifies plan with same facility state returns deterministic batch.
        /// </summary>
        [Test]
        public void Plan_WithSameFacilityState_ReturnsDeterministicBatch()
        {
            (GameRoot game, Faction empire, Planet _, Building _) = CreateShipyardBatchScene(
                constructionFacilityCount: 4,
                energyCapacity: 20,
                shipyardMaintenance: 1
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal first = GetShipyardProposal(context);
            AIManufactureProposal second = GetShipyardProposal(context);

            Assert.AreEqual(first.GetSortKey(), second.GetSortKey());
            Assert.AreEqual(first.Demand.QuantityNeeded, second.Demand.QuantityNeeded);
            Assert.AreSame(first.Product.GetReference(), second.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with construction facility exactly at maintenance allocation adds proposal.
        /// </summary>
        [Test]
        public void Plan_WithConstructionFacilityDemand_IgnoresLegacyMaintenanceAllocation()
        {
            (GameRoot game, Faction empire, Building _) = CreateConstructionFacilityBatchScene(
                constructionFacilityCount: 2,
                constructionFacilityMaintenance: 4
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.ConstructionFacility);

            Assert.AreEqual(2, proposal.Demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies plan with construction facility one over maintenance allocation does not add proposal.
        /// </summary>
        [Test]
        public void Plan_WithConstructionFacilityAboveLegacyAllocation_AddsProposal()
        {
            (GameRoot game, Faction empire, Building _) = CreateConstructionFacilityBatchScene(
                constructionFacilityCount: 2,
                constructionFacilityMaintenance: 5
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(item => item.Demand.Kind == AIDemandKind.ConstructionFacility)
            );
        }

        /// <summary>
        /// Verifies plan with construction facility demand uses every construction lane.
        /// </summary>
        [Test]
        public void Plan_WithConstructionFacilityDemand_UsesEveryConstructionLane()
        {
            (GameRoot game, Faction empire, Building _) = CreateConstructionFacilityBatchScene(
                constructionFacilityCount: 2,
                constructionFacilityMaintenance: 1
            );
            game.Config.AI.Infrastructure.ProductionFacilityMaintenanceAllocationPercent = 100;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.ConstructionFacility);

            Assert.AreEqual(2, proposal.Demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies established sectors compound facility expansion from local construction capacity.
        /// </summary>
        [Test]
        public void Plan_WithEstablishedDestinationSector_UsesAllLocalFacilityProducers()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetsPerConstructionFacility = 100;
            game.Config.AI.Infrastructure.FacilitySectorHubTargetCount = 5;
            PlanetSector core = AITestSceneBuilder.AddSector(game, "core");
            Planet coreProducer = AITestSceneBuilder.AddPlanet(
                game,
                core,
                "core-producer",
                empire.InstanceID,
                energyCapacity: 20
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                coreProducer,
                "core-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AddMaintenanceCapacity(game, coreProducer, 1);

            PlanetSector outerRim = AITestSceneBuilder.AddSector(game, "outer-rim");
            outerRim.SectorType = PlanetSectorType.OuterRim;
            Planet localProducer = AITestSceneBuilder.AddPlanet(
                game,
                outerRim,
                "flive",
                empire.InstanceID,
                energyCapacity: 6
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                localProducer,
                "flive-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Planet siblingProducer = AITestSceneBuilder.AddPlanet(
                game,
                outerRim,
                "gamorr",
                empire.InstanceID,
                energyCapacity: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                siblingProducer,
                "gamorr-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building constructionFacility = AITestSceneBuilder.CreateBuildingTemplate(
                "construction-yard-template",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            constructionFacility.MaintenanceCost = 0;
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(constructionFacility),
            };

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(AITestSceneBuilder.CreateContext(game, empire))
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.ConstructionFacility
                    && item.Demand.DestinationPlanet == localProducer
                );

            CollectionAssert.AreEquivalent(
                new[] { localProducer, siblingProducer },
                proposal.ProducerOptions.Select(option => option.ProducerPlanet).ToArray()
            );
        }

        /// <summary>
        /// Verifies plan with planetary shield demand selects strongest shield.
        /// </summary>
        [Test]
        public void Plan_WithPlanetaryShieldDemand_SelectsStrongestShield()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            AITestSceneBuilder.AddProductionFacility(
                game,
                headquarters,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            AddMaintenanceCapacity(game, headquarters, 1);
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense
            );
            shield.ShieldStrength = 40;
            Building deathStarShield = AITestSceneBuilder.CreateBuildingTemplate(
                "death-star-shield",
                BuildingType.Defense
            );
            deathStarShield.ResearchOrder = 3;
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(shield),
                new Technology(deathStarShield),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.PlanetaryDefense
                    && item.Demand.BuildingType == BuildingType.Defense
                );

            Assert.AreSame(shield, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with shield pair exactly at defensive budget queues complete pair.
        /// </summary>
        [Test]
        public void Plan_WithShieldPairExactlyAtDefensiveBudget_QueuesCompletePair()
        {
            (GameRoot game, Faction empire, Building _) = CreatePlanetaryDefenseScene(
                minimumMaintenanceHeadroom: 36,
                shieldMaintenance: 7
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.PlanetaryDefense
                    && item.Demand.BuildingType == BuildingType.Defense
                );

            Assert.AreEqual(2, proposal.Demand.QuantityNeeded);
            Assert.AreEqual(14, proposal.GetMaintenanceCost());
        }

        /// <summary>
        /// Verifies plan with shield pair one over defensive budget queues affordable shield.
        /// </summary>
        [Test]
        public void Plan_WithShieldPairOneOverDefensiveBudget_QueuesAffordableShield()
        {
            (GameRoot game, Faction empire, Building _) = CreatePlanetaryDefenseScene(
                minimumMaintenanceHeadroom: 37,
                shieldMaintenance: 7
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.PlanetaryDefense
                    && item.Demand.BuildingType == BuildingType.Defense
                );

            Assert.AreEqual(1, proposal.Demand.QuantityNeeded);
            Assert.AreEqual(7, proposal.GetMaintenanceCost());
        }

        /// <summary>
        /// Verifies plan with no affordable shield does not add shield proposal.
        /// </summary>
        [Test]
        public void Plan_WithNoAffordableShield_DoesNotAddShieldProposal()
        {
            (GameRoot game, Faction empire, Building _) = CreatePlanetaryDefenseScene(
                minimumMaintenanceHeadroom: 44,
                shieldMaintenance: 7
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(item =>
                        item.Demand.Kind == AIDemandKind.PlanetaryDefense
                        && item.Demand.BuildingType == BuildingType.Defense
                    )
            );
        }

        /// <summary>
        /// Verifies plan with planetary starfighter demand selects efficient defender.
        /// </summary>
        [Test]
        public void Plan_WithPlanetaryStarfighterDemand_SelectsEfficientDefender()
        {
            (GameRoot game, Faction empire, Planet _, Starfighter efficient) =
                CreatePlanetaryStarfighterScene(0, 4, 12);
            Starfighter stronger = AITestSceneBuilder.CreateStarfighter(
                "stronger-fighter",
                empire.InstanceID,
                laserCannon: 20,
                maintenanceCost: 10
            );
            empire.ResearchQueue[ManufacturingType.Ship].Add(new Technology(stronger));
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.PlanetaryStarfighterReserve);

            Assert.AreSame(efficient, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with planetary fighter batch exactly at defensive budget queues planning batch.
        /// </summary>
        [Test]
        public void Plan_WithDisabledPlanetDefenseEfficiency_PrefersStrongerDefender()
        {
            (GameRoot game, Faction empire, Planet _, Starfighter _) =
                CreatePlanetaryStarfighterScene(0, 4, 12);
            game.Config.AI.Selection.TechnologyUtility.Starfighter.PlanetDefenseEfficiency.Weight =
                0;
            Starfighter stronger = AITestSceneBuilder.CreateStarfighter(
                "stronger-fighter",
                empire.InstanceID,
                laserCannon: 20,
                maintenanceCost: 10
            );
            empire.ResearchQueue[ManufacturingType.Ship].Add(new Technology(stronger));
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.PlanetaryStarfighterReserve);

            Assert.AreSame(stronger, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan withplanetaryfighterbatchexactlyatdefensivebudget queuesplanningbatch.
        /// </summary>
        [Test]
        public void Plan_WithPlanetaryFighterBatchExactlyAtDefensiveBudget_QueuesPlanningBatch()
        {
            (GameRoot game, Faction empire, Planet _, Starfighter _) =
                CreatePlanetaryStarfighterScene(10, 10, 10);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.PlanetaryStarfighterReserve);

            Assert.AreEqual(3, proposal.Demand.QuantityNeeded);
            Assert.AreEqual(30, proposal.GetMaintenanceCost());
        }

        /// <summary>
        /// Verifies plan with planetary fighter batch one over defensive budget queues affordable count.
        /// </summary>
        [Test]
        public void Plan_WithPlanetaryFighterBatchOneOverDefensiveBudget_QueuesAffordableCount()
        {
            (GameRoot game, Faction empire, Planet _, Starfighter _) =
                CreatePlanetaryStarfighterScene(11, 10, 10);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.PlanetaryStarfighterReserve);

            Assert.AreEqual(3, proposal.Demand.QuantityNeeded);
            Assert.AreEqual(30, proposal.GetMaintenanceCost());
        }

        /// <summary>
        /// Verifies plan with no affordable planetary fighter does not add proposal.
        /// </summary>
        [Test]
        public void Plan_WithNoAffordablePlanetaryFighter_DoesNotAddProposal()
        {
            (GameRoot game, Faction empire, Planet _, Starfighter _) =
                CreatePlanetaryStarfighterScene(47, 4, 10);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(item => item.Demand.Kind == AIDemandKind.PlanetaryStarfighterReserve)
            );
        }

        /// <summary>
        /// Verifies plan with special forces mission demand selects requested unlocked type.
        /// </summary>
        [Test]
        public void Plan_WithSpecialForcesMissionDemand_SelectsRequestedUnlockedType()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Infrastructure.ProductionQueueTargetPlanningIntervals = 30;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-world",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            SpecialForces commandos = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            SpecialForces spies = AITestSceneBuilder.CreateSpecialForces(
                "spies",
                empire.InstanceID,
                MissionTypeIDs.Espionage
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(commandos),
                new Technology(spies),
            };
            SpecialForces firstCommandos = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            firstCommandos.InstanceID = "commandos-1";
            SpecialForces secondCommandos = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID,
                MissionTypeIDs.Sabotage
            );
            secondCommandos.InstanceID = "commandos-2";
            game.AttachNode(firstCommandos, planet);
            game.AttachNode(secondCommandos, planet);
            Officer officer = EntityFactory.CreateOfficer("officer", empire.InstanceID);
            StubMission mission = EntityFactory.CreateMission(
                "active-espionage",
                empire.InstanceID,
                target.InstanceID
            );
            mission.ConfigKey = MissionTypeIDs.Espionage;
            game.AttachNode(mission, target);
            game.AttachNode(officer, mission);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIManufactureProposal> proposals = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Where(item => item.Demand.Kind == AIDemandKind.SpecialForces)
                .ToList();

            AIManufactureProposal spyProposal = proposals.Single(item =>
                item.Demand.ProductTypeId == "spies"
            );
            Assert.AreEqual(1, proposals.Count);
            Assert.AreEqual(1, spyProposal.Demand.QuantityNeeded);
            Assert.AreSame(spies, spyProposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with fleet deficit adds fleet seed capital ship proposal.
        /// </summary>
        [Test]
        public void Plan_WithFleetDeficit_AddsFleetSeedCapitalShipProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            CapitalShip template = AITestSceneBuilder.CreateCapitalShip(
                "corvette-template",
                empire.InstanceID
            );
            template.TypeID = "corvette";
            template.ManufacturingFactionInstanceIDs.Add(empire.InstanceID);
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(template),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(planet, proposal.Destination);
            Assert.AreSame(template, proposal.Product.GetReference());
            Assert.AreEqual(AICapitalShipProductionRole.General, proposal.Demand.CapitalShipRole);
        }

        /// <summary>
        /// Verifies plan with fleet deficit selects highest general role metric.
        /// </summary>
        [Test]
        public void Plan_WithFleetDeficit_SelectsHighestGeneralRoleMetric()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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

            CapitalShip weakShip = AITestSceneBuilder.CreateCapitalShip(
                "weak-template",
                empire.InstanceID,
                combatStrength: 75,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            weakShip.TypeID = "weak";
            weakShip.ConstructionCost = 30;
            weakShip.MaintenanceCost = 0;
            weakShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = 0;
            weakShip.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 75;
            weakShip.WeaponRecharge = 10;
            CapitalShip battleShip = AITestSceneBuilder.CreateCapitalShip(
                "battle-template",
                empire.InstanceID,
                combatStrength: 260,
                regimentCapacity: 2,
                starfighterCapacity: 1
            );
            battleShip.TypeID = "battle";
            battleShip.ConstructionCost = 44;
            battleShip.MaintenanceCost = 0;
            battleShip.WeaponRecharge = 10;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(weakShip),
                new Technology(battleShip),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(battleShip, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with fleet deficit uses configured laser cannon damage multiplier.
        /// </summary>
        [Test]
        public void Plan_WithFleetDeficit_UsesConfiguredLaserCannonDamageMultiplier()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.Combat.SpaceCombat.LaserCannonCapitalDamageMultiplier = 0.75;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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

            CapitalShip laserShip = AITestSceneBuilder.CreateCapitalShip(
                "laser-template",
                empire.InstanceID,
                combatStrength: 300,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            laserShip.TypeID = "laser";
            laserShip.MaintenanceCost = 0;
            laserShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = 0;
            laserShip.PrimaryWeapons[PrimaryWeaponType.LaserCannon][0] = 300;
            laserShip.WeaponRecharge = 20;
            CapitalShip turbolaserShip = AITestSceneBuilder.CreateCapitalShip(
                "turbolaser-template",
                empire.InstanceID,
                combatStrength: 200,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            turbolaserShip.TypeID = "turbolaser";
            turbolaserShip.MaintenanceCost = 0;
            turbolaserShip.WeaponRecharge = 10;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(laserShip),
                new Technology(turbolaserShip),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(laserShip, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with general deficit does not select planet destroying capital ship.
        /// </summary>
        [Test]
        public void Plan_WithGeneralDeficit_DoesNotSelectPlanetDestroyingCapitalShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            CapitalShip battleShip = AITestSceneBuilder.CreateCapitalShip(
                "battle-template",
                empire.InstanceID,
                combatStrength: 100
            );
            battleShip.TypeID = "battle";
            battleShip.MaintenanceCost = 0;
            battleShip.WeaponRecharge = 10;
            CapitalShip planetDestroyer = AITestSceneBuilder.CreateCapitalShip(
                "planet-destroyer-template",
                empire.InstanceID,
                combatStrength: 1000
            );
            planetDestroyer.TypeID = "planet-destroyer";
            planetDestroyer.CanDestroyPlanets = true;
            planetDestroyer.MaintenanceCost = 0;
            planetDestroyer.WeaponRecharge = 100;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(planetDestroyer),
                new Technology(battleShip),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(battleShip, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with combat and transport deficits selects efficient transport.
        /// </summary>
        [Test]
        public void Plan_WithCombatAndTransportDeficits_SelectsEfficientTransport()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1000;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.Selection.TechnologyUtility.DuplicateCost.Weight = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "existing-ship",
                    empire.InstanceID,
                    combatStrength: 100,
                    regimentCapacity: 0,
                    starfighterCapacity: 0
                ),
                fleet
            );

            CapitalShip lineShip = AITestSceneBuilder.CreateCapitalShip(
                "line-ship-template",
                empire.InstanceID,
                combatStrength: 1000,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            lineShip.TypeID = "line-ship";
            lineShip.ConstructionCost = 0;
            CapitalShip transport = AITestSceneBuilder.CreateCapitalShip(
                "transport-template",
                empire.InstanceID,
                combatStrength: 0,
                regimentCapacity: 2,
                starfighterCapacity: 0
            );
            transport.TypeID = "transport";
            transport.ConstructionCost = 10;
            transport.Roles.Add(CapitalShipRole.Transport);
            CapitalShip slowTransport = AITestSceneBuilder.CreateCapitalShip(
                "slow-transport-template",
                empire.InstanceID,
                combatStrength: 0,
                regimentCapacity: 2,
                starfighterCapacity: 0
            );
            slowTransport.TypeID = "slow-transport";
            slowTransport.ConstructionCost = 100;
            slowTransport.Roles.Add(CapitalShipRole.Transport);
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(lineShip),
                new Technology(transport),
                new Technology(slowTransport),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(
                game,
                empire,
                random: new SequenceRNG(intValues: new[] { 1 })
            );

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetCapitalShip && item.Destination == fleet
                );

            Assert.AreSame(transport, proposal.Product.GetReference());
            Assert.AreEqual(
                AICapitalShipProductionRole.TroopTransport,
                proposal.Demand.CapitalShipRole
            );
        }

        /// <summary>
        /// Verifies plan with regiment strength gap and full capacity selects transport.
        /// </summary>
        [Test]
        public void Plan_WithRegimentStrengthGapAndFullCapacity_SelectsTransport()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 0;
            game.Config.AI.Selection.TechnologyUtility.DuplicateCost.Weight = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID, defenseRating: 20),
                target
            );
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            CapitalShip existingShip = AITestSceneBuilder.CreateCapitalShip(
                "existing-ship",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 1,
                starfighterCapacity: 0
            );
            game.AttachNode(fleet, planet);
            game.AttachNode(existingShip, fleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("attacker", empire.InstanceID, attackRating: 5),
                existingShip
            );

            CapitalShip lineShip = AITestSceneBuilder.CreateCapitalShip(
                "line-ship-template",
                empire.InstanceID,
                combatStrength: 1000,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            lineShip.TypeID = "line-ship";
            lineShip.ConstructionCost = 0;
            CapitalShip transport = AITestSceneBuilder.CreateCapitalShip(
                "transport-template",
                empire.InstanceID,
                combatStrength: 0,
                regimentCapacity: 2,
                starfighterCapacity: 0
            );
            transport.TypeID = "transport";
            transport.ConstructionCost = 0;
            transport.Roles.Add(CapitalShipRole.Transport);
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(lineShip),
                new Technology(transport),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetCapitalShip && item.Destination == fleet
                );

            Assert.AreSame(transport, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with understrength headquarters defense fleet selects combat ship.
        /// </summary>
        [Test]
        public void Plan_WithUnderstrengthHeadquartersDefenseFleet_SelectsCombatShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            AITestSceneBuilder.AddProductionFacility(
                game,
                headquarters,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = headquarters.InstanceID,
            };
            game.AttachNode(fleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "existing-ship",
                    empire.InstanceID,
                    combatStrength: 100,
                    regimentCapacity: 0,
                    starfighterCapacity: 0
                ),
                fleet
            );

            CapitalShip lineShip = AITestSceneBuilder.CreateCapitalShip(
                "line-ship-template",
                empire.InstanceID,
                combatStrength: 1000,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            lineShip.TypeID = "line-ship";
            lineShip.ConstructionCost = 0;
            CapitalShip transport = AITestSceneBuilder.CreateCapitalShip(
                "transport-template",
                empire.InstanceID,
                combatStrength: 0,
                regimentCapacity: 2,
                starfighterCapacity: 0
            );
            transport.TypeID = "transport";
            transport.ConstructionCost = 0;
            transport.Roles.Add(CapitalShipRole.Transport);
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(lineShip),
                new Technology(transport),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetCapitalShip && item.Destination == fleet
                );

            Assert.AreSame(lineShip, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan general role selection selects eligible warship.
        /// </summary>
        /// <param name="hasCommittedCombatShip">Whether has committed combat ship.</param>
        [TestCase(false, TestName = "Plan_WithNoCommittedCombatShip_SelectsEligibleWarship")]
        [TestCase(true, TestName = "Plan_WithCommittedCombatShip_SelectsEligibleWarship")]
        public void Plan_GeneralRoleSelectionSelectsEligibleWarship(bool hasCommittedCombatShip)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1500;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, planet);
            if (hasCommittedCombatShip)
            {
                game.AttachNode(
                    AITestSceneBuilder.CreateCapitalShip(
                        "existing-ship",
                        empire.InstanceID,
                        combatStrength: 100
                    ),
                    fleet
                );
            }
            else
            {
                game.AttachNode(
                    AITestSceneBuilder.CreateCapitalShip(
                        "existing-transport",
                        empire.InstanceID,
                        combatStrength: 0,
                        regimentCapacity: 1
                    ),
                    fleet
                );
            }

            CapitalShip lowerMetricTemplate = AITestSceneBuilder.CreateCapitalShip(
                "lower-metric-template",
                empire.InstanceID,
                combatStrength: 100
            );
            lowerMetricTemplate.TypeID = "lower-metric";
            lowerMetricTemplate.ConstructionCost = 10;
            lowerMetricTemplate.MaintenanceCost = 0;
            lowerMetricTemplate.WeaponRecharge = 5;
            CapitalShip higherMetricTemplate = AITestSceneBuilder.CreateCapitalShip(
                "higher-metric-template",
                empire.InstanceID,
                combatStrength: 1000
            );
            higherMetricTemplate.TypeID = "higher-metric";
            higherMetricTemplate.ConstructionCost = 300;
            higherMetricTemplate.MaintenanceCost = 0;
            higherMetricTemplate.WeaponRecharge = 10;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(lowerMetricTemplate),
                new Technology(higherMetricTemplate),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(
                game,
                empire,
                random: new SequenceRNG(intValues: new[] { 0 })
            );

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetCapitalShip && item.Destination == fleet
                );

            Assert.AreSame(higherMetricTemplate, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan general combat selection fills missing carrier role.
        /// </summary>
        /// <param name="hasCarrier">Whether has carrier.</param>
        [TestCase(false, TestName = "Plan_WithNoCarrier_SelectsCarrierCapableWarship")]
        [TestCase(true, TestName = "Plan_WithCarrier_SelectsHigherQualityWarship")]
        public void Plan_GeneralCombatSelectionFillsMissingCarrierRole(bool hasCarrier)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "existing-warship",
                    empire.InstanceID,
                    combatStrength: 100,
                    starfighterCapacity: hasCarrier ? 1 : 0
                ),
                fleet
            );

            CapitalShip warship = AITestSceneBuilder.CreateCapitalShip(
                "warship-template",
                empire.InstanceID,
                combatStrength: 500,
                starfighterCapacity: 0
            );
            warship.TypeID = "warship";
            warship.MaintenanceCost = 0;
            warship.WeaponRecharge = 10;
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier-template",
                empire.InstanceID,
                combatStrength: 500,
                starfighterCapacity: 6
            );
            carrier.TypeID = "carrier";
            carrier.MaintenanceCost = 0;
            carrier.WeaponRecharge = 5;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(warship),
                new Technology(carrier),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(
                game,
                empire,
                random: new SequenceRNG(intValues: new[] { 1 })
            );

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetCapitalShip && item.Destination == fleet
                );

            Assert.AreSame(hasCarrier ? warship : carrier, proposal.Product.GetReference());
            Assert.AreEqual(AICapitalShipProductionRole.General, proposal.Demand.CapitalShipRole);
        }

        /// <summary>
        /// Verifies plan with bombardment deficit selects efficient bombardment ship.
        /// </summary>
        [Test]
        public void Plan_WithBombardmentDeficit_SelectsEfficientBombardmentShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, target, "shield-1", rebels.InstanceID, 100);
            AddShield(game, target, "shield-2", rebels.InstanceID, 100);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard-facility",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            CapitalShip existingShip = AITestSceneBuilder.CreateCapitalShip(
                "existing",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            existingShip.Bombardment = 10;
            existingShip.RegimentCapacity = 1;
            game.AttachNode(fleet, planet);
            game.AttachNode(existingShip, fleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("fleet-regiment", empire.InstanceID),
                existingShip
            );

            CapitalShip lineShip = AITestSceneBuilder.CreateCapitalShip(
                "line-template",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            lineShip.TypeID = "line";
            CapitalShip bombardmentShip = AITestSceneBuilder.CreateCapitalShip(
                "bombardment-template",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            bombardmentShip.TypeID = "bombardment";
            bombardmentShip.Bombardment = 20;
            bombardmentShip.HasGravityWell = true;
            bombardmentShip.ConstructionCost = 20;
            CapitalShip slowBombardmentShip = AITestSceneBuilder.CreateCapitalShip(
                "slow-bombardment-template",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            slowBombardmentShip.TypeID = "slow-bombardment";
            slowBombardmentShip.Bombardment = 20;
            slowBombardmentShip.ConstructionCost = 200;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(lineShip),
                new Technology(bombardmentShip),
                new Technology(slowBombardmentShip),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetCapitalShip && item.Destination == fleet
                );

            Assert.AreSame(bombardmentShip, proposal.Product.GetReference());
            Assert.AreEqual(
                AICapitalShipProductionRole.Bombardment,
                proposal.Demand.CapitalShipRole
            );
        }

        /// <summary>
        /// Verifies plan with interdiction demand selects eligible interdiction ship.
        /// </summary>
        [Test]
        public void Plan_WithInterdictionDemand_SelectsEligibleInterdictionShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard-facility",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(fleet, planet);
            CapitalShip existingShip = AITestSceneBuilder.CreateCapitalShip(
                "existing",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 1,
                starfighterCapacity: 0
            );
            game.AttachNode(existingShip, fleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("fleet-regiment", empire.InstanceID),
                existingShip
            );
            CapitalShip lowerRecharge = AITestSceneBuilder.CreateCapitalShip(
                "lower-recharge-template",
                empire.InstanceID
            );
            lowerRecharge.TypeID = "lower-recharge";
            lowerRecharge.HasGravityWell = true;
            lowerRecharge.ShieldRechargeRate = 10;
            lowerRecharge.ConstructionCost = 10;
            lowerRecharge.MaintenanceCost = 0;
            CapitalShip higherRecharge = AITestSceneBuilder.CreateCapitalShip(
                "higher-recharge-template",
                empire.InstanceID
            );
            higherRecharge.TypeID = "higher-recharge";
            higherRecharge.HasGravityWell = true;
            higherRecharge.ShieldRechargeRate = 20;
            higherRecharge.ConstructionCost = 100;
            higherRecharge.MaintenanceCost = 0;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(lowerRecharge),
                new Technology(higherRecharge),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(
                game,
                empire,
                random: new SequenceRNG(intValues: new[] { 1 })
            );

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetCapitalShip && item.Destination == fleet
                );

            Assert.AreSame(lowerRecharge, proposal.Product.GetReference());
            Assert.AreEqual(
                AICapitalShipProductionRole.Interdiction,
                proposal.Demand.CapitalShipRole
            );
        }

        /// <summary>
        /// Verifies plan with only bombardment deficit ignores carrier capacity.
        /// </summary>
        [Test]
        public void Plan_WithOnlyBombardmentDeficit_IgnoresCarrierCapacity()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 0;
            game.Config.AI.Selection.TechnologyUtility.DuplicateCost.Weight = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, target, "shield-1", rebels.InstanceID, 100);
            AddShield(game, target, "shield-2", rebels.InstanceID, 100);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard-facility",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            CapitalShip existingShip = AITestSceneBuilder.CreateCapitalShip(
                "existing",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            existingShip.Bombardment = 10;
            existingShip.RegimentCapacity = 1;
            game.AttachNode(fleet, planet);
            game.AttachNode(existingShip, fleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("fleet-regiment", empire.InstanceID),
                existingShip
            );

            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier-template",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 6,
                starfighterCapacity: 12
            );
            carrier.TypeID = "carrier";
            carrier.ConstructionCost = 0;
            carrier.MaintenanceCost = 0;
            CapitalShip bombardmentShip = AITestSceneBuilder.CreateCapitalShip(
                "bombardment-template",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            bombardmentShip.TypeID = "bombardment";
            bombardmentShip.Bombardment = 20;
            bombardmentShip.ConstructionCost = 0;
            bombardmentShip.MaintenanceCost = 0;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(carrier),
                new Technology(bombardmentShip),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetCapitalShip && item.Destination == fleet
                );

            Assert.AreSame(bombardmentShip, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with multiple general candidates and first roll selects first candidate.
        /// </summary>
        [Test]
        public void Plan_WithMultipleGeneralCandidatesAndFirstRoll_SelectsFirstCandidate()
        {
            (GameRoot game, Faction empire, Fleet fleet) = CreateCapitalSelectionScene();

            CapitalShip strongTemplate = AITestSceneBuilder.CreateCapitalShip(
                "strong-template",
                empire.InstanceID,
                combatStrength: 300
            );
            strongTemplate.TypeID = "strong";
            strongTemplate.ConstructionCost = 0;
            strongTemplate.MaintenanceCost = 0;
            strongTemplate.WeaponRecharge = 10;
            CapitalShip alternateTemplate = AITestSceneBuilder.CreateCapitalShip(
                "alternate-template",
                empire.InstanceID,
                combatStrength: 250
            );
            alternateTemplate.TypeID = "alternate";
            alternateTemplate.ConstructionCost = 0;
            alternateTemplate.MaintenanceCost = 0;
            alternateTemplate.WeaponRecharge = 10;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(strongTemplate),
                new Technology(alternateTemplate),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(
                game,
                empire,
                random: new SequenceRNG(intValues: new[] { 0 })
            );

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetCapitalShip && item.Destination == fleet
                );

            Assert.AreSame(alternateTemplate, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with multiple general candidates and second roll selects second candidate.
        /// </summary>
        [Test]
        public void Plan_WithMultipleGeneralCandidatesAndSecondRoll_SelectsSecondCandidate()
        {
            (GameRoot game, Faction empire, Fleet fleet) = CreateCapitalSelectionScene();

            CapitalShip firstTemplate = AITestSceneBuilder.CreateCapitalShip(
                "first-template",
                empire.InstanceID,
                combatStrength: 300
            );
            firstTemplate.TypeID = "first";
            firstTemplate.MaintenanceCost = 0;
            firstTemplate.WeaponRecharge = 10;
            CapitalShip secondTemplate = AITestSceneBuilder.CreateCapitalShip(
                "second-template",
                empire.InstanceID,
                combatStrength: 250
            );
            secondTemplate.TypeID = "second";
            secondTemplate.MaintenanceCost = 0;
            secondTemplate.WeaponRecharge = 10;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(firstTemplate),
                new Technology(secondTemplate),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(
                game,
                empire,
                random: new SequenceRNG(intValues: new[] { 1 })
            );

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetCapitalShip && item.Destination == fleet
                );

            Assert.AreSame(secondTemplate, proposal.Product.GetReference());
        }

        /// <summary>
        /// Verifies plan with repeated starfighter type selects different competitive type.
        /// </summary>
        [Test]
        public void Plan_WithRepeatedStarfighterType_SelectsDifferentCompetitiveType()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.Selection.PreferredStarfighterTypeCountPerFleet = 10;
            game.Config.AI.Selection.TechnologyUtility.DuplicateCost.Weight = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
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
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                combatStrength: 500,
                regimentCapacity: 0,
                starfighterCapacity: 2
            );
            game.AttachNode(fleet, planet);
            game.AttachNode(carrier, fleet);
            Starfighter existingStarfighter = new Starfighter
            {
                InstanceID = "existing-starfighter",
                TypeID = "strong",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(existingStarfighter, carrier);

            Starfighter strongTemplate = new Starfighter
            {
                InstanceID = "strong-template",
                TypeID = "strong",
                OwnerInstanceID = empire.InstanceID,
                LaserCannon = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter alternateTemplate = new Starfighter
            {
                InstanceID = "alternate-template",
                TypeID = "alternate",
                OwnerInstanceID = empire.InstanceID,
                LaserCannon = 5,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(strongTemplate),
                new Technology(alternateTemplate),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetStarfighter && item.Destination == fleet
                );

            Assert.AreEqual(alternateTemplate.TypeID, proposal.Product.GetReference().GetTypeID());
        }

        /// <summary>
        /// Verifies plan with starfighter deficit queues work through next planning tick.
        /// </summary>
        [Test]
        public void Plan_WithStarfighterDeficit_QueuesWorkThroughNextPlanningTick()
        {
            (GameRoot game, Faction empire, Fleet fleet, Planet firstProducer, Planet _) =
                CreateDistributedStarfighterScene(includeSecondProducer: false);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetStarfighter && item.Destination == fleet
                );

            Assert.AreSame(firstProducer, proposal.ProducerPlanet);
            Assert.AreEqual(2, proposal.Demand.QuantityNeeded);
        }

        /// <summary>
        /// Verifies plan with starfighter deficit distributes batch across producer planets.
        /// </summary>
        [Test]
        public void Plan_WithStarfighterDeficit_DistributesBatchAcrossProducerPlanets()
        {
            (
                GameRoot game,
                Faction empire,
                Fleet fleet,
                Planet firstProducer,
                Planet secondProducer
            ) = CreateDistributedStarfighterScene(includeSecondProducer: true);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Dictionary<string, int> quantities = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Where(item =>
                    item.Demand.Kind == AIDemandKind.FleetStarfighter && item.Destination == fleet
                )
                .ToDictionary(
                    item => item.ProducerPlanet.InstanceID,
                    item => item.Demand.QuantityNeeded
                );

            Assert.AreEqual(2, quantities[firstProducer.InstanceID]);
            Assert.AreEqual(2, quantities[secondProducer.InstanceID]);
            Assert.AreEqual(4, quantities.Values.Sum());
        }

        /// <summary>
        /// Verifies fleet reinforcement production uses the producer with the earliest arrival.
        /// </summary>
        [Test]
        public void Plan_WithBackloggedNearbyProducer_UsesEarlierArrivalProducer()
        {
            (
                GameRoot game,
                Faction empire,
                Fleet fleet,
                Planet nearbyProducer,
                Planet distantProducer
            ) = CreateDistributedStarfighterScene(includeSecondProducer: true);
            game.Config.AI.Selection.PreferredStarfighterTypeCountPerFleet = 1;
            CapitalShip queuedShip = AITestSceneBuilder.CreateCapitalShip(
                "queued-ship",
                empire.InstanceID
            );
            queuedShip.ConstructionCost = 1000;
            queuedShip.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(queuedShip, fleet);
            nearbyProducer.AddToManufacturingQueue(queuedShip);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(AITestSceneBuilder.CreateContext(game, empire))
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIDemandKind.FleetStarfighter && item.Destination == fleet
                );

            Assert.AreSame(distantProducer, proposal.ProducerPlanet);
        }

        /// <summary>
        /// Verifies plan with distributed starfighter batch uses preferred fleet type count.
        /// </summary>
        [Test]
        public void Plan_WithDistributedStarfighterBatch_UsesPreferredFleetTypeCount()
        {
            (GameRoot game, Faction empire, Fleet fleet, Planet _, Planet _) =
                CreateDistributedStarfighterScene(includeSecondProducer: true);
            game.Config.AI.Selection.PreferredStarfighterTypeCountPerFleet = 4;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            int quantity = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Where(item =>
                    item.Demand.Kind == AIDemandKind.FleetStarfighter && item.Destination == fleet
                )
                .Sum(item => item.Demand.QuantityNeeded);

            Assert.AreEqual(4, quantity);
        }

        /// <summary>
        /// Verifies plan with queue covering next planning tick does not add more work.
        /// </summary>
        [Test]
        public void Plan_WithQueueCoveringNextPlanningTick_DoesNotAddMoreWork()
        {
            (GameRoot game, Faction empire, Fleet fleet, Planet firstProducer, Planet _) =
                CreateDistributedStarfighterScene(includeSecondProducer: false);
            game.Config.AI.Infrastructure.ProductionQueueTargetPlanningIntervals = 1;
            Starfighter queuedStarfighter = new Starfighter
            {
                InstanceID = "queued-starfighter",
                TypeID = "queued-starfighter",
                OwnerInstanceID = empire.InstanceID,
                ConstructionCost = game.Config.AI.TickInterval,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            CapitalShip carrier = fleet.GetChildren<CapitalShip>().Single();
            game.AttachNode(queuedStarfighter, carrier);
            firstProducer.AddToManufacturingQueue(queuedStarfighter);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIManufactureProposal> proposals = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Where(item =>
                    item.Demand.Kind == AIDemandKind.FleetStarfighter && item.Destination == fleet
                )
                .ToList();

            Assert.IsEmpty(proposals);
        }

        /// <summary>
        /// Gets shipyard proposal.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The requested shipyard proposal.</returns>
        private static AIManufactureProposal GetShipyardProposal(AITurnContext context)
        {
            return new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIDemandKind.Shipyard);
        }

        /// <summary>
        /// Creates distributed starfighter scene.
        /// </summary>
        /// <param name="includeSecondProducer">Whether include second producer.</param>
        /// <returns>The created distributed starfighter scene.</returns>
        private static (
            GameRoot game,
            Faction faction,
            Fleet fleet,
            Planet firstProducer,
            Planet secondProducer
        ) CreateDistributedStarfighterScene(bool includeSecondProducer)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ProductionQueueTargetPlanningIntervals = 4;
            game.Config.AI.NonCapitalSummary.StarfighterRequirementHeadquarters = 0;
            game.Config.AI.NonCapitalSummary.StarfighterRequirementInfrastructure = 0;
            game.Config.AI.NonCapitalSummary.StarfighterRequirementDefault = 0;
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.Selection.PreferredStarfighterTypeCountPerFleet = 10;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "shipyard-system");
            Planet firstProducer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "producer-1",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                firstProducer,
                "shipyard-1",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Planet secondProducer = null;
            if (includeSecondProducer)
            {
                secondProducer = AITestSceneBuilder.AddPlanet(
                    game,
                    system,
                    "producer-2",
                    empire.InstanceID,
                    positionX: 100
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    secondProducer,
                    "shipyard-2",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship
                );
            }

            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                combatStrength: 500,
                regimentCapacity: 0,
                starfighterCapacity: 6
            );
            game.AttachNode(fleet, firstProducer);
            game.AttachNode(carrier, fleet);
            Starfighter template = new Starfighter
            {
                InstanceID = "starfighter-template",
                TypeID = "starfighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingFactionInstanceIDs = new List<string> { empire.InstanceID },
                ConstructionCost = 2,
                MaintenanceCost = 0,
                LaserCannon = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(template),
            };
            return (game, empire, fleet, firstProducer, secondProducer);
        }

        /// <summary>
        /// Creates planetary defense scene.
        /// </summary>
        /// <param name="minimumMaintenanceHeadroom">The minimum maintenance headroom.</param>
        /// <param name="shieldMaintenance">The shield maintenance.</param>
        /// <returns>The created planetary defense scene.</returns>
        private static (
            GameRoot game,
            Faction faction,
            Building shield
        ) CreatePlanetaryDefenseScene(int minimumMaintenanceHeadroom, int shieldMaintenance)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = minimumMaintenanceHeadroom;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "defense-system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defense-world",
                empire.InstanceID
            );
            planet.IsHeadquarters = true;
            empire.HQInstanceID = planet.InstanceID;
            planet.SetPopularSupport(empire.InstanceID, 100);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AddMaintenanceCapacity(game, planet, 1);

            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield-template",
                BuildingType.Defense
            );
            shield.ShieldStrength = 80;
            shield.MaintenanceCost = shieldMaintenance;
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(shield),
            };
            return (game, empire, shield);
        }

        /// <summary>
        /// Creates planetary starfighter scene.
        /// </summary>
        /// <param name="minimumMaintenanceHeadroom">The minimum maintenance headroom.</param>
        /// <param name="fighterMaintenance">The fighter maintenance.</param>
        /// <param name="fighterStrength">The fighter strength.</param>
        /// <returns>The created planetary starfighter scene.</returns>
        private static (
            GameRoot game,
            Faction faction,
            Planet planet,
            Starfighter starfighter
        ) CreatePlanetaryStarfighterScene(
            int minimumMaintenanceHeadroom,
            int fighterMaintenance,
            int fighterStrength
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.ProductionQueueTargetPlanningIntervals = 3;
            game.Config.AI.Selection.MaintenanceHeadroomReserve = minimumMaintenanceHeadroom;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            game.Config.AI.NonCapitalSummary.RequireStaticDefenseBeforeStarfighters = false;
            game.Config.AI.NonCapitalSummary.InteriorStarfighterBaselinePercent = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "defense-system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defense-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AddMaintenanceCapacity(game, planet, 1);
            Starfighter starfighter = AITestSceneBuilder.CreateStarfighter(
                "planetary-fighter",
                empire.InstanceID,
                laserCannon: fighterStrength,
                maintenanceCost: fighterMaintenance
            );
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(starfighter),
            };
            return (game, empire, planet, starfighter);
        }

        /// <summary>
        /// Creates shipyard batch scene.
        /// </summary>
        /// <param name="constructionFacilityCount">The construction facility count.</param>
        /// <param name="energyCapacity">The energy capacity.</param>
        /// <param name="shipyardMaintenance">The shipyard maintenance.</param>
        /// <returns>The created shipyard batch scene.</returns>
        private static (
            GameRoot game,
            Faction faction,
            Planet planet,
            Building shipyard
        ) CreateShipyardBatchScene(
            int constructionFacilityCount,
            int energyCapacity,
            int shipyardMaintenance
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.ProductionFacilityMaintenanceAllocationPercent = 3;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "shipyard-system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID,
                energyCapacity: energyCapacity,
                rawResourceNodes: 2
            );
            planet.SetPopularSupport(empire.InstanceID, 100);
            for (int index = 0; index < constructionFacilityCount; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"construction-yard-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            }
            AddResourceEconomy(game, planet);

            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard-template",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.MaintenanceCost = shipyardMaintenance;
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(shipyard),
            };
            return (game, empire, planet, shipyard);
        }

        /// <summary>
        /// Creates construction facility batch scene.
        /// </summary>
        /// <param name="constructionFacilityCount">The construction facility count.</param>
        /// <param name="constructionFacilityMaintenance">The construction facility maintenance.</param>
        /// <returns>The created construction facility batch scene.</returns>
        private static (
            GameRoot game,
            Faction faction,
            Building constructionFacility
        ) CreateConstructionFacilityBatchScene(
            int constructionFacilityCount,
            int constructionFacilityMaintenance
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.ConstructionFacilityTargetClearTicks = 1;
            game.Config.AI.Infrastructure.ProductionQueueTargetPlanningIntervals = 100;
            game.Config.AI.Infrastructure.ProductionFacilityMaintenanceAllocationPercent = 4;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "construction-system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID,
                energyCapacity: 40,
                rawResourceNodes: 4
            );
            planet.SetPopularSupport(empire.InstanceID, 100);
            for (int index = 0; index < constructionFacilityCount; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"construction-yard-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
                Building queuedBuilding = AITestSceneBuilder.CreateBuildingTemplate(
                    $"queued-building-{index}",
                    BuildingType.Defense
                );
                queuedBuilding.OwnerInstanceID = empire.InstanceID;
                queuedBuilding.ManufacturingStatus = ManufacturingStatus.Building;
                game.AttachNode(queuedBuilding, planet);
                planet.AddToManufacturingQueue(queuedBuilding);
            }
            AddResourceEconomy(game, planet);

            Building constructionFacility = AITestSceneBuilder.CreateBuildingTemplate(
                "construction-facility-template",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            constructionFacility.MaintenanceCost = constructionFacilityMaintenance;
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(constructionFacility),
            };
            return (game, empire, constructionFacility);
        }

        /// <summary>
        /// Adds resource economy.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        private static void AddResourceEconomy(GameRoot game, Planet planet)
        {
            planet.NumRawResourceNodes += 2;
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
        }

        /// <summary>
        /// Adds maintenance capacity.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="count">The count.</param>
        private static void AddMaintenanceCapacity(GameRoot game, Planet planet, int count)
        {
            planet.NumRawResourceNodes += count;
            for (int index = 0; index < count; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"maintenance-mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"maintenance-refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }
        }

        /// <summary>
        /// Adds shield.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="strength">The strength.</param>
        private static void AddShield(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId,
            int strength
        )
        {
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                instanceId,
                BuildingType.Defense
            );
            shield.OwnerInstanceID = ownerInstanceId;
            shield.ShieldStrength = strength;
            game.AttachNode(shield, planet);
        }

        /// <summary>
        /// Creates capital selection scene.
        /// </summary>
        /// <returns>The created capital selection scene.</returns>
        private static (GameRoot game, Faction faction, Fleet fleet) CreateCapitalSelectionScene()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "capital-system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID,
                rawResourceNodes: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            for (int index = 0; index < 2; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.Building
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.Building
                );
            }

            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip existingShip = AITestSceneBuilder.CreateCapitalShip(
                "existing-ship",
                empire.InstanceID,
                combatStrength: 100
            );
            existingShip.TypeID = "existing";
            existingShip.MaintenanceCost = 0;
            game.AttachNode(fleet, planet);
            game.AttachNode(existingShip, fleet);
            return (game, empire, fleet);
        }

        /// <summary>
        /// Creates facility upgrade scene.
        /// </summary>
        /// <param name="maintenanceBudgetOffset">The maintenance budget offset.</param>
        /// <returns>The created facility upgrade scene.</returns>
        private static (
            GameRoot game,
            Faction faction,
            Planet planet,
            Building replacement,
            Building advancedShipyard,
            int maintenanceBudget
        ) CreateFacilityUpgradeScene(int maintenanceBudgetOffset)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 20;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "upgrade-system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "upgrade-world",
                empire.InstanceID,
                energyCapacity: 5
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building replacement = AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "legacy-a",
                BuildingType.Shipyard,
                ManufacturingType.Ship,
                processRate: 4
            );
            Building remaining = AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "legacy-b",
                BuildingType.Shipyard,
                ManufacturingType.Ship,
                processRate: 4
            );
            replacement.MaintenanceCost = 10;
            remaining.MaintenanceCost = 10;
            replacement.Upgrades.Add("advanced-shipyard");
            remaining.Upgrades.Add("advanced-shipyard");
            AddMaintenanceCapacity(game, planet, 1);

            int reserve =
                (
                    empire.MaintenanceCapacity
                        * game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent
                    + 99
                ) / 100;
            int maintenanceBudget = empire.ProjectedMaintenanceHeadroom - reserve;
            Building advancedShipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "advanced-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            advancedShipyard.TypeID = "advanced-shipyard";
            advancedShipyard.ProcessRate = 2;
            advancedShipyard.ResearchOrder = 5;
            advancedShipyard.MaintenanceCost =
                replacement.MaintenanceCost + maintenanceBudget + maintenanceBudgetOffset;
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(advancedShipyard),
            };
            return (game, empire, planet, replacement, advancedShipyard, maintenanceBudget);
        }

        /// <summary>
        /// Creates shipyard selection scene.
        /// </summary>
        /// <param name="affordableMaintenance">The affordable maintenance.</param>
        /// <param name="overBudgetMaintenance">The over budget maintenance.</param>
        /// <returns>The created shipyard selection scene.</returns>
        private static (
            GameRoot game,
            Faction faction,
            Building affordableShipyard,
            Building overBudgetShipyard
        ) CreateShipyardSelectionScene(int affordableMaintenance, int overBudgetMaintenance)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.ProductionFacilityMaintenanceAllocationPercent = 3;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "shipyard-system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 2
            );
            planet.SetPopularSupport(empire.InstanceID, 100);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AddResourceEconomy(game, planet);

            Building affordableShipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "affordable-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            affordableShipyard.ProcessRate = 2;
            affordableShipyard.MaintenanceCost = affordableMaintenance;
            Building overBudgetShipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "over-budget-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            overBudgetShipyard.ProcessRate = 1;
            overBudgetShipyard.MaintenanceCost = overBudgetMaintenance;
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(affordableShipyard),
                new Technology(overBudgetShipyard),
            };
            return (game, empire, affordableShipyard, overBudgetShipyard);
        }
    }
}
