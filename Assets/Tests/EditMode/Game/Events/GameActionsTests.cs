using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameActionsTests
    {
        private GameRoot BuildGame(out Planet empPlanet, out Planet rebelPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            empPlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(empPlanet, system);
            rebelPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(rebelPlanet, system);
            return game;
        }

        [Test]
        public void Execute_ValidIDs_PopulatesAttackersAndDefenders()
        {
            GameRoot game = BuildGame(out Planet empPlanet, out Planet rebelPlanet);
            Officer attacker = EntityFactory.CreateOfficer("a1", "empire");
            Officer defender = EntityFactory.CreateOfficer("d1", "rebels");
            game.AttachNode(attacker, empPlanet);
            game.AttachNode(defender, rebelPlanet);

            TriggerDuelAction action = new TriggerDuelAction
            {
                AttackerInstanceIDs = new List<string> { "a1" },
                DefenderInstanceIDs = new List<string> { "d1" },
            };

            List<GameResult> results = action.Execute(game);

            DuelTriggeredResult duel = results.OfType<DuelTriggeredResult>().First();
            Assert.AreEqual(1, duel.Attackers.Count);
            Assert.AreEqual("a1", duel.Attackers[0].InstanceID);
            Assert.AreEqual(1, duel.Defenders.Count);
            Assert.AreEqual("d1", duel.Defenders[0].InstanceID);
        }

        [Test]
        public void Execute_StaleInstanceID_IsFilteredFromResult()
        {
            GameRoot game = BuildGame(out Planet empPlanet, out Planet rebelPlanet);
            Officer attacker = EntityFactory.CreateOfficer("a1", "empire");
            game.AttachNode(attacker, empPlanet);

            TriggerDuelAction action = new TriggerDuelAction
            {
                AttackerInstanceIDs = new List<string> { "a1", "stale-id" },
                DefenderInstanceIDs = new List<string> { "also-stale" },
            };

            List<GameResult> results = action.Execute(game);

            DuelTriggeredResult duel = results.OfType<DuelTriggeredResult>().First();
            Assert.AreEqual(1, duel.Attackers.Count, "Stale attacker ID should be filtered");
            Assert.AreEqual(0, duel.Defenders.Count, "Stale defender ID should be filtered");
            Assert.IsFalse(
                duel.Attackers.Any(o => o == null),
                "Attackers list should contain no nulls"
            );
        }
    }
}
