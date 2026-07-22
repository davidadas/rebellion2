using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIFleetAttackProposalTests
    {
        [Test]
        public void Execute_WithFleetNotReady_AssignsBuildingOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
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
        public void Execute_WithTargetReadyButCampaignUnderstrength_MovesToTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 100;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Planet fortifiedEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fortified-enemy",
                rebels.InstanceID
            );
            enemy.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            Fleet fleet = AddBattleFleet(
                game,
                owned,
                empire.InstanceID,
                combatStrength: 500,
                fleetId: "attacker"
            );
            AddBattleFleet(
                game,
                fortifiedEnemy,
                rebels.InstanceID,
                combatStrength: 1000,
                fleetId: "defender"
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            AITestSceneBuilder.RevealPlanet(game, empire, fortifiedEnemy);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                enemy
            );

            proposal.Execute(context);

            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderStatus.Readying, fleet.Order.Status);
        }

        [Test]
        public void Execute_WithOrbitalAdvantageAndNoTroops_MovesToTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 5000;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(
                game,
                owned,
                empire.InstanceID,
                combatStrength: 1000,
                fleetId: "attacker"
            );
            AddBattleFleet(
                game,
                enemy,
                rebels.InstanceID,
                combatStrength: 500,
                fleetId: "defender"
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                enemy
            );

            proposal.Execute(context);

            Assert.AreSame(enemy, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderStatus.Readying, fleet.Order.Status);
        }

        [Test]
        public void CanExecute_WithFriendlyTarget_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet friendlyTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
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
        public void Execute_WithStagedOrderForDifferentTarget_ReplacesOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet firstTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "first-target",
                rebels.InstanceID
            );
            Planet secondTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "second-target",
                rebels.InstanceID
            );
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = firstTarget.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                secondTarget
            );

            proposal.Execute(context);

            Assert.AreEqual(secondTarget.InstanceID, fleet.Order.TargetPlanetId);
            Assert.AreEqual(FleetOrderStatus.Building, fleet.Order.Status);
        }

        [Test]
        public void CanExecute_WithReadyOrderForDifferentTarget_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet firstTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "first-target",
                rebels.InstanceID
            );
            Planet secondTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "second-target",
                rebels.InstanceID
            );
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = firstTarget.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                secondTarget
            );

            bool canExecute = proposal.CanExecute(context);

            Assert.IsFalse(canExecute);
        }

        [Test]
        public void Execute_WithCompletedAttackOrder_ClearsOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", empire.InstanceID);
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
        public void Execute_WithStaleTargetOwnership_TravelsBeforeClearingOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID),
                fleet.CapitalShips[0]
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );
            game.ChangeUnitOwnership(target, empire.InstanceID);
            Assert.AreSame(target, game.GetSceneNodeByInstanceID<Planet>(target.InstanceID));

            proposal.Execute(context);

            Assert.AreSame(target, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderType.Attack, fleet.Order.OrderType);

            fleet.Movement = null;
            proposal.Execute(context);

            Assert.IsNull(fleet.Order);
        }

        [Test]
        public void Execute_WithShieldedTarget_BombardsBeforeAssaulting()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, target, "shield-1", rebels.InstanceID);
            AddShield(game, target, "shield-2", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            fleet.CapitalShips[0].Bombardment = 100;
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID),
                fleet.CapitalShips[0]
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                fleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Ready,
                target
            );

            proposal.Execute(context);

            Assert.IsTrue(context.Results.Any(result => result is BombardmentResult));
            Assert.IsFalse(context.Results.Any(result => result is PlanetaryAssaultResult));
        }

        private static Fleet AddBattleFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId,
            int combatStrength = 100,
            string fleetId = "fleet"
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(fleetId, ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                $"{fleetId}-ship",
                ownerInstanceId,
                combatStrength
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, planet);
            return fleet;
        }

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
            shield.DefenseFacilityClass = DefenseFacilityClass.Shield;
            shield.ShieldStrength = 1;
            game.AttachNode(shield, planet);
        }
    }
}
