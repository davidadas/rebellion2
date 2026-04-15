using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    /// <summary>
    /// Tests for BlockadeSystem.
    /// Tests transition detection (start/end) and evacuation loss rolls.
    /// Does NOT test blockade detection logic (that's Planet.IsBlockaded(), tested in PlanetTests).
    /// </summary>
    [TestFixture]
    public class BlockadeSystemTests
    {
        private (GameRoot game, Planet planet, Fleet hostileFleet) BuildScene()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            Planet planet = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(hostileFleet, planet);

            return (game, planet, hostileFleet);
        }

        [Test]
        public void ProcessTick_NewBlockade_EmitsBlockadeStarted()
        {
            (GameRoot game, Planet planet, _) = BuildScene();
            BlockadeSystem manager = new BlockadeSystem(game, new StubRNG());

            List<GameResult> results = manager.ProcessTick();

            BlockadeChangedResult result = results.OfType<BlockadeChangedResult>().FirstOrDefault();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Blockaded);
            Assert.AreEqual(planet, result.Planet);
        }

        [Test]
        public void ProcessTick_AlreadyBlockaded_NoRepeatedEvent()
        {
            (GameRoot game, _, _) = BuildScene();
            BlockadeSystem manager = new BlockadeSystem(game, new StubRNG());

            manager.ProcessTick();
            List<GameResult> results = manager.ProcessTick();

            Assert.AreEqual(0, results.OfType<BlockadeChangedResult>().Count());
        }

        [Test]
        public void ProcessTick_BlockadeEnds_EmitsBlockadeCleared()
        {
            (GameRoot game, Planet planet, _) = BuildScene();
            BlockadeSystem manager = new BlockadeSystem(game, new StubRNG());

            manager.ProcessTick();

            // Defender arrives, breaking the blockade
            Fleet defenderFleet = new Fleet
            {
                InstanceID = "f2",
                DisplayName = "Imperial Fleet",
                OwnerInstanceID = "empire",
            };
            game.AttachNode(defenderFleet, planet);

            List<GameResult> results = manager.ProcessTick();

            BlockadeChangedResult result = results.OfType<BlockadeChangedResult>().FirstOrDefault();
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Blockaded);
            Assert.AreEqual(planet, result.Planet);
        }

        [Test]
        public void ProcessTick_NeverBlockaded_NoEndEvent()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            PlanetSystem system = new PlanetSystem { InstanceID = "s1" };
            Planet planet = new Planet { InstanceID = "p1", OwnerInstanceID = "empire" };
            game.Factions.Add(empire);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);

            BlockadeSystem manager = new BlockadeSystem(game, new StubRNG());
            List<GameResult> results = manager.ProcessTick();

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_NewBlockade_InTransitDefendersSurvive()
        {
            (GameRoot game, Planet planet, _) = BuildScene();
            Regiment inTransit = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 },
            };
            game.AttachNode(inTransit, planet);

            BlockadeSystem manager = new BlockadeSystem(game, new StubRNG());
            manager.ProcessTick();

            Assert.IsNotNull(
                game.GetSceneNodeByInstanceID<Regiment>("r1"),
                "In-transit defenders should NOT be destroyed on blockade start"
            );
        }

        [Test]
        public void ProcessTick_MultiplePlanets_HandledIndependently()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem sys1 = new PlanetSystem { InstanceID = "s1" };
            PlanetSystem sys2 = new PlanetSystem { InstanceID = "s2" };
            Planet blockaded = new Planet { InstanceID = "p1", OwnerInstanceID = "empire" };
            Planet safe = new Planet { InstanceID = "p2", OwnerInstanceID = "empire" };
            Fleet hostile = new Fleet { InstanceID = "f1", OwnerInstanceID = "alliance" };
            Fleet defender = new Fleet { InstanceID = "f2", OwnerInstanceID = "empire" };

            game.AttachNode(sys1, game.GetGalaxyMap());
            game.AttachNode(sys2, game.GetGalaxyMap());
            game.AttachNode(blockaded, sys1);
            game.AttachNode(safe, sys2);
            game.AttachNode(hostile, blockaded);
            game.AttachNode(defender, safe);

            BlockadeSystem manager = new BlockadeSystem(game, new StubRNG());
            List<GameResult> results = manager.ProcessTick();

            Assert.AreEqual(1, results.OfType<BlockadeChangedResult>().Count());
            Assert.AreEqual(blockaded, results.OfType<BlockadeChangedResult>().First().Planet);
        }

        [Test]
        public void RollEvacuationLoss_RollBelowThreshold_ReturnsTrue()
        {
            GameConfig config = TestConfig.Create();
            config.Blockade.EvacuationLossPercent = 25;
            GameRoot game = new GameRoot(config);

            // FixedRNG returns 0 from NextInt → 0 < 25 → loss
            BlockadeSystem system = new BlockadeSystem(game, new FixedRNG());

            Assert.IsTrue(system.RollEvacuationLoss());
        }

        [Test]
        public void RollEvacuationLoss_RollAboveThreshold_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            config.Blockade.EvacuationLossPercent = 25;
            GameRoot game = new GameRoot(config);

            // MaxRNG returns 99 from NextInt(0,100) → 99 >= 25 → survives
            BlockadeSystem system = new BlockadeSystem(game, new MaxRNG());

            Assert.IsFalse(system.RollEvacuationLoss());
        }

        [Test]
        public void RollEvacuationLoss_ZeroPercent_NeverDestroys()
        {
            GameConfig config = TestConfig.Create();
            config.Blockade.EvacuationLossPercent = 0;
            GameRoot game = new GameRoot(config);

            BlockadeSystem system = new BlockadeSystem(game, new FixedRNG());

            Assert.IsFalse(system.RollEvacuationLoss());
        }

        [Test]
        public void RollEvacuationLoss_HundredPercent_AlwaysDestroys()
        {
            GameConfig config = TestConfig.Create();
            config.Blockade.EvacuationLossPercent = 100;
            GameRoot game = new GameRoot(config);

            BlockadeSystem system = new BlockadeSystem(game, new MaxRNG());

            Assert.IsTrue(system.RollEvacuationLoss());
        }
    }
}
