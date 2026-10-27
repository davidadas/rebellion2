using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class BlockadeTransitionResolverTests
    {
        [Test]
        public void Resolve_NewBlockade_ReturnsStartedResult()
        {
            (GameRoot game, Planet planet) = BuildScene();
            BlockadeTransitionResolver resolver = new BlockadeTransitionResolver(game);
            bool wasBlockaded = planet.IsBlockaded();
            Fleet fleet = AttachOperationalFleet(game, planet, "alliance");

            BlockadeChangedResult result = resolver.Resolve(planet, wasBlockaded);

            Assert.AreSame(planet, result.Planet);
            Assert.AreSame(fleet, result.BlockadingFleet);
            Assert.IsTrue(result.Blockaded);
            Assert.AreEqual(game.CurrentTick, result.Tick);
        }

        [Test]
        public void Resolve_ExistingBlockade_ReturnsNull()
        {
            (GameRoot game, Planet planet) = BuildScene();
            BlockadeTransitionResolver resolver = new BlockadeTransitionResolver(game);
            AttachOperationalFleet(game, planet, "alliance");

            BlockadeChangedResult result = resolver.Resolve(planet, wasBlockaded: true);

            Assert.IsNull(result);
        }

        [Test]
        public void Resolve_UnblockadedPlanet_ReturnsNull()
        {
            (GameRoot game, Planet planet) = BuildScene();
            BlockadeTransitionResolver resolver = new BlockadeTransitionResolver(game);

            BlockadeChangedResult result = resolver.Resolve(planet, wasBlockaded: false);

            Assert.IsNull(result);
        }

        [Test]
        public void Resolve_EndedBlockade_ReturnsEndedResult()
        {
            (GameRoot game, Planet planet) = BuildScene();
            BlockadeTransitionResolver resolver = new BlockadeTransitionResolver(game);
            Fleet fleet = AttachOperationalFleet(game, planet, "alliance");
            bool wasBlockaded = planet.IsBlockaded();
            fleet.Movement = new MovementState { TransitTicks = 1 };

            BlockadeChangedResult result = resolver.Resolve(planet, wasBlockaded);

            Assert.AreSame(planet, result.Planet);
            Assert.IsNull(result.BlockadingFleet);
            Assert.IsFalse(result.Blockaded);
            Assert.AreEqual(game.CurrentTick, result.Tick);
        }

        /// <summary>
        /// Builds an owned planet with no fleets in orbit.
        /// </summary>
        /// <returns>The game and planet.</returns>
        private static (GameRoot game, Planet planet) BuildScene()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "alliance" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "empire" };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            return (game, planet);
        }

        /// <summary>
        /// Adds a stationary fleet containing one completed capital ship.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="planet">The fleet's planet.</param>
        /// <param name="ownerInstanceId">The fleet owner's identifier.</param>
        /// <returns>The attached fleet.</returns>
        private static Fleet AttachOperationalFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId
        )
        {
            Fleet fleet = EntityFactory.CreateFleet($"{ownerInstanceId}-fleet", ownerInstanceId);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"{ownerInstanceId}-ship",
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return fleet;
        }
    }
}
