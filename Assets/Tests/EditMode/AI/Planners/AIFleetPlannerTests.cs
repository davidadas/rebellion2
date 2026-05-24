using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIFleetPlannerTests
    {
        [Test]
        public void Plan_WithIdleBattleFleetAndEnemyPlanet_AddsAttackProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = AddBattleFleet(game, owned, empire.InstanceID, "fleet");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIFleetAttackProposal>()
                    .Any(proposal =>
                        proposal.Fleet == fleet
                        && proposal.TargetPlanet == enemy
                        && proposal.OrderType == FleetOrderType.Attack
                    )
            );
        }

        [Test]
        public void Plan_WithAssemblingAttackFleet_AddsDifferentAttackOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet firstEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "first-enemy",
                rebels.InstanceID
            );
            Planet secondEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "second-enemy",
                rebels.InstanceID
            );
            Fleet assemblingFleet = AddBattleFleet(game, owned, empire.InstanceID, "assembling");
            assemblingFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = firstEnemy.InstanceID,
            };
            Fleet idleFleet = AddBattleFleet(game, owned, empire.InstanceID, "idle");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProposal> proposals = new AIFleetPlanner().Plan(context);

            Assert.IsTrue(
                proposals
                    .OfType<AIFleetAttackProposal>()
                    .Any(proposal =>
                        proposal.Fleet == idleFleet && proposal.TargetPlanet == secondEnemy
                    )
            );
        }

        private static Fleet AddBattleFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId,
            string fleetId
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(fleetId, ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                $"{fleetId}-ship",
                ownerInstanceId
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, planet);
            return fleet;
        }
    }
}
