using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.GameEvents;
using Rebellion.Gameplay.Scripting;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class DuelCommandsTests
    {
        [Test]
        public void Resolve_AuthoredCompletionEffect_AppliesOnceBeforeReturning()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "rebels" };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            Officer first = EntityFactory.CreateOfficer("first", "rebels");
            first.ForceValue = 10;
            game.AttachNode(first, planet);
            Officer second = EntityFactory.CreateOfficer("second", "empire");
            second.IsCaptured = true;
            game.AttachNode(second, planet);
            second.IsCaptured = false;
            game.GetEventPool()
                .Add(
                    new GameEvent
                    {
                        InstanceID = "completion",
                        Triggers = new List<GameTrigger> { new DuelCompletedTrigger() },
                        Actions = new List<GameAction>
                        {
                            new IncreaseForceRankAction { OfficerInstanceID = "first", Amount = 3 },
                        },
                    }
                );
            FixedRandomProvider random = new FixedRandomProvider(new[] { 0.99 });
            GameEventCommands events = new GameEventCommands(
                game,
                random,
                new GameActionCommands(game, random)
            );
            DuelCommands commands = new DuelCommands(
                game,
                random,
                encounter => events.ProcessTriggeredEvents(new[] { encounter })
            );

            List<GameResult> results = commands.Resolve(first, second);

            Assert.AreEqual(13, first.ForceValue);
            Assert.AreEqual(1, game.EventRuntime.GetState("completion").ActivationCount);
            Assert.IsEmpty(results.OfType<DuelResult>());
            Assert.IsNotEmpty(results);
            Assert.That(results.All(result => result.ReportContext is DuelResult));
            Assert.AreEqual(1, results.Select(result => result.ReportContext).Distinct().Count());
            Assert.IsEmpty(events.ProcessTriggeredEvents(results));
            Assert.AreEqual(1, game.EventRuntime.GetState("completion").ActivationCount);
        }
    }
}
