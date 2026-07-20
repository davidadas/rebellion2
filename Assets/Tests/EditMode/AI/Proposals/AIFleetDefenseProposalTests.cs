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
    public class AIFleetDefenseProposalTests
    {
        [Test]
        public void Execute_WithReadyDefenseFleet_MovesFleetToHeadquarters()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 100;
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
            AIFleetDefenseProposal proposal = new AIFleetDefenseProposal(fleet, headquarters);

            proposal.Execute(context);

            Assert.AreSame(headquarters, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderType.Defend, fleet.Order.OrderType);
            Assert.AreEqual(FleetOrderStatus.Readying, fleet.Order.Status);
            Assert.AreEqual(headquarters.InstanceID, fleet.Order.TargetPlanetId);
        }

        [Test]
        public void Execute_WithInsufficientDefenseFleet_MovesFleetToHeadquarters()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
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
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, fleetPlanet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "ship",
                    empire.InstanceID,
                    combatStrength: 100
                ),
                fleet
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetDefenseProposal proposal = new AIFleetDefenseProposal(fleet, headquarters);

            proposal.Execute(context);

            Assert.AreSame(headquarters, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderType.Defend, fleet.Order.OrderType);
            Assert.AreEqual(FleetOrderStatus.Readying, fleet.Order.Status);
            Assert.AreEqual(headquarters.InstanceID, fleet.Order.TargetPlanetId);
        }

        [Test]
        public void Execute_WithThreatenedOwnedPlanet_MovesFleetToPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID
            );
            Planet threatenedPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "threatened-world",
                empire.InstanceID
            );
            Fleet fleet = AddBattleFleet(
                game,
                fleetPlanet,
                empire.InstanceID,
                "defense-fleet",
                combatStrength: 1000
            );
            AddBattleFleet(
                game,
                threatenedPlanet,
                rebels.InstanceID,
                "hostile-fleet",
                combatStrength: 500
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetDefenseProposal proposal = new AIFleetDefenseProposal(fleet, threatenedPlanet);

            proposal.Execute(context);

            Assert.AreSame(threatenedPlanet, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(FleetOrderType.Defend, fleet.Order.OrderType);
            Assert.AreEqual(FleetOrderStatus.Readying, fleet.Order.Status);
            Assert.AreEqual(threatenedPlanet.InstanceID, fleet.Order.TargetPlanetId);
        }

        [Test]
        public void Execute_AfterPlanetThreatEnds_ClearsDefenseOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet fleetPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fleet-world",
                empire.InstanceID
            );
            Planet defendedPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defended-world",
                empire.InstanceID
            );
            Fleet fleet = AddBattleFleet(game, fleetPlanet, empire.InstanceID, "defense-fleet");
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = defendedPlanet.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetDefenseProposal proposal = new AIFleetDefenseProposal(fleet, defendedPlanet);

            proposal.Execute(context);

            Assert.IsNull(fleet.Order);
            Assert.IsNull(fleet.Movement);
            Assert.AreSame(fleetPlanet, fleet.GetParent());
        }

        private static Fleet AddBattleFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId,
            string fleetId,
            int combatStrength = 100
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(fleetId, ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    $"{fleetId}-ship",
                    ownerInstanceId,
                    combatStrength
                ),
                fleet
            );
            return fleet;
        }
    }
}
