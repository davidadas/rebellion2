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
    public class AICreateFleetForOrderProposalTests
    {
        [Test]
        public void Execute_WithValidAttackOrder_CreatesFleetWithOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AICreateFleetForOrderProposal proposal = new AICreateFleetForOrderProposal(
                empire.InstanceID,
                staging,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );

            proposal.Execute(context);

            Fleet fleet = staging.GetFleets()[0];
            Assert.AreEqual(FleetRoleType.Battle, fleet.RoleType);
            Assert.AreEqual(empire.InstanceID, fleet.GetOwnerInstanceID());
            Assert.AreEqual(FleetOrderType.Attack, fleet.Order.OrderType);
            Assert.AreEqual(FleetOrderStatus.Staging, fleet.Order.Status);
            Assert.AreEqual(target.InstanceID, fleet.Order.TargetPlanetId);
        }

        [Test]
        public void CanExecute_WithIdleBattleFleet_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet idleFleet = EntityFactory.CreateFleet("idle", empire.InstanceID);
            idleFleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(idleFleet, staging);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID);
            game.AttachNode(ship, idleFleet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AICreateFleetForOrderProposal proposal = new AICreateFleetForOrderProposal(
                empire.InstanceID,
                staging,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                target
            );

            bool canExecute = proposal.CanExecute(context);

            Assert.IsFalse(canExecute);
        }
    }
}
