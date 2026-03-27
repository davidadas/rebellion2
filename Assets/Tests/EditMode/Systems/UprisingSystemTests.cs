using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class UprisingSystemTests
    {
        private class MockRNG : IRandomNumberProvider
        {
            private Queue<double> values;

            public MockRNG(params double[] values)
            {
                this.values = new Queue<double>(values);
            }

            public double NextDouble()
            {
                return values.Count > 0 ? values.Dequeue() : 0.5;
            }

            public int NextInt(int min, int max)
            {
                return (int)(NextDouble() * (max - min)) + min;
            }
        }

        [Test]
        public void ProcessTick_LowLoyalty_EmitsUprisingEvent()
        {
            var config = new GameConfig();
            var game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            var system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            var planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 10 }, { "rebels", 50 } },
            };
            game.AttachNode(planet, system);

            var manager = new UprisingSystem(game);
            manager.ProcessTick(new MockRNG(0.01));

            Assert.IsTrue(planet.IsInUprising, "Planet should be in uprising");
            Assert.AreEqual(
                "rebels",
                planet.OwnerInstanceID,
                "Ownership should transfer to rebels"
            );
        }

        [Test]
        public void ProcessTick_HighLoyalty_NoEvent()
        {
            var config = new GameConfig();
            var game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            var planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 80 }, { "rebels", 20 } },
            };
            var system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            var manager = new UprisingSystem(game);
            manager.ProcessTick(new MockRNG(0.01));

            Assert.IsFalse(planet.IsInUprising, "Planet should not be in uprising");
        }

        [Test]
        public void ProcessTick_SameSeed_SameEvents()
        {
            var config1 = new GameConfig();
            var game1 = new GameRoot(config1);
            var planet1 = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 10 }, { "rebels", 50 } },
            };
            game1.GetGalaxyMap().AddChild(planet1);

            var config2 = new GameConfig();
            var game2 = new GameRoot(config2);
            var planet2 = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 10 }, { "rebels", 50 } },
            };
            game2.GetGalaxyMap().AddChild(planet2);

            var manager1 = new UprisingSystem(game1);
            var manager2 = new UprisingSystem(game2);

            manager1.ProcessTick(new SystemRandomProvider(42));
            manager2.ProcessTick(new SystemRandomProvider(42));

            Assert.AreEqual(
                planet1.IsInUprising,
                planet2.IsInUprising,
                "Deterministic RNG should produce same uprising state"
            );
        }

        [Test]
        public void ProcessTick_NoPopulation_NoEvent()
        {
            var config = new GameConfig();
            var game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            var planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int>(),
            };
            var system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            var manager = new UprisingSystem(game);
            manager.ProcessTick(new MockRNG(0.01));

            Assert.IsFalse(planet.IsInUprising, "Planet with no population should not revolt");
        }

        [Test]
        public void ProcessTick_CooldownDoesNotBlockUprisingRoll()
        {
            var config = new GameConfig();
            var game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            var planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 10 }, { "rebels", 50 } },
            };
            var system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);
            game.CurrentTick = 100;

            var manager = new UprisingSystem(game);

            manager.ProcessTick(new MockRNG(0.99));

            Assert.IsFalse(
                planet.IsInUprising,
                "First tick with high roll should not trigger uprising"
            );

            game.CurrentTick = 101;
            manager.ProcessTick(new MockRNG(0.01));

            Assert.IsTrue(planet.IsInUprising, "Uprising roll must not be blocked by cooldown");
        }

        [Test]
        public void SubdueMission_WhenInUprising_ClearsState()
        {
            var config = new GameConfig();
            var game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            var system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            var planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsInUprising = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, system);

            var mission = new SubdueUprisingMission(
                "empire",
                planet,
                new List<IMissionParticipant>(),
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);

            // Directly call OnSuccess to test the uprising clearing logic
            var onSuccessMethod = typeof(SubdueUprisingMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            onSuccessMethod.Invoke(mission, new object[] { game, new MockRNG() });

            Assert.IsFalse(
                planet.IsInUprising,
                "SubdueUprising mission should clear uprising flag"
            );
        }

        [Test]
        public void BeginUprising_Idempotent_NoDuplicateEffects()
        {
            var config = new GameConfig();
            var game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            var system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            var planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, system);

            // Call BeginUprising twice
            planet.BeginUprising("rebels");
            planet.BeginUprising("rebels");

            Assert.AreEqual(
                "rebels",
                planet.OwnerInstanceID,
                "Owner should not change on second call"
            );
            Assert.IsTrue(planet.IsInUprising, "Uprising flag should remain set");
        }

        [Test]
        public void SubdueMission_WhenNotInUprising_Throws()
        {
            var config = new GameConfig();
            var game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            var system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            var planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsInUprising = false,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, system);

            Assert.Throws<InvalidSceneOperationException>(
                () =>
                {
                    new SubdueUprisingMission(
                        "empire",
                        planet,
                        new List<IMissionParticipant>(),
                        new List<IMissionParticipant>()
                    );
                },
                "SubdueUprising mission should throw when planet is not in uprising"
            );
        }
    }
}
