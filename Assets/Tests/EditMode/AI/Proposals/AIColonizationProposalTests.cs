using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIColonizationProposalTests
    {
        [Test]
        public void Execute_WithRemoteTarget_MovesFleetWithoutRevealingCurrentTargetState()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIColonizationProposal proposal = new AIColonizationProposal(
                fleet,
                FleetOrderStatus.Staging,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            proposal.Execute(context);

            Assert.AreEqual(FleetOrderType.Colonize, fleet.Order.OrderType);
            Assert.AreEqual(target.InstanceID, fleet.Order.TargetPlanetId);
            Assert.AreSame(target, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            Assert.IsNull(target.GetOwnerInstanceID());
        }

        [Test]
        public void Execute_WithFleetAndRegimentAtTarget_ClaimsPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            CapitalShip ship = fleet.CapitalShips.Single();
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID);
            game.AttachNode(regiment, ship);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIColonizationProposal proposal = new AIColonizationProposal(
                fleet,
                FleetOrderStatus.Ready,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            proposal.Execute(context);

            Assert.AreEqual(empire.InstanceID, target.GetOwnerInstanceID());
            Assert.AreEqual(100, target.GetPopularSupport(empire.InstanceID));
            Assert.AreSame(target, regiment.GetParent());
            Assert.IsNull(fleet.Order);
        }

        [Test]
        public void Execute_WithMultipleRegiments_DropsWeakestRegiment()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            Fleet fleet = AddBattleFleet(game, target, empire.InstanceID);
            CapitalShip ship = fleet.CapitalShips.Single();
            Regiment strongerRegiment = AITestSceneBuilder.CreateRegiment(
                "stronger",
                empire.InstanceID
            );
            strongerRegiment.AttackRating = 20;
            strongerRegiment.DefenseRating = 20;
            Regiment weakerRegiment = AITestSceneBuilder.CreateRegiment(
                "weaker",
                empire.InstanceID
            );
            weakerRegiment.AttackRating = 10;
            weakerRegiment.DefenseRating = 10;
            game.AttachNode(strongerRegiment, ship);
            game.AttachNode(weakerRegiment, ship);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIColonizationProposal proposal = new AIColonizationProposal(
                fleet,
                FleetOrderStatus.Ready,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            proposal.Execute(context);

            Assert.AreSame(target, weakerRegiment.GetParent());
            Assert.AreSame(ship, strongerRegiment.GetParent());
        }

        [Test]
        public void Execute_WithStaleTargetState_TravelsBeforeRejectingColonization()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIColonizationProposal proposal = new AIColonizationProposal(
                fleet,
                FleetOrderStatus.Staging,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );
            target.IsColonized = true;
            game.ChangeUnitOwnership(target, rebels.InstanceID);

            proposal.Execute(context);

            Assert.AreSame(target, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderType.Colonize, fleet.Order.OrderType);

            fleet.Movement = null;
            proposal.Execute(context);

            Assert.AreEqual(rebels.InstanceID, target.GetOwnerInstanceID());
            Assert.IsNull(fleet.Order);
        }

        private static Fleet AddBattleFleet(GameRoot game, Planet planet, string ownerInstanceId)
        {
            Fleet fleet = EntityFactory.CreateFleet("fleet", ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, planet);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                ownerInstanceId,
                regimentCapacity: 2
            );
            game.AttachNode(ship, fleet);
            return fleet;
        }
    }
}
