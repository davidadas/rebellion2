using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventTargetTests
    {
        [Test]
        public void Resolve_AuthoredPlanet_ReturnsPlanet()
        {
            GameRoot game = BuildGame(out Planet planet);
            GameEventTarget target = new GameEventTarget
            {
                Planet = new PlanetTarget { InstanceID = planet.InstanceID },
            };

            Planet resolved = target.Resolve(game, new StubRNG()) as Planet;

            Assert.AreSame(planet, resolved);
        }

        [Test]
        public void Resolve_DestroyedAuthoredPlanet_ReturnsNull()
        {
            GameRoot game = BuildGame(out Planet planet);
            planet.IsDestroyed = true;
            GameEventTarget target = new GameEventTarget
            {
                Planet = new PlanetTarget { InstanceID = planet.InstanceID },
            };

            Planet resolved = target.Resolve(game, new StubRNG()) as Planet;

            Assert.IsNull(resolved);
        }

        [Test]
        public void Resolve_RandomPlanetTarget_SelectsEligibleSystemType()
        {
            GameRoot game = BuildGame(out Planet corePlanet);
            PlanetSystem rimSystem = new PlanetSystem
            {
                InstanceID = "rim-system",
                SystemType = PlanetSystemType.RimSystem,
            };
            Planet rimPlanet = new Planet { InstanceID = "rim-planet" };
            game.AttachNode(rimSystem, game.Galaxy);
            game.AttachNode(rimPlanet, rimSystem);
            GameEventTarget target = new GameEventTarget
            {
                RandomPlanets = new RandomPlanetsTarget
                {
                    Count = 1,
                    SystemType = PlanetSystemType.RimSystem,
                },
            };

            Planet resolved = target.Resolve(game, new StubRNG()) as Planet;

            Assert.AreSame(rimPlanet, resolved);
            Assert.AreNotSame(corePlanet, resolved);
        }

        [Test]
        public void Resolve_MultipleSelectors_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet planet);
            GameEventTarget target = new GameEventTarget
            {
                Planet = new PlanetTarget { InstanceID = planet.InstanceID },
                RandomPlanets = new RandomPlanetsTarget(),
            };

            TestDelegate resolve = () => target.Resolve(game, new StubRNG());

            Assert.Throws<InvalidOperationException>(resolve);
        }

        private static GameRoot BuildGame(out Planet planet)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "core-system",
                SystemType = PlanetSystemType.CoreSystem,
            };
            planet = new Planet { InstanceID = "core-planet" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);
            return game;
        }
    }
}
