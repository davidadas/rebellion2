using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIProductionDemandGeneratorTests
    {
        [Test]
        public void Generate_WithUnminedResourcesAndBalancedEconomy_AddsMineAndRefineryDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            AITestSceneBuilder.AddPlanet(
                game,
                system,
                "resource-world",
                empire.InstanceID,
                rawResourceNodes: 4
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(demands.Any(demand => demand.Kind == AIProductionDemandKind.Mine));
            Assert.IsTrue(demands.Any(demand => demand.Kind == AIProductionDemandKind.Refinery));
        }

        [Test]
        public void Generate_WithInboundFacilityMeetingTarget_DoesNotAddDuplicateDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "destination",
                empire.InstanceID
            );
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "inbound-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.OwnerInstanceID = empire.InstanceID;
            shipyard.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(shipyard, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(demands.Any(demand => demand.Kind == AIProductionDemandKind.Shipyard));
        }

        [Test]
        public void Generate_WithUndefendedHeadquarters_AddsShieldAndWeaponDemands()
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
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            CollectionAssert.AreEquivalent(
                new[] { BuildingType.Defense, BuildingType.Weapon },
                demands
                    .Where(demand => demand.Kind == AIProductionDemandKind.HeadquartersDefense)
                    .Select(demand => demand.BuildingType)
            );
        }

        [Test]
        public void Generate_WithFleetCapacityGaps_AddsFleetReinforcementDemands()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Infrastructure.StarfighterParentFillPercent = 100;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 100;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 10,
                regimentCapacity: 1,
                starfighterCapacity: 2
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetStarfighter)
            );
            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetRegiment)
            );
        }

        [Test]
        public void Generate_WithAttackRegimentStrengthGap_AddsFleetRegimentDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID, defenseRating: 20),
                enemy
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = AddAttackFleet(
                game,
                owned,
                enemy,
                empire.InstanceID,
                regimentCapacity: 2
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("attacker", empire.InstanceID, attackRating: 5),
                fleet.CapitalShips.Single()
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetRegiment
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithAttackRegimentStrengthGapAndFullCapacity_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID, defenseRating: 20),
                enemy
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = AddAttackFleet(
                game,
                owned,
                enemy,
                empire.InstanceID,
                regimentCapacity: 1
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("attacker", empire.InstanceID, attackRating: 5),
                fleet.CapitalShips.Single()
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.TroopTransport, demand.CapitalShipRole);
        }

        [Test]
        public void Generate_WithWeakIdleBattleFleet_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumAttackStrength
                    - ship.GetPrimaryWeaponStrength(),
                demand.QuantityNeeded
            );
            Assert.AreEqual(AICapitalShipProductionRole.General, demand.CapitalShipRole);
        }

        [Test]
        public void Generate_WithShieldedAttackTargetAndInsufficientBombardment_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, enemy, "shield-1", rebels.InstanceID, 10);
            AddShield(game, enemy, "shield-2", rebels.InstanceID, 10);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            ship.Bombardment = 10;
            game.AttachNode(fleet, owned);
            game.AttachNode(ship, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(11, demand.QuantityNeeded);
            Assert.AreEqual(AICapitalShipProductionRole.Bombardment, demand.CapitalShipRole);
        }

        [Test]
        public void Generate_WithUnderstrengthHeadquartersDefenseFleet_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile-fleet", rebels.InstanceID);
            hostileFleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(hostileFleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "hostile-ship",
                    rebels.InstanceID,
                    combatStrength: 2000
                ),
                hostileFleet
            );
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = headquarters.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 1000,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            game.AttachNode(fleet, fleetPlanet);
            game.AttachNode(ship, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(1500, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithInboundCapitalShipFillingCombatNeed_DoesNotAddCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip presentShip = AITestSceneBuilder.CreateCapitalShip(
                "present-ship",
                empire.InstanceID,
                combatStrength: 100,
                regimentCapacity: 1
            );
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound-ship",
                empire.InstanceID,
                combatStrength: 400
            );
            inboundShip.Movement = new MovementState { TransitTicks = 10 };
            fleet.AddChild(presentShip);
            presentShip.SetParent(fleet);
            fleet.AddChild(inboundShip);
            inboundShip.SetParent(fleet);
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == fleet
                )
            );
        }

        [TestCase(ManufacturingStatus.Building)]
        [TestCase(ManufacturingStatus.Complete)]
        public void Generate_WithCommittedCapitalShipFillingCombatNeed_DoesNotAddCapitalShipDemand(
            ManufacturingStatus manufacturingStatus
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 500;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "committed-ship",
                empire.InstanceID,
                combatStrength: 500,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            ship.ManufacturingStatus = manufacturingStatus;
            game.AttachNode(fleet, owned);
            game.AttachNode(ship, fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    && demand.DestinationFleet == fleet
                )
            );
        }

        [Test]
        public void Generate_WithColonizationFleetMissingRegimentCapacity_AddsCapitalShipDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Colonize,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                regimentCapacity: 0,
                starfighterCapacity: 0
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.FleetCapitalShip
                    && item.DestinationFleet == fleet
                );

            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void Generate_WithSpecialForcesDeficit_AddsDemandForUnlockedType()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-world",
                empire.InstanceID
            );
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID
            );
            empire.ResearchQueue[ManufacturingType.Troop] = new List<Technology>
            {
                new Technology(template),
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.SpecialForces);

            Assert.AreEqual("commandos", demand.ProductTypeId);
            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(
                game.Config.AI.Infrastructure.SpecialForcesTargetCountPerType,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void Generate_WithTooFewCommittedBattleFleets_AddsFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(planet, demand.DestinationPlanet);
            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumBattleFleetCount,
                demand.QuantityNeeded
            );
        }

        [Test]
        public void Generate_WithExpandingTerritory_ScalesFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.PlanetsPerBattleFleet = 2;
            game.Config.AI.FleetDeployment.MaximumBattleFleetCount = 5;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            for (int index = 0; index < 7; index++)
            {
                AITestSceneBuilder.AddPlanet(game, system, $"owned-{index}", empire.InstanceID);
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreEqual(4, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithFleetRoleCapacityDeficit_AddsFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MaximumConcurrentAttackOrders = 2;
            game.Config.AI.FleetDeployment.MaximumConcurrentColonizationOrders = 1;
            game.Config.AI.FleetDeployment.MaximumBattleFleetCount = 8;
            game.Config.AI.FleetDeployment.PlanetsPerBattleFleet = 100;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            for (int index = 0; index < 3; index++)
            {
                Fleet fleet = EntityFactory.CreateFleet($"fleet-{index}", empire.InstanceID);
                fleet.RoleType = FleetRoleType.Battle;
                game.AttachNode(fleet, headquarters);
                game.AttachNode(
                    AITestSceneBuilder.CreateCapitalShip($"ship-{index}", empire.InstanceID),
                    fleet
                );
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreEqual(1, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithUnguardedHeadquartersAndFleetRoleDeficit_AddsHeadquartersFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, fleetPlanet);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item => item.Kind == AIProductionDemandKind.FleetSeedCapitalShip);

            Assert.AreSame(headquarters, demand.DestinationPlanet);
            Assert.AreEqual(3, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithMaximumFleetCountAndUnguardedHeadquarters_DoesNotAddFleetSeedDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumBattleFleetCount = 1;
            game.Config.AI.FleetDeployment.MaximumBattleFleetCount = 1;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, fleetPlanet);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetSeedCapitalShip)
            );
        }

        [Test]
        public void Generate_WithUnderGarrisonedPlanet_AddsRequiredGarrisonDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "under-garrisoned",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 20);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIProductionDemand demand = new AIProductionDemandGenerator()
                .Generate(context)
                .Single(item =>
                    item.Kind == AIProductionDemandKind.GarrisonRegimentReserve
                    && item.DestinationPlanet == planet
                );

            Assert.AreEqual(4, demand.QuantityNeeded);
        }

        [Test]
        public void Generate_WithSatisfiedGarrisonRequirement_DoesNotAddGarrisonDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "garrisoned",
                empire.InstanceID
            );
            planet.SetPopularSupport(empire.InstanceID, 20);
            for (int index = 0; index < 4; index++)
            {
                game.AttachNode(
                    AITestSceneBuilder.CreateRegiment($"regiment-{index}", empire.InstanceID),
                    planet
                );
            }
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsFalse(
                demands.Any(demand =>
                    demand.Kind == AIProductionDemandKind.GarrisonRegimentReserve
                    && demand.DestinationPlanet == planet
                )
            );
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

        private static Fleet AddAttackFleet(
            GameRoot game,
            Planet location,
            Planet target,
            string ownerInstanceId,
            int regimentCapacity
        )
        {
            Fleet fleet = EntityFactory.CreateFleet("fleet", ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(fleet, location);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "ship",
                    ownerInstanceId,
                    regimentCapacity: regimentCapacity
                ),
                fleet
            );
            return fleet;
        }
    }
}
