using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class AISystemTests
    {
        [Test]
        public void ProcessTick_BeforeConfiguredInterval_DoesNotProcessFaction()
        {
            (GameRoot game, Fleet fleet, AISystem system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval - 1;

            system.ProcessTick();

            Assert.IsNull(fleet.Order);
        }

        [Test]
        public void ProcessTick_AtConfiguredInterval_ProcessesFaction()
        {
            (GameRoot game, Fleet fleet, AISystem system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval;

            system.ProcessTick();

            Assert.IsNotNull(fleet.Order);
            Assert.AreEqual(FleetOrderType.Attack, fleet.Order.OrderType);
        }

        private static (GameRoot Game, Fleet Fleet, AISystem System) BuildScene()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            rebels.PlayerID = "player";
            game.Config.AI.TickInterval = 7;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 0;
            PlanetSystem planetSystem = AITestSceneBuilder.AddSystem(game, "system");
            Planet owned = AITestSceneBuilder.AddPlanet(
                game,
                planetSystem,
                "owned",
                empire.InstanceID
            );
            Planet enemy = AITestSceneBuilder.AddPlanet(
                game,
                planetSystem,
                "enemy",
                rebels.InstanceID
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);

            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID);
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, owned);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            PlanetaryControlSystem control = new PlanetaryControlSystem(
                game,
                context.Movement,
                context.Manufacturing,
                context.FogOfWar
            );
            BombardmentSystem bombardment = new BombardmentSystem(
                game,
                context.Random,
                context.Movement,
                control
            );
            PlanetaryAssaultSystem planetaryAssault = new PlanetaryAssaultSystem(
                game,
                context.Random,
                control
            );
            AISystem system = new AISystem(
                game,
                context.Missions,
                context.Movement,
                context.Manufacturing,
                bombardment,
                planetaryAssault,
                context.Random,
                context.FogOfWar
            );

            return (game, fleet, system);
        }
    }
}
