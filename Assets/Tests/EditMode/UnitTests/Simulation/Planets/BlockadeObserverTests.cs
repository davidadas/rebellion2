using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class BlockadeObserverTests
    {
        [Test]
        public void Publish_NewBlockade_AddsStartedResult()
        {
            (GameRoot game, Planet planet) = BuildScene();
            OrbitalStateNotifications notifications = OrbitalStateNotifications.Create(game);
            bool wasBlockaded = planet.IsBlockaded();
            Fleet fleet = AttachOperationalFleet(game, planet, "alliance");
            List<GameResult> results = new List<GameResult>();

            notifications.Publish(planet, wasBlockaded, results);

            BlockadeChangedResult result = results.OfType<BlockadeChangedResult>().Single();
            Assert.AreSame(planet, result.Planet);
            Assert.AreSame(fleet, result.BlockadingFleet);
            Assert.IsTrue(result.Blockaded);
            Assert.AreEqual(game.CurrentTick, result.Tick);
        }

        [Test]
        public void Publish_ExistingBlockade_AddsNoResult()
        {
            (GameRoot game, Planet planet) = BuildScene();
            OrbitalStateNotifications notifications = OrbitalStateNotifications.Create(game);
            AttachOperationalFleet(game, planet, "alliance");
            List<GameResult> results = new List<GameResult>();

            notifications.Publish(planet, wasBlockaded: true, results);

            Assert.IsEmpty(results);
        }

        [Test]
        public void Publish_UnblockadedPlanet_AddsNoResult()
        {
            (GameRoot game, Planet planet) = BuildScene();
            OrbitalStateNotifications notifications = OrbitalStateNotifications.Create(game);
            List<GameResult> results = new List<GameResult>();

            notifications.Publish(planet, wasBlockaded: false, results);

            Assert.IsEmpty(results);
        }

        [Test]
        public void Publish_EndedBlockade_AddsEndedResult()
        {
            (GameRoot game, Planet planet) = BuildScene();
            OrbitalStateNotifications notifications = OrbitalStateNotifications.Create(game);
            Fleet fleet = AttachOperationalFleet(game, planet, "alliance");
            bool wasBlockaded = planet.IsBlockaded();
            fleet.Movement = new MovementState { TransitTicks = 1 };
            List<GameResult> results = new List<GameResult>();

            notifications.Publish(planet, wasBlockaded, results);

            BlockadeChangedResult result = results.OfType<BlockadeChangedResult>().Single();
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
