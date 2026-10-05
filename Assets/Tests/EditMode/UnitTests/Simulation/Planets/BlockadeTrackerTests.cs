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
    /// <summary>
    /// Tests for BlockadeTracker.
    /// Tests transition detection for blockade starts and endings.
    /// Does NOT test blockade detection logic (that's Planet.IsBlockaded(), tested in PlanetTests).
    /// </summary>
    [TestFixture]
    public class BlockadeTrackerTests
    {
        [Test]
        public void ProcessTick_StartedAndEndedBlockades_ReportsStartBeforeEnd()
        {
            (GameRoot game, Planet firstPlanet, Fleet fleet) = BuildScene();
            BlockadeTracker system = new BlockadeTracker(game);
            new BlockadeTickProcessor(system).ProcessTick(game);
            Planet secondPlanet = new Planet { InstanceID = "p2", OwnerInstanceID = "empire" };
            game.AttachNode(secondPlanet, firstPlanet.GetParent());
            game.MoveNode(fleet, secondPlanet);
            game.CurrentTick = 42;

            BlockadeChangedResult[] results = new BlockadeTickProcessor(system)
                .ProcessTick(game)
                .Cast<BlockadeChangedResult>()
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { secondPlanet, firstPlanet },
                results.Select(result => result.Planet)
            );
            CollectionAssert.AreEqual(
                new[] { true, false },
                results.Select(result => result.Blockaded)
            );
            CollectionAssert.AreEqual(new[] { 42, 42 }, results.Select(result => result.Tick));
        }

        [Test]
        public void ProcessTick_NewBlockade_EmitsBlockadeStarted()
        {
            (GameRoot game, Planet planet, Fleet hostileFleet) = BuildScene();
            BlockadeTracker manager = new BlockadeTracker(game);

            IReadOnlyList<GameResult> results = new BlockadeTickProcessor(manager).ProcessTick(
                game
            );

            BlockadeChangedResult result = results.OfType<BlockadeChangedResult>().FirstOrDefault();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Blockaded);
            Assert.AreEqual(planet, result.Planet);
            Assert.AreEqual(hostileFleet, result.BlockadingFleet);
        }

        [Test]
        public void ProcessTick_NewNeutralPlanetBlockade_EmitsBlockadeStarted()
        {
            (GameRoot game, Planet planet, Fleet blockadingFleet) = BuildScene();
            planet.OwnerInstanceID = null;
            BlockadeTracker manager = new BlockadeTracker(game);

            BlockadeChangedResult result = new BlockadeTickProcessor(manager)
                .ProcessTick(game)
                .OfType<BlockadeChangedResult>()
                .Single();

            Assert.IsTrue(result.Blockaded);
            Assert.AreEqual(planet, result.Planet);
            Assert.AreEqual(blockadingFleet, result.BlockadingFleet);
        }

        [Test]
        public void ProcessTick_HostileFleetInTransit_EmitsBlockadeOnlyAfterArrival()
        {
            (GameRoot game, _, Fleet hostileFleet) = BuildScene();
            hostileFleet.Movement = new MovementState { TransitTicks = 10 };
            BlockadeTracker manager = new BlockadeTracker(game);

            IReadOnlyList<GameResult> inTransitResults = new BlockadeTickProcessor(
                manager
            ).ProcessTick(game);
            hostileFleet.Movement = null;
            IReadOnlyList<GameResult> arrivalResults = new BlockadeTickProcessor(
                manager
            ).ProcessTick(game);

            Assert.IsFalse(inTransitResults.OfType<BlockadeChangedResult>().Any());
            BlockadeChangedResult result = arrivalResults.OfType<BlockadeChangedResult>().Single();
            Assert.IsTrue(result.Blockaded);
            Assert.AreEqual(hostileFleet, result.BlockadingFleet);
        }

        [Test]
        public void ProcessTick_AlreadyBlockaded_NoRepeatedEvent()
        {
            (GameRoot game, _, _) = BuildScene();
            BlockadeTracker manager = new BlockadeTracker(game);

            new BlockadeTickProcessor(manager).ProcessTick(game);
            IReadOnlyList<GameResult> results = new BlockadeTickProcessor(manager).ProcessTick(
                game
            );

            Assert.AreEqual(0, results.OfType<BlockadeChangedResult>().Count());
        }

        [Test]
        public void ProcessTick_BlockadeEnds_EmitsBlockadeCleared()
        {
            (GameRoot game, Planet planet, _) = BuildScene();
            BlockadeTracker manager = new BlockadeTracker(game);

            new BlockadeTickProcessor(manager).ProcessTick(game);

            // Defender arrives, breaking the blockade
            Fleet defenderFleet = new Fleet
            {
                InstanceID = "f2",
                DisplayName = "Imperial Fleet",
                OwnerInstanceID = "empire",
            };
            game.AttachNode(defenderFleet, planet);
            AttachOperationalCapitalShip(game, defenderFleet, "defender-ship");

            IReadOnlyList<GameResult> results = new BlockadeTickProcessor(manager).ProcessTick(
                game
            );

            BlockadeChangedResult result = results.OfType<BlockadeChangedResult>().FirstOrDefault();
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Blockaded);
            Assert.AreEqual(planet, result.Planet);
        }

        [Test]
        public void ProcessTick_NeverBlockaded_NoEndEvent()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            PlanetSector sector = new PlanetSector { InstanceID = "s1" };
            Planet planet = new Planet { InstanceID = "p1", OwnerInstanceID = "empire" };
            game.GetFactions().Add(empire);
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);

            BlockadeTracker manager = new BlockadeTracker(game);
            IReadOnlyList<GameResult> results = new BlockadeTickProcessor(manager).ProcessTick(
                game
            );

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

            BlockadeTracker manager = new BlockadeTracker(game);
            new BlockadeTickProcessor(manager).ProcessTick(game);

            Assert.IsNotNull(
                game.GetSceneNodeByInstanceID<Regiment>("r1"),
                "In-transit defenders should NOT be destroyed on blockade start"
            );
        }

        [Test]
        public void ProcessTick_MultiplePlanets_HandledIndependently()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);

            PlanetSector sector1 = new PlanetSector { InstanceID = "s1" };
            PlanetSector sector2 = new PlanetSector { InstanceID = "s2" };
            Planet blockaded = new Planet { InstanceID = "p1", OwnerInstanceID = "empire" };
            Planet safe = new Planet { InstanceID = "p2", OwnerInstanceID = "empire" };
            Fleet hostile = new Fleet { InstanceID = "f1", OwnerInstanceID = "alliance" };
            Fleet defender = new Fleet { InstanceID = "f2", OwnerInstanceID = "empire" };

            game.AttachNode(sector1, game.GetGalaxyMap());
            game.AttachNode(sector2, game.GetGalaxyMap());
            game.AttachNode(blockaded, sector1);
            game.AttachNode(safe, sector2);
            game.AttachNode(hostile, blockaded);
            game.AttachNode(defender, safe);
            AttachOperationalCapitalShip(game, hostile, "hostile-ship");
            AttachOperationalCapitalShip(game, defender, "defender-ship");

            BlockadeTracker manager = new BlockadeTracker(game);
            IReadOnlyList<GameResult> results = new BlockadeTickProcessor(manager).ProcessTick(
                game
            );

            Assert.AreEqual(1, results.OfType<BlockadeChangedResult>().Count());
            Assert.AreEqual(blockaded, results.OfType<BlockadeChangedResult>().First().Planet);
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <returns>The constructed scene.</returns>
        private (GameRoot game, Planet planet, Fleet hostileFleet) BuildScene()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSector sector = new PlanetSector
            {
                InstanceID = "s1",
                DisplayName = "Test Sector",
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

            game.GetFactions().Add(empire);
            game.GetFactions().Add(alliance);
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            game.AttachNode(hostileFleet, planet);
            AttachOperationalCapitalShip(game, hostileFleet, "hostile-ship");

            return (game, planet, hostileFleet);
        }

        /// <summary>
        /// Attaches operational capital ship.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="fleet">The fleet.</param>
        /// <param name="id">The id.</param>
        private static void AttachOperationalCapitalShip(GameRoot game, Fleet fleet, string id)
        {
            game.AttachNode(
                new CapitalShip
                {
                    InstanceID = id,
                    OwnerInstanceID = fleet.OwnerInstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                fleet
            );
        }
    }
}
