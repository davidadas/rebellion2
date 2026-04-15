using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class ResourceRebalanceSystemTests
    {
        private GameRoot CreateGame()
        {
            GameConfig config = TestConfig.Create();
            // Use short timer for tests so rebalance fires immediately
            config.ResourceRebalance.RebalanceTimerBase = 0;
            config.ResourceRebalance.RebalanceTimerSpread = 1;
            config.ResourceRebalance.ResourceWalkTimerBase = 0;
            config.ResourceRebalance.ResourceWalkTimerSpread = 1;
            return new GameRoot(config);
        }

        private Planet CreatePlanet(string id, string ownerID, int rawResources, int energy)
        {
            return new Planet
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = ownerID,
                IsColonized = true,
                NumRawResourceNodes = rawResources,
                EnergyCapacity = energy,
            };
        }

        [Test]
        public void ApplyResourceDecay_FactionWithResources_ReducesValues()
        {
            GameRoot game = CreateGame();
            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "empire", 10, 10);
            game.AttachNode(sys, game.GetGalaxyMap());
            game.AttachNode(planet, sys);

            // Use StubRNG which returns min (0) — all probability checks succeed
            StubRNG rng = new StubRNG();
            ResourceRebalanceSystem system = new ResourceRebalanceSystem(game, rng);

            game.CurrentTick = 1;
            system.ProcessTick();

            Assert.Less(planet.NumRawResourceNodes, 10);
            Assert.Less(planet.EnergyCapacity, 10);
        }

        [Test]
        public void ApplyResourceDecay_ResourcesBelowEnergy_ClampsRawMaterialsToEnergy()
        {
            GameRoot game = CreateGame();
            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "empire", 10, 3);
            game.AttachNode(sys, game.GetGalaxyMap());
            game.AttachNode(planet, sys);

            StubRNG rng = new StubRNG();
            ResourceRebalanceSystem system = new ResourceRebalanceSystem(game, rng);

            game.CurrentTick = 1;
            system.ProcessTick();

            Assert.LessOrEqual(planet.NumRawResourceNodes, planet.EnergyCapacity);
        }

        [Test]
        public void ApplyResourceDecay_FactionWithFullResources_GuaranteesAtLeastOneLoss()
        {
            GameConfig config = TestConfig.Create();
            config.ResourceRebalance.RebalanceTimerBase = 0;
            config.ResourceRebalance.RebalanceTimerSpread = 1;
            config.ResourceRebalance.ResourceWalkTimerBase = 999; // Don't fire walk
            config.ResourceRebalance.ResourceWalkTimerSpread = 1;
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "empire", 1, 1);
            game.AttachNode(sys, game.GetGalaxyMap());
            game.AttachNode(planet, sys);

            // HighRNG always returns 99 for NextInt — probability checks fail (99 >= any prob)
            HighRNG rng = new HighRNG();
            ResourceRebalanceSystem system = new ResourceRebalanceSystem(game, rng);

            game.CurrentTick = 1;
            system.ProcessTick();

            int totalRemaining = planet.NumRawResourceNodes + planet.EnergyCapacity;
            Assert.Less(
                totalRemaining,
                2,
                "At least 1 unit should be lost even with all rolls failing."
            );
        }

        [Test]
        public void ResourceWalk_FactionWithEnergy_CanIncrementEnergy()
        {
            GameConfig config = TestConfig.Create();
            config.ResourceRebalance.RebalanceTimerBase = 999; // Don't fire rebalance
            config.ResourceRebalance.RebalanceTimerSpread = 1;
            config.ResourceRebalance.ResourceWalkTimerBase = 0;
            config.ResourceRebalance.ResourceWalkTimerSpread = 1;
            GameRoot game = new GameRoot(config);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "empire", 5, 5);
            game.AttachNode(sys, game.GetGalaxyMap());
            game.AttachNode(planet, sys);

            // HighRNG returns max-1: for NextInt(0,100) returns 99 → case 4 (increment energy)
            HighRNG rng = new HighRNG();
            ResourceRebalanceSystem system = new ResourceRebalanceSystem(game, rng);

            game.CurrentTick = 1000; // Past the walk timer
            system.ProcessTick();

            Assert.AreEqual(6, planet.EnergyCapacity, "Energy should have been incremented by 1.");
        }

        [Test]
        public void ResourceWalk_FactionAtMaxEnergy_EnergyCannotExceedMax()
        {
            GameConfig config = TestConfig.Create();
            config.ResourceRebalance.RebalanceTimerBase = 999;
            config.ResourceRebalance.RebalanceTimerSpread = 1;
            config.ResourceRebalance.ResourceWalkTimerBase = 0;
            config.ResourceRebalance.ResourceWalkTimerSpread = 1;
            config.ResourceRebalance.MaxEnergy = 15;
            GameRoot game = new GameRoot(config);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "empire", 5, 15); // Already at max
            game.AttachNode(sys, game.GetGalaxyMap());
            game.AttachNode(planet, sys);

            HighRNG rng = new HighRNG();
            ResourceRebalanceSystem system = new ResourceRebalanceSystem(game, rng);

            game.CurrentTick = 1000;
            system.ProcessTick();

            Assert.AreEqual(15, planet.EnergyCapacity);
        }

        /// <summary>
        /// RNG that always returns max-1 (highest possible value).
        /// Makes probability checks fail (roll >= probability) and
        /// selects last entries in tables.
        /// </summary>
        private class HighRNG : IRandomNumberProvider
        {
            public int NextInt(int min, int max) => max - 1;

            public double NextDouble() => 0.99;
        }
    }
}
