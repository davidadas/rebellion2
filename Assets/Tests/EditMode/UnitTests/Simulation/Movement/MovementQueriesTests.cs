using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class MovementQueriesTests
    {
        /// <summary>
        /// Verifies valid destination does not move unit.
        /// </summary>
        [Test]
        public void TryGetTransitTicks_ValidDestination_DoesNotMoveUnit()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementQueries movement
            ) = BuildScene();

            bool result = movement.TryGetTransitTicks(
                new List<IMovable> { officer },
                destination,
                out int transitTicks
            );

            Assert.IsTrue(result);
            Assert.Greater(transitTicks, 0);
            Assert.AreEqual(origin, officer.GetParent());
            Assert.IsNull(officer.Movement);
        }

        /// <summary>
        /// Verifies fleet with unfinished slower ship ignores unfinished ship.
        /// </summary>
        [Test]
        public void TryGetTransitTicks_FleetWithUnfinishedSlowerShip_IgnoresUnfinishedShip()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer _,
                MovementQueries movement
            ) = BuildScene();
            Fleet fleet = EntityFactory.CreateFleet("mixed-fleet", "empire");
            game.AttachNode(fleet, origin);
            CapitalShip completedShip = CreateMovableCapitalShip("completed-ship");
            completedShip.Hyperdrive = 10;
            game.AttachNode(completedShip, fleet);
            Assert.IsTrue(
                movement.TryGetTransitTicks(
                    new List<IMovable> { fleet },
                    destination,
                    out int completedOnlyTicks
                )
            );
            CapitalShip unfinishedShip = new CapitalShip
            {
                InstanceID = "unfinished-ship",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(unfinishedShip, fleet);

            bool estimated = movement.TryGetTransitTicks(
                new List<IMovable> { fleet },
                destination,
                out int mixedFleetTicks
            );

            Assert.IsTrue(estimated);
            Assert.AreEqual(completedOnlyTicks, mixedFleetTicks);
            Assert.IsNull(fleet.Movement);
        }

        /// <summary>
        /// Verifies valid destination does not assign movement.
        /// </summary>
        [Test]
        public void TryEstimateManufacturedTransitTicks_ValidDestination_DoesNotAssignMovement()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementQueries movement
            ) = BuildScene();

            bool result = movement.TryEstimateManufacturedTransitTicks(
                officer,
                origin,
                destination,
                out int transitTicks
            );

            Assert.IsTrue(result);
            Assert.Greater(transitTicks, 0);
            Assert.IsNull(officer.Movement);
        }

        /// <summary>
        /// Verifies view fleet destination uses live fleet location.
        /// </summary>
        [Test]
        public void TryEstimateManufacturedTransitTicks_ViewFleetDestination_UsesLiveFleetLocation()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementQueries movement
            ) = BuildScene();

            Fleet liveFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(liveFleet, destination);

            Fleet viewFleet = EntityFactory.CreateFleet(liveFleet.InstanceID, "empire");

            bool result = movement.TryEstimateManufacturedTransitTicks(
                officer,
                origin,
                viewFleet,
                out int transitTicks
            );

            Assert.IsTrue(result);
            Assert.Greater(transitTicks, 0);
            Assert.IsNull(officer.Movement);
        }

        /// <summary>
        /// Verifies hostile planet destination returns false.
        /// </summary>
        [Test]
        public void TryEstimateManufacturedTransitTicks_HostilePlanetDestination_ReturnsFalse()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementQueries movement
            ) = BuildScene();
            destination.OwnerInstanceID = "rebels";

            bool result = movement.TryEstimateManufacturedTransitTicks(
                officer,
                origin,
                destination,
                out int transitTicks
            );

            Assert.IsFalse(result);
            Assert.AreEqual(0, transitTicks);
            Assert.IsNull(officer.Movement);
        }

        /// <summary>
        /// Verifies starfighter to enemy blockaded planet returns false.
        /// </summary>
        [Test]
        public void TryEstimateManufacturedTransitTicks_StarfighterToEnemyBlockadedPlanet_ReturnsFalse()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer _,
                MovementQueries movement
            ) = BuildScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Building;
            AddBlockadingFleet(game, destination);

            bool estimated = movement.TryEstimateManufacturedTransitTicks(
                starfighter,
                origin,
                destination,
                out _
            );

            Assert.IsFalse(estimated);
        }

        /// <summary>
        /// Verifies valid route does not mutate fleet.
        /// </summary>
        [Test]
        public void CanSetFleetWaypointRoute_ValidRoute_DoesNotMutateFleet()
        {
            (
                _,
                Planet origin,
                Planet firstDestination,
                Planet secondDestination,
                Fleet fleet,
                MovementQueries movement
            ) = BuildWaypointScene();

            bool canSetRoute = movement.CanSetFleetWaypointRoute(
                new ISceneNode[] { fleet },
                new[] { firstDestination.InstanceID, secondDestination.InstanceID },
                "empire"
            );

            Assert.IsTrue(canSetRoute);
            Assert.AreSame(origin, fleet.GetParent());
            Assert.IsNull(fleet.Movement);
            Assert.IsEmpty(fleet.Waypoints);
        }

        /// <summary>
        /// Verifies capital ship does not change fleet membership.
        /// </summary>
        [Test]
        public void CanSetFleetWaypointRoute_CapitalShip_DoesNotChangeFleetMembership()
        {
            (
                _,
                _,
                Planet firstDestination,
                Planet secondDestination,
                Fleet fleet,
                MovementQueries movement
            ) = BuildWaypointScene();
            CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();

            bool canSetRoute = movement.CanSetFleetWaypointRoute(
                new ISceneNode[] { ship },
                new[] { firstDestination.InstanceID, secondDestination.InstanceID },
                "empire"
            );

            Assert.IsTrue(canSetRoute);
            Assert.AreSame(fleet, ship.GetParent());
            Assert.IsNull(ship.Movement);
            Assert.IsEmpty(fleet.Waypoints);
        }

        /// <summary>
        /// Verifies starfighter to enemy blockaded planet returns false.
        /// </summary>
        [Test]
        public void TryGetSelectionTransitTicks_StarfighterToEnemyBlockadedPlanet_ReturnsFalse()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer _,
                MovementQueries movement
            ) = BuildScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(starfighter, origin);
            AddBlockadingFleet(game, destination);

            bool estimated = movement.TryGetSelectionTransitTicks(
                new ISceneNode[] { starfighter },
                destination,
                "empire",
                out _
            );

            Assert.IsFalse(estimated);
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <returns>The constructed scene.</returns>
        private (
            GameRoot game,
            Planet origin,
            Planet destination,
            Officer officer,
            MovementQueries movement
        ) BuildScene(GameConfig config = null)
        {
            GameRoot game = new GameRoot(config ?? TestContent.Data.GameConfig);

            Faction empire = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                TypeID = "origin-type",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(origin, sector);

            Planet destination = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 100,
            };
            game.AttachNode(destination, sector);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, origin);

            MovementQueries movement = new MovementQueries(game);

            return (game, origin, destination, officer, movement);
        }

        /// <summary>
        /// Builds waypoint scene.
        /// </summary>
        /// <returns>The constructed waypoint scene.</returns>
        private static (
            GameRoot game,
            Planet origin,
            Planet firstDestination,
            Planet secondDestination,
            Fleet fleet,
            MovementQueries movement
        ) BuildWaypointScene()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            Planet firstDestination = new Planet
            {
                InstanceID = "first-destination",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 50,
                PositionY = 25,
            };
            Planet secondDestination = new Planet
            {
                InstanceID = "second-destination",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 50,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(firstDestination, sector);
            game.AttachNode(secondDestination, sector);

            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = CreateMovableCapitalShip("ship");
            game.AttachNode(fleet, origin);
            game.AttachNode(ship, fleet);
            MovementQueries movement = new MovementQueries(game);
            return (game, origin, firstDestination, secondDestination, fleet, movement);
        }

        /// <summary>
        /// Adds blockading fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="starfighterCapacity">The starfighter capacity.</param>
        /// <returns>The result of add blockading fleet.</returns>
        private static (Fleet fleet, CapitalShip ship) AddBlockadingFleet(
            GameRoot game,
            Planet planet,
            int starfighterCapacity = 0
        )
        {
            Fleet fleet = EntityFactory.CreateFleet($"blockader-{planet.InstanceID}", "rebels");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"blockader-ship-{planet.InstanceID}",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = starfighterCapacity,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return (fleet, ship);
        }

        /// <summary>
        /// Creates movable capital ship.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created movable capital ship.</returns>
        private static CapitalShip CreateMovableCapitalShip(string instanceId)
        {
            return new CapitalShip
            {
                InstanceID = instanceId,
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }
    }
}
