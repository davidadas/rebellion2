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
        [Test]
        public void ProcessTick_LowLoyalty_EmitsUprisingEvent()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 10 }, { "rebels", 50 } },
            };
            game.AttachNode(planet, system);

            UprisingSystem manager = new UprisingSystem(game);
            manager.ProcessTick(new QueueRNG(0.01));

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
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 80 }, { "rebels", 20 } },
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            UprisingSystem manager = new UprisingSystem(game);
            manager.ProcessTick(new QueueRNG(0.01));

            Assert.IsFalse(planet.IsInUprising, "Planet should not be in uprising");
        }

        [Test]
        public void ProcessTick_SameSeed_SameEvents()
        {
            GameConfig config1 = new GameConfig();
            GameRoot game1 = new GameRoot(config1);
            Planet planet1 = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 10 }, { "rebels", 50 } },
            };
            game1.GetGalaxyMap().AddChild(planet1);

            GameConfig config2 = new GameConfig();
            GameRoot game2 = new GameRoot(config2);
            Planet planet2 = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 10 }, { "rebels", 50 } },
            };
            game2.GetGalaxyMap().AddChild(planet2);

            UprisingSystem manager1 = new UprisingSystem(game1);
            UprisingSystem manager2 = new UprisingSystem(game2);

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
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int>(),
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            UprisingSystem manager = new UprisingSystem(game);
            manager.ProcessTick(new QueueRNG(0.01));

            Assert.IsFalse(planet.IsInUprising, "Planet with no population should not revolt");
        }

        [Test]
        public void ProcessTick_CooldownDoesNotBlockUprisingRoll()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 10 }, { "rebels", 50 } },
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);
            game.CurrentTick = 100;

            UprisingSystem manager = new UprisingSystem(game);

            manager.ProcessTick(new QueueRNG(0.99));

            Assert.IsFalse(
                planet.IsInUprising,
                "First tick with high roll should not trigger uprising"
            );

            game.CurrentTick = 101;
            manager.ProcessTick(new QueueRNG(0.01));

            Assert.IsTrue(planet.IsInUprising, "Uprising roll must not be blocked by cooldown");
        }

        [Test]
        public void SubdueMission_WhenInUprising_ClearsState()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsInUprising = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, system);

            SubdueUprisingMission mission = new SubdueUprisingMission(
                "empire",
                planet,
                new List<IMissionParticipant>(),
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, planet);

            // Directly call OnSuccess to test the uprising clearing logic
            System.Reflection.MethodInfo onSuccessMethod = typeof(SubdueUprisingMission).GetMethod(
                "OnSuccess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            onSuccessMethod.Invoke(mission, new object[] { game });

            Assert.IsFalse(
                planet.IsInUprising,
                "SubdueUprising mission should clear uprising flag"
            );
        }

        [Test]
        public void BeginUprising_Idempotent_NoDuplicateEffects()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, system);

            game.ChangeUnitOwnership(planet, "rebels");
            planet.BeginUprising();
            planet.BeginUprising(); // Calling twice should be idempotent

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
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsInUprising = false,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, system);

            Assert.Throws<InvalidOperationException>(
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
