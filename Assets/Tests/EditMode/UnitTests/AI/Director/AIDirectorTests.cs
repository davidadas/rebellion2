using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public class AIDirectorTests
    {
        /// <summary>Verifies before configured interval does not process faction.</summary>
        [Test]
        public void ProcessTick_BeforeConfiguredInterval_DoesNotProcessFaction()
        {
            (GameRoot game, Fleet fleet, AIDirector system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval - 1;

            system.ProcessTick();

            Assert.IsNull(fleet.Order);
        }

        /// <summary>Verifies at configured interval processes faction.</summary>
        [Test]
        public void ProcessTick_AtConfiguredInterval_ProcessesFaction()
        {
            (GameRoot game, Fleet fleet, AIDirector system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval;

            system.ProcessTick();

            Assert.IsNotNull(fleet.Order);
            Assert.AreEqual(FleetOrderType.Attack, fleet.Order.OrderType);
        }

        /// <summary>Verifies at configured interval yields between work units.</summary>
        [Test]
        public void ProcessTickIncrementally_AtConfiguredInterval_YieldsBetweenWorkUnits()
        {
            (GameRoot game, Fleet fleet, AIDirector system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval;
            List<GameResult> results = new List<GameResult>();

            int completedSteps = system.ProcessTickIncrementally(results).Count();

            Assert.AreEqual(14, completedSteps);
            Assert.IsNotNull(fleet.Order);
            Assert.AreEqual(FleetOrderType.Attack, fleet.Order.OrderType);
        }

        /// <summary>Verifies that a disabled AI interval produces no work units.</summary>
        /// <param name="interval">The disabled tick interval.</param>
        [TestCase(0)]
        [TestCase(-1)]
        public void ProcessTickIncrementally_DisabledInterval_ProducesNoWork(int interval)
        {
            (GameRoot game, Fleet fleet, AIDirector system) = BuildScene();
            game.Config.AI.TickInterval = interval;
            List<GameResult> results = new List<GameResult>();

            Assert.IsEmpty(system.ProcessTickIncrementally(results));
            Assert.IsEmpty(results);
            Assert.IsNull(fleet.Order);
        }

        /// <summary>Verifies that a human-controlled faction is not assigned AI orders.</summary>
        [Test]
        public void ProcessTick_HumanControlledFaction_DoesNotIssueOrders()
        {
            (GameRoot game, Fleet fleet, AIDirector system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval;
            game.GetPlayers()
                .Add(
                    new Player
                    {
                        PlayerID = "second-human",
                        FactionID = fleet.OwnerInstanceID,
                        ControllerType = PlayerControllerType.Human,
                    }
                );

            Assert.IsEmpty(system.ProcessTick());
            Assert.IsNull(fleet.Order);
        }

        /// <summary>Verifies that disposing after the context yield does not execute later phases.</summary>
        [Test]
        public void ProcessTickIncrementally_DisposedAfterFirstYield_DoesNotExecuteOrders()
        {
            (GameRoot game, Fleet fleet, AIDirector system) = BuildScene();
            game.CurrentTick = game.Config.AI.TickInterval;
            List<GameResult> results = new List<GameResult>();

            using (
                IEnumerator<object> steps = system.ProcessTickIncrementally(results).GetEnumerator()
            )
            {
                Assert.IsTrue(steps.MoveNext());
            }

            Assert.IsNull(fleet.Order);
            Assert.IsEmpty(results);
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <returns>The constructed scene.</returns>
        private static (GameRoot Game, Fleet Fleet, AIDirector System) BuildScene()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.GetPlayers()
                .Add(
                    new Player
                    {
                        PlayerID = "player",
                        FactionID = rebels.InstanceID,
                        ControllerType = PlayerControllerType.Human,
                    }
                );
            game.Config.AI.TickInterval = 7;
            game.Config.AI.FleetDeployment.AttackOpportunityCostPenaltyWeight = 0;
            PlanetSector planetSystem = AITestSceneBuilder.AddSector(game, "system");
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
            FogOfWarCommands fogOfWar = new FogOfWarCommands(game);
            PlanetaryControlCommands control = new PlanetaryControlCommands(
                game,
                context.Movement,
                context.Manufacturing,
                fogOfWar,
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            BombardmentCommands bombardment = new BombardmentCommands(
                game,
                context.Random,
                context.Movement,
                control,
                new BombardmentQueries(game)
            );
            PlanetaryAssaultCommands planetaryAssault = new PlanetaryAssaultCommands(
                game,
                context.Random,
                control,
                new PlanetaryAssaultQueries(game)
            );
            AIDirector system = new AIDirector(
                game,
                context.Missions,
                new MissionQueries(game),
                context.Movement,
                context.Manufacturing,
                bombardment,
                new BombardmentQueries(game),
                planetaryAssault,
                new PlanetaryAssaultQueries(game),
                context.Random,
                new FogOfWarQueries(game)
            );

            return (game, fleet, system);
        }
    }
}
