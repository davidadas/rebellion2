using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIProductionPlannerTests
    {
        [Test]
        public void Plan_WithMineDemandAndUnlockedMine_AddsManufactureProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
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
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(mine),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(proposal => proposal.Demand.Kind == AIProductionDemandKind.Mine)
            );
        }

        [Test]
        public void Plan_WithAdvancedShipyardUnlocked_SelectsFasterFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
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
                .Single(item => item.Demand.Kind == AIProductionDemandKind.Shipyard);

            Assert.AreSame(advancedShipyard, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithShipyardExactlyAtMaintenanceBudget_SelectsShipyard()
        {
            (GameRoot game, Faction empire, Building shipyard, Building _) =
                CreateShipyardSelectionScene(100, 101);
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(shipyard),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIProductionDemandKind.Shipyard);

            Assert.AreEqual(100, empire.ProjectedMaintenanceHeadroom);
            Assert.AreSame(shipyard, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithFasterShipyardOneOverMaintenanceBudget_SelectsAffordableShipyard()
        {
            (GameRoot game, Faction empire, Building affordableShipyard, Building _) =
                CreateShipyardSelectionScene(100, 101);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIProductionDemandKind.Shipyard);

            Assert.AreSame(affordableShipyard, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithoutAffordableShipyard_DoesNotAddShipyardProposal()
        {
            (GameRoot game, Faction empire, Building _, Building overBudgetShipyard) =
                CreateShipyardSelectionScene(100, 101);
            empire.ResearchQueue[ManufacturingType.Building] = new List<Technology>
            {
                new Technology(overBudgetShipyard),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);

            Assert.IsFalse(
                proposals
                    .OfType<AIManufactureProposal>()
                    .Any(item => item.Demand.Kind == AIProductionDemandKind.Shipyard)
            );
        }

        [Test]
        public void Plan_WithHeadquartersShieldDemand_SelectsStrongestShield()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
                    item.Demand.Kind == AIProductionDemandKind.HeadquartersDefense
                    && item.Demand.BuildingType == BuildingType.Defense
                );

            Assert.AreSame(shield, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithSpecialForcesDeficit_SelectsRequestedUnlockedType()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
            SpecialForces commandos = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID
            );
            SpecialForces spies = AITestSceneBuilder.CreateSpecialForces(
                "spies",
                empire.InstanceID
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(commandos),
                new Technology(spies),
            };
            SpecialForces firstCommandos = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID
            );
            firstCommandos.InstanceID = "commandos-1";
            SpecialForces secondCommandos = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID
            );
            secondCommandos.InstanceID = "commandos-2";
            game.AttachNode(firstCommandos, planet);
            game.AttachNode(secondCommandos, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIProductionDemandKind.SpecialForces);

            Assert.AreEqual("spies", proposal.Demand.ProductTypeId);
            Assert.AreSame(spies, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithFleetDeficit_AddsFleetSeedCapitalShipProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
            template.AllowedOwnerInstanceIDs.Add(empire.InstanceID);
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(template),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item => item.Demand.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(planet, proposal.Destination);
            Assert.AreSame(template, proposal.Product.GetReference());
            Assert.AreEqual(AICapitalShipProductionRole.General, proposal.Demand.CapitalShipRole);
        }

        [Test]
        public void Plan_WithFleetDeficit_SelectsHighestGeneralRoleMetric()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
                .Single(item => item.Demand.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(battleShip, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithGeneralDeficit_DoesNotSelectPlanetDestroyingCapitalShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.Combat.Bombardment.PlanetDestroyingCapitalShipTypeIDs = new List<string>
            {
                "planet-destroyer",
            };
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
                .Single(item => item.Demand.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(battleShip, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithCombatAndTroopTransportDeficits_SelectsTransport()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1000;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.Selection.LocalDuplicatePenaltyPerSelection = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                );

            Assert.AreSame(transport, proposal.Product.GetReference());
            Assert.AreEqual(
                AICapitalShipProductionRole.TroopTransport,
                proposal.Demand.CapitalShipRole
            );
        }

        [Test]
        public void Plan_WithRegimentStrengthGapAndFullCapacity_SelectsTransport()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 0;
            game.Config.AI.Selection.LocalDuplicatePenaltyPerSelection = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                );

            Assert.AreSame(transport, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithUnderstrengthHeadquartersDefenseFleet_SelectsCombatShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                );

            Assert.AreSame(lineShip, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithGeneralRole_SelectsHighestEfficiencyMetric()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1500;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
                    combatStrength: 100
                ),
                fleet
            );

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
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                );

            Assert.AreSame(higherMetricTemplate, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithBombardmentDeficit_SelectsBombardmentCapableCapitalShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, target, "shield-1", rebels.InstanceID, 10);
            AddShield(game, target, "shield-2", rebels.InstanceID, 10);
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
            game.AttachNode(fleet, planet);
            game.AttachNode(existingShip, fleet);

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
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(lineShip),
                new Technology(bombardmentShip),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                );

            Assert.AreSame(bombardmentShip, proposal.Product.GetReference());
            Assert.AreEqual(
                AICapitalShipProductionRole.Bombardment,
                proposal.Demand.CapitalShipRole
            );
        }

        [Test]
        public void Plan_WithOnlyBombardmentDeficit_IgnoresCarrierCapacity()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.Selection.LocalDuplicatePenaltyPerSelection = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, target, "shield-1", rebels.InstanceID, 10);
            AddShield(game, target, "shield-2", rebels.InstanceID, 10);
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
            game.AttachNode(fleet, planet);
            game.AttachNode(existingShip, fleet);

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
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                );

            Assert.AreSame(bombardmentShip, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithCapitalShipExactlyAtMaintenanceBudget_SelectsCapitalShip()
        {
            (GameRoot game, Faction empire, Fleet fleet) = CreateCapitalSelectionScene();

            CapitalShip template = AITestSceneBuilder.CreateCapitalShip(
                "exactly-affordable-template",
                empire.InstanceID,
                combatStrength: 300
            );
            template.TypeID = "exactly-affordable";
            template.MaintenanceCost = 27;
            template.WeaponRecharge = 10;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(template),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                );

            Assert.AreEqual(100, empire.MaintenanceCapacity);
            Assert.AreSame(template, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithCapitalShipOneOverMaintenanceBudget_SelectsAffordableCandidate()
        {
            (GameRoot game, Faction empire, Fleet fleet) = CreateCapitalSelectionScene();

            CapitalShip affordableTemplate = AITestSceneBuilder.CreateCapitalShip(
                "affordable-template",
                empire.InstanceID,
                combatStrength: 300
            );
            affordableTemplate.TypeID = "affordable";
            affordableTemplate.MaintenanceCost = 27;
            affordableTemplate.WeaponRecharge = 5;
            CapitalShip overBudgetTemplate = AITestSceneBuilder.CreateCapitalShip(
                "over-budget-template",
                empire.InstanceID,
                combatStrength: 1000
            );
            overBudgetTemplate.TypeID = "over-budget";
            overBudgetTemplate.MaintenanceCost = 28;
            overBudgetTemplate.WeaponRecharge = 100;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(affordableTemplate),
                new Technology(overBudgetTemplate),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                );

            Assert.AreSame(affordableTemplate, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithoutAffordableCapitalShip_DoesNotAddCapitalShipProposal()
        {
            (GameRoot game, Faction empire, Fleet fleet) = CreateCapitalSelectionScene();

            CapitalShip affordableTransport = AITestSceneBuilder.CreateCapitalShip(
                "affordable-transport-template",
                empire.InstanceID,
                combatStrength: 0,
                regimentCapacity: 4
            );
            affordableTransport.TypeID = "affordable-transport";
            affordableTransport.MaintenanceCost = 27;
            CapitalShip overBudgetTemplate = AITestSceneBuilder.CreateCapitalShip(
                "over-budget-template",
                empire.InstanceID
            );
            overBudgetTemplate.TypeID = "over-budget";
            overBudgetTemplate.MaintenanceCost = 28;
            overBudgetTemplate.WeaponRecharge = 10;
            empire.ResearchQueue[ManufacturingType.Ship] = new List<Technology>
            {
                new Technology(affordableTransport),
                new Technology(overBudgetTemplate),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIManufactureProposal> proposals = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Where(item =>
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                )
                .ToList();

            Assert.IsEmpty(proposals);
        }

        [Test]
        public void Plan_WithEqualGeneralMetricsAndLowTieRoll_SelectsLaterCandidate()
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
                random: new SequenceRNG(intValues: new[] { 49 })
            );

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                );

            Assert.AreSame(alternateTemplate, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithEqualGeneralMetricsAndHighTieRoll_KeepsEarlierCandidate()
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
                random: new SequenceRNG(intValues: new[] { 50 })
            );

            AIManufactureProposal proposal = new AIProductionPlanner()
                .Plan(context)
                .OfType<AIManufactureProposal>()
                .Single(item =>
                    item.Demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.Destination == fleet
                );

            Assert.AreSame(firstTemplate, proposal.Product.GetReference());
        }

        [Test]
        public void Plan_WithRepeatedStarfighterType_SelectsDifferentCompetitiveType()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.Selection.MaxDuplicateStarfighterTypePerFleet = 10;
            game.Config.AI.Selection.LocalDuplicatePenaltyPerSelection = 1000;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
                    item.Demand.Kind == AIProductionDemandKind.FleetStarfighter
                    && item.Destination == fleet
                );

            Assert.AreSame(alternateTemplate, proposal.Product.GetReference());
        }

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
            shield.DefenseFacilityClass = DefenseFacilityClass.Shield;
            shield.ShieldStrength = strength;
            game.AttachNode(shield, planet);
        }

        private static (GameRoot game, Faction faction, Fleet fleet) CreateCapitalSelectionScene()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MaximumConcurrentAttackOrders = 0;
            game.Config.AI.FleetDeployment.MaximumConcurrentColonizationOrders = 0;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.Selection.CapitalMaintenanceAllocationPercent = 30;
            game.Config.AI.Selection.CapitalMaintenanceSafetyPercent = 90;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "capital-system");
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

        private static (
            GameRoot game,
            Faction faction,
            Building affordableShipyard,
            Building overBudgetShipyard
        ) CreateShipyardSelectionScene(int affordableMaintenance, int overBudgetMaintenance)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MaximumConcurrentAttackOrders = 0;
            game.Config.AI.FleetDeployment.MaximumConcurrentColonizationOrders = 0;
            game.Config.AI.Selection.MaintenanceHeadroomHardFloor = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "shipyard-system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID,
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
