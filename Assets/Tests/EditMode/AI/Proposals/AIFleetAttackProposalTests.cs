using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIFleetAttackProposalTests
    {
        [Test]
        public void Execute_WithFleetNotReady_AssignsStagingOrder()
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
            Assert.AreEqual(FleetOrderStatus.Staging, fleet.Order.Status);
            Assert.AreEqual(enemy.InstanceID, fleet.Order.TargetPlanetId);
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

        private static Fleet AddBattleFleet(GameRoot game, Planet planet, string ownerInstanceId)
        {
            Fleet fleet = EntityFactory.CreateFleet("fleet", ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", ownerInstanceId);
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, planet);
            return fleet;
        }
    }
}
