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
        public void Execute_WithRemoteTarget_MovesColonizationFleetIntact()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddColonizationFleet(game, owned, empire.InstanceID);
            CapitalShip carrier = fleet.GetChildren<CapitalShip>().Single();
            CapitalShip combatShip = AITestSceneBuilder.CreateCapitalShip(
                "combat-ship",
                empire.InstanceID,
                combatStrength: 500,
                regimentCapacity: 0
            );
            game.AttachNode(combatShip, fleet);
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID);
            game.AttachNode(regiment, carrier);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIColonizationProposal proposal = new AIColonizationProposal(
                fleet,
                FleetOrderStatus.Staging,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            proposal.Execute(context);

            Assert.AreSame(target, fleet.GetParent());
            Assert.AreEqual(FleetRoleType.Colonization, fleet.RoleType);
            Assert.AreEqual(FleetOrderType.Colonize, fleet.Order.OrderType);
            Assert.AreEqual(target.InstanceID, fleet.Order.TargetPlanetId);
            Assert.IsNotNull(fleet.Movement);
            Assert.AreSame(carrier, regiment.GetParent());
            Assert.AreSame(fleet, combatShip.GetParent());
            Assert.IsNull(target.GetOwnerInstanceID());
        }

        [Test]
        public void Execute_WithFleetAndRegimentAtTarget_ClaimsPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            Fleet fleet = AddColonizationFleet(game, target, empire.InstanceID);
            CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();
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
        public void Execute_WithColonizationFleetAtTarget_PreservesFleetRole()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            Fleet fleet = AddColonizationFleet(game, target, empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Colonize,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = target.InstanceID,
            };
            CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();
            game.AttachNode(AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID), ship);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIColonizationProposal proposal = new AIColonizationProposal(
                fleet,
                FleetOrderStatus.Ready,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            proposal.Execute(context);

            Assert.AreEqual(FleetRoleType.Colonization, fleet.RoleType);
            Assert.IsNull(fleet.Order);
        }

        [Test]
        public void Execute_WithMultipleRegiments_DropsWeakestRegiment()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            Fleet fleet = AddColonizationFleet(game, target, empire.InstanceID);
            CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();
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
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddColonizationFleet(game, owned, empire.InstanceID);
            CapitalShip carrier = fleet.GetChildren<CapitalShip>().Single();
            CapitalShip combatShip = AITestSceneBuilder.CreateCapitalShip(
                "combat-ship",
                empire.InstanceID,
                combatStrength: 500,
                regimentCapacity: 0
            );
            game.AttachNode(combatShip, fleet);
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID);
            game.AttachNode(regiment, carrier);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIColonizationProposal proposal = new AIColonizationProposal(
                fleet,
                FleetOrderStatus.Staging,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );
            target.IsColonized = true;
            game.ChangeOwnership(target, rebels.InstanceID);

            proposal.Execute(context);

            Assert.AreSame(target, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderType.Colonize, fleet.Order.OrderType);

            fleet.Movement = null;
            new AIColonizationProposal(
                fleet,
                fleet.Order.Status,
                context.Assessment.GetKnownPlanet(target.InstanceID)
            ).Execute(context);

            Assert.AreEqual(rebels.InstanceID, target.GetOwnerInstanceID());
            Assert.IsNull(fleet.Order);
        }

        private static Fleet AddColonizationFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId
        )
        {
            Fleet fleet = EntityFactory.CreateFleet("fleet", ownerInstanceId);
            fleet.RoleType = FleetRoleType.Colonization;
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
