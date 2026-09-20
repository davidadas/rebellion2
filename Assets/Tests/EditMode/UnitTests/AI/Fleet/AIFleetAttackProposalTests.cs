using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Core;
using Rebellion.AI.Fleets;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Core.Helpers;

namespace Rebellion.Tests.AI.Fleets
{
    [TestFixture]
    public class AIFleetAttackProposalTests
    {
        [Test]
        public void Execute_WithFleetNotReady_AssignsBuildingOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "enemy",
                rebels.InstanceID
            );
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                enemy
            );

            proposal.Execute(context);

            Assert.IsNotNull(fleet.Order);
            Assert.AreEqual(FleetOrderType.Attack, fleet.Order.OrderType);
            Assert.AreEqual(FleetOrderStatus.Building, fleet.Order.Status);
            Assert.AreEqual(enemy.InstanceID, fleet.Order.TargetPlanetId);
        }

        [Test]
        public void Execute_WithStaleTargetIntelligence_AppliesUncertaintyReserve()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            ConfigureMinimalAttackRequirements(game);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(game, staging, empire.InstanceID);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = fleet.GetCombatValue();
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            game.CurrentTick =
                game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks + 1;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            proposal.Execute(context);

            Assert.IsNull(fleet.Movement);
            Assert.AreEqual(FleetOrderStatus.Building, fleet.Order.Status);
        }

        [Test]
        public void Execute_WithInboundShipArrivingAtTargetAfterFleet_WaitsAtStagingPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            ConfigureMinimalAttackRequirements(game);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            staging.PositionX = 0;
            target.PositionX = 10000;
            Fleet fleet = AddBattleFleet(game, staging, empire.InstanceID, regimentCount: 6);
            CapitalShip inbound = AITestSceneBuilder.CreateCapitalShip(
                "inbound",
                empire.InstanceID,
                combatStrength: 100
            );
            inbound.Movement = new MovementState
            {
                TransitTicks = 100,
                CurrentPosition = new Point(-10000, 0),
            };
            game.AttachNode(inbound, fleet);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );

            proposal.Execute(context);

            Assert.IsNull(fleet.Movement);
            Assert.AreEqual(FleetOrderStatus.Building, fleet.Order.Status);
        }

        [Test]
        public void Execute_WithInboundShipArrivingAtTargetBeforeFleet_SynchronizesArrival()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            ConfigureMinimalAttackRequirements(game);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            staging.PositionX = 0;
            target.PositionX = 10000;
            Fleet fleet = AddBattleFleet(game, staging, empire.InstanceID, regimentCount: 6);
            CapitalShip inbound = AITestSceneBuilder.CreateCapitalShip(
                "inbound",
                empire.InstanceID,
                combatStrength: 100
            );
            inbound.Movement = new MovementState
            {
                TransitTicks = 100,
                CurrentPosition = new Point(9000, 0),
            };
            game.AttachNode(inbound, fleet);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );

            proposal.Execute(context);

            Assert.IsNotNull(fleet.Movement);
            Assert.IsNotNull(inbound.Movement);
            Assert.AreEqual(fleet.Movement.TransitTicks, inbound.Movement.TransitTicks);
        }

        [Test]
        public void Execute_WithInboundShipArrivingAtTargetWithFleet_LaunchesAttack()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            ConfigureMinimalAttackRequirements(game);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            staging.PositionX = 0;
            target.PositionX = 10000;
            Fleet fleet = AddBattleFleet(game, staging, empire.InstanceID, regimentCount: 6);
            CapitalShip inbound = AITestSceneBuilder.CreateCapitalShip(
                "inbound",
                empire.InstanceID,
                combatStrength: 100
            );
            inbound.Movement = new MovementState
            {
                TransitTicks = 100,
                CurrentPosition = new Point(0, 0),
            };
            game.AttachNode(inbound, fleet);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );

            proposal.Execute(context);

            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderStatus.Readying, fleet.Order.Status);
        }

        [Test]
        public void Execute_WithCompletedAttackOrder_ClearsOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "target",
                empire.InstanceID
            );
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            proposal.Execute(context);

            Assert.IsNull(fleet.Order);
        }

        [Test]
        public void CanExecute_WithFriendlyTarget_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "owned",
                empire.InstanceID
            );
            Planet friendlyTarget = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "friendly",
                empire.InstanceID
            );
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                friendlyTarget
            );

            bool canExecute = proposal.CanExecute(context);

            Assert.IsFalse(canExecute);
        }

        [Test]
        public void Execute_WithReturningDeliveryOnlyFleet_RebasesFleetAndInboundDelivery()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet friendly = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "friendly",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            friendly.PositionX = 0;
            target.PositionX = 10000;
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            CapitalShip inbound = fleet.GetChildren<CapitalShip>().Single();
            inbound.ManufacturingStatus = ManufacturingStatus.Delivering;
            inbound.Movement = new MovementState
            {
                TransitTicks = 20,
                TicksElapsed = 5,
                CurrentPosition = new Point(7500, 0),
            };
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = target.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Returning,
                target
            );

            proposal.Execute(context);

            Assert.AreSame(friendly, fleet.GetParentOfType<Planet>());
            Assert.IsNull(fleet.Movement);
            Assert.IsNotNull(inbound.Movement);
            Assert.AreEqual(0, inbound.Movement.TicksElapsed);
            Assert.AreEqual(new Point(7500, 0), inbound.Movement.OriginPosition);
        }

        [Test]
        public void Execute_WithExposedDefendingRegiment_BombardsBeforeAssaulting()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            fleet.GetChildren<CapitalShip>().Single().Bombardment = 10;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                target
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            proposal.Execute(context);

            Assert.AreEqual(1, context.Results.OfType<BombardmentResult>().Count());
        }

        [Test]
        public void Execute_WithImpenetrableShields_ReturnsOrderToBuilding()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            AddShield(game, target, "shield-1", rebels.InstanceID);
            AddShield(game, target, "shield-2", rebels.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            proposal.Execute(context);

            Assert.AreEqual(FleetOrderStatus.Building, fleet.Order.Status);
        }

        [Test]
        public void Execute_WithNoViableBombardmentOrAssault_ReturnsOrderToBuilding()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            AddShield(game, target, "shield", rebels.InstanceID);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                target
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            proposal.Execute(context);

            Assert.AreEqual(FleetOrderStatus.Building, fleet.Order.Status);
            Assert.IsEmpty(context.Results.OfType<BombardmentResult>());
            Assert.IsEmpty(context.Results.OfType<PlanetaryAssaultResult>());
        }

        [Test]
        public void Execute_WithSuccessfulPlanetaryAssault_AddsGarrisonChangeResult()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent = 0;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 100);
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();
            game.AttachNode(AITestSceneBuilder.CreateRegiment("attacker", empire.InstanceID), ship);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            proposal.Execute(context);

            PlanetaryAssaultResult assault = context
                .Results.OfType<PlanetaryAssaultResult>()
                .Single();
            Assert.IsTrue(assault.Success);
            Assert.AreSame(
                target,
                context.Results.OfType<PlanetGarrisonChangedResult>().Single().Planet
            );
        }

        /// <summary>
        /// Adds battle fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="regimentCount">The number of ready regiments to load.</param>
        /// <returns>The result of add battle fleet.</returns>
        private static Fleet AddBattleFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId,
            int regimentCount = 0
        )
        {
            Fleet fleet = EntityFactory.CreateFleet("fleet", ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                ownerInstanceId,
                regimentCapacity: System.Math.Max(1, regimentCount)
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            for (int index = 0; index < regimentCount; index++)
            {
                Regiment regiment = AITestSceneBuilder.CreateRegiment(
                    $"regiment-{index}",
                    ownerInstanceId
                );
                ship.AddChild(regiment);
                regiment.SetParent(ship);
            }
            game.AttachNode(fleet, planet);
            return fleet;
        }

        /// <summary>
        /// Configures attack readiness to depend only on available fleet combat strength.
        /// </summary>
        /// <param name="game">The game to configure.</param>
        private static void ConfigureMinimalAttackRequirements(GameRoot game)
        {
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent = 0;
        }

        /// <summary>
        /// Adds shield.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        private static void AddShield(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId
        )
        {
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                instanceId,
                BuildingType.Defense
            );
            shield.OwnerInstanceID = ownerInstanceId;
            shield.ShieldStrength = 10;
            game.AttachNode(shield, planet);
        }
    }
}
