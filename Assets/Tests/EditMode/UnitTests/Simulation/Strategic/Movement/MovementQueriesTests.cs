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

        [Test]
        public void TryGetTransitTicks_FleetWithUnfinishedSlowerShip_IgnoresUnfinishedShip()
        {
            GameConfig config = new GameConfig
            {
                Movement = new GameConfig.MovementConfig
                {
                    DistanceDivisor = 5,
                    MinTransitTicks = 1,
                    SameSectorMinTransitTicks = 1,
                    DefaultFighterHyperdrive = 60,
                    DefaultPersonnelHyperdrive = 100,
                },
            };
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer _,
                MovementQueries movement
            ) = BuildScene(config);
            Fleet fleet = EntityFactory.CreateFleet("mixed-fleet", "empire");
            game.AttachNode(fleet, origin);
            CapitalShip completedShip = CreateMovableCapitalShip("completed-ship");
            completedShip.Hyperdrive = 80;
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
                Hyperdrive = 100,
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

        [Test]
        public void CalculateTransitTicks_HigherHyperdriveRating_ReturnsLongerDuration()
        {
            GameConfig config = new GameConfig
            {
                Movement = new GameConfig.MovementConfig
                {
                    DistanceDivisor = 5,
                    MinTransitTicks = 1,
                    SameSectorMinTransitTicks = 1,
                    DefaultFighterHyperdrive = 60,
                    DefaultPersonnelHyperdrive = 100,
                },
            };
            (_, Planet origin, Planet destination, _, MovementQueries movement) = BuildScene(
                config
            );
            destination.PositionX = 100;
            destination.PositionY = 0;
            CapitalShip fasterShip = CreateMovableCapitalShip("faster-ship");
            fasterShip.Hyperdrive = 80;
            CapitalShip slowerShip = CreateMovableCapitalShip("slower-ship");
            slowerShip.Hyperdrive = 100;

            int fasterTransitTicks = movement.CalculateTransitTicks(
                fasterShip,
                origin,
                destination
            );
            int slowerTransitTicks = movement.CalculateTransitTicks(
                slowerShip,
                origin,
                destination
            );

            Assert.AreEqual(16, fasterTransitTicks);
            Assert.AreEqual(20, slowerTransitTicks);
        }

        [Test]
        public void CalculateTransitTicks_Officer_UsesConfiguredOfficerHyperdrive()
        {
            GameConfig config = new GameConfig
            {
                Movement = new GameConfig.MovementConfig
                {
                    DistanceDivisor = 5,
                    MinTransitTicks = 1,
                    SameSectorMinTransitTicks = 1,
                    DefaultFighterHyperdrive = 60,
                    DefaultPersonnelHyperdrive = 100,
                },
            };
            (_, Planet origin, Planet destination, Officer officer, MovementQueries movement) =
                BuildScene(config);
            destination.PositionX = 100;
            destination.PositionY = 0;

            int transitTicks = movement.CalculateTransitTicks(officer, origin, destination);

            Assert.AreEqual(20, transitTicks);
        }

        [Test]
        public void CalculateTransitTicks_SpecialForces_UsesConfiguredOfficerHyperdrive()
        {
            GameConfig config = new GameConfig
            {
                Movement = new GameConfig.MovementConfig
                {
                    DistanceDivisor = 5,
                    MinTransitTicks = 1,
                    SameSectorMinTransitTicks = 1,
                    DefaultFighterHyperdrive = 50,
                    DefaultPersonnelHyperdrive = 100,
                },
            };
            (GameRoot game, Planet origin, Planet destination, _, MovementQueries movement) =
                BuildScene(config);
            destination.PositionX = 100;
            destination.PositionY = 0;
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "special-forces",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(specialForces, origin);

            int transitTicks = movement.CalculateTransitTicks(specialForces, origin, destination);

            Assert.AreEqual(20, transitTicks);
        }

        [Test]
        public void CalculateTransitTicks_ZeroDistanceDivisor_ThrowsInvalidOperationException()
        {
            GameConfig config = new GameConfig
            {
                Movement = new GameConfig.MovementConfig
                {
                    DistanceDivisor = 0,
                    MinTransitTicks = 1,
                    SameSectorMinTransitTicks = 1,
                    DefaultFighterHyperdrive = 60,
                    DefaultPersonnelHyperdrive = 100,
                },
            };
            (_, Planet origin, Planet destination, _, MovementQueries movement) = BuildScene(
                config
            );
            CapitalShip ship = CreateMovableCapitalShip("ship");
            ship.Hyperdrive = 80;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                movement.CalculateTransitTicks(ship, origin, destination)
            );

            Assert.AreEqual(
                "Movement distance divisor must be greater than zero.",
                exception.Message
            );
        }

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

        [Test]
        public void GetPersonnelEncounterOdds_HostileDetector_ReturnsCompleteProjection()
        {
            GameConfig config = TestConfig.Create();
            config.ProbabilityTables.Mission.Foil = new Dictionary<int, int> { { -1000, 73 } };
            config.ProbabilityTables.Mission.Evasion = new Dictionary<int, int> { { -1000, 41 } };
            (
                GameRoot game,
                Planet _,
                Planet destination,
                Officer officer,
                MovementQueries movement
            ) = BuildScene(config);
            (_, CapitalShip ship) = AddBlockadingFleet(game, destination, starfighterCapacity: 1);
            Starfighter detector = EntityFactory.CreateStarfighter("detector", "rebels");
            detector.DetectionRating = 100;
            detector.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(detector, ship);

            PersonnelMovementEncounterOdds odds = movement.GetPersonnelEncounterOdds(
                new IMissionParticipant[] { officer },
                destination
            );

            CollectionAssert.AreEqual(
                new ISceneNode[] { ship, detector },
                odds.Detectors.Select(odds => odds.Detector)
            );
            PersonnelMovementDetectorOdds detectorOdds = odds.Detectors.Single(odds =>
                odds.Detector == detector
            );
            Assert.AreSame(detector, detectorOdds.Detector);
            Assert.AreEqual(73, detectorOdds.DetectionProbability);
            Assert.AreEqual(41, detectorOdds.GetEvasionProbability(officer));
        }

        [Test]
        public void GetPersonnelEncounterOdds_MixedDefenders_UsesOnlyCapitalShipsAndCarriedFighters()
        {
            (
                GameRoot game,
                Planet _,
                Planet destination,
                Officer officer,
                MovementQueries movement
            ) = BuildScene();
            destination.OwnerInstanceID = "rebels";
            (_, CapitalShip ship) = AddBlockadingFleet(game, destination, starfighterCapacity: 1);
            Starfighter carriedFighter = EntityFactory.CreateStarfighter(
                "carried-fighter",
                "rebels"
            );
            Starfighter groundFighter = EntityFactory.CreateStarfighter("ground-fighter", "rebels");
            Regiment carriedRegiment = EntityFactory.CreateRegiment("carried-regiment", "rebels");
            Regiment groundRegiment = EntityFactory.CreateRegiment("ground-regiment", "rebels");
            carriedFighter.ManufacturingStatus = ManufacturingStatus.Complete;
            groundFighter.ManufacturingStatus = ManufacturingStatus.Complete;
            carriedRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            groundRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            ship.RegimentCapacity = 1;
            game.AttachNode(carriedFighter, ship);
            game.AttachNode(carriedRegiment, ship);
            game.AttachNode(groundFighter, destination);
            game.AttachNode(groundRegiment, destination);

            PersonnelMovementEncounterOdds odds = movement.GetPersonnelEncounterOdds(
                new IMissionParticipant[] { officer },
                destination
            );

            CollectionAssert.AreEqual(
                new ISceneNode[] { ship, carriedFighter },
                odds.Detectors.Select(entry => entry.Detector)
            );
        }

        [Test]
        public void GetPersonnelEncounterOdds_HostileFleetForceUser_ReturnsOnlyShipDetector()
        {
            GameConfig config = TestConfig.Create();
            config.Jedi.MissionParticipantEncounterMinimum = 1;
            config.Jedi.MissionDefenderEncounterMinimum = 1;
            config.Jedi.EncounterProbabilityOffset = -10;
            (
                GameRoot game,
                Planet _,
                Planet destination,
                Officer officer,
                MovementQueries movement
            ) = BuildScene(config);
            officer.ForceValue = 20;
            (_, CapitalShip ship) = AddBlockadingFleet(game, destination);
            Officer defender = EntityFactory.CreateOfficer("force-defender", "rebels");
            defender.ForceValue = 40;
            game.AttachNode(defender, ship);

            PersonnelMovementEncounterOdds odds = movement.GetPersonnelEncounterOdds(
                new IMissionParticipant[] { officer },
                destination
            );

            Assert.AreSame(ship, odds.Detectors.Single().Detector);
        }

        [Test]
        public void GetPersonnelEncounterOdds_CompletedDetectionBlocker_ReturnsNoDetectors()
        {
            (
                GameRoot game,
                Planet _,
                Planet destination,
                Officer officer,
                MovementQueries movement
            ) = BuildScene(TestConfig.Create());
            (_, CapitalShip ship) = AddBlockadingFleet(game, destination, starfighterCapacity: 1);
            Starfighter detector = EntityFactory.CreateStarfighter("detector", "rebels");
            detector.DetectionRating = 100;
            detector.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(detector, ship);
            destination.EnergyCapacity = 1;
            Building blocker = new Building
            {
                InstanceID = "detection-blocker",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                IsDetectionBlocker = true,
            };
            game.AttachNode(blocker, destination);
            officer.ForceValue = 100;
            Officer defender = EntityFactory.CreateOfficer("force-defender", "rebels");
            defender.ForceValue = 100;
            game.AttachNode(defender, ship);

            PersonnelMovementEncounterOdds odds = movement.GetPersonnelEncounterOdds(
                new IMissionParticipant[] { officer },
                destination
            );

            Assert.IsEmpty(odds.Detectors);
        }

        [Test]
        public void FindSafeRelocationDestinations_InTransitUnit_RanksFromCurrentPosition()
        {
            (
                GameRoot game,
                Planet excludedOrigin,
                Planet assignedDestination,
                Officer officer,
                MovementQueries movement
            ) = BuildScene();
            excludedOrigin.OwnerInstanceID = "rebels";
            Planet livePositionNearest = new Planet
            {
                InstanceID = "live-position-nearest",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 20,
                PositionY = 0,
            };
            Planet assignedDestinationNearest = new Planet
            {
                InstanceID = "assigned-destination-nearest",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 90,
                PositionY = 100,
            };
            game.AttachNode(livePositionNearest, assignedDestination.GetParent());
            game.AttachNode(assignedDestinationNearest, assignedDestination.GetParent());
            game.MoveNode(officer, assignedDestination);
            officer.Movement = new MovementState
            {
                OriginPosition = excludedOrigin.GetPosition(),
                CurrentPosition = new Point(10, 0),
            };

            IReadOnlyList<ContainerNode> destinations = movement.FindSafeRelocationDestinations(
                officer,
                assignedDestination
            );

            Assert.AreSame(livePositionNearest, destinations.First());
        }

        [Test]
        public void CanUseSafeRelocationDestination_AlreadyAtAllowedOrigin_ReturnsTrue()
        {
            (_, Planet origin, _, Officer officer, MovementQueries movement) = BuildScene();

            bool accepted = movement.CanUseSafeRelocationDestination(
                officer,
                origin,
                origin,
                allowOriginPlanet: true
            );

            Assert.IsTrue(accepted);
        }

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

        [Test]
        public void TryGetSelectionTransitTicks_CapitalShipToPlanet_ReturnsTransitTime()
        {
            GameConfig config = CreateMovementConfig();
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer _,
                MovementQueries movement
            ) = BuildScene(config);
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = CreateMovableCapitalShip("ship");
            game.AttachNode(fleet, origin);
            game.AttachNode(ship, fleet);

            bool estimated = movement.TryGetSelectionTransitTicks(
                new ISceneNode[] { ship },
                destination,
                "empire",
                out int transitTicks
            );

            Assert.IsTrue(estimated);
            Assert.Greater(transitTicks, 0);
        }

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

        [Test]
        public void TryGetSelectionTransitTicks_GroupExceedsDestinationCapacity_ReturnsFalse()
        {
            GameConfig config = CreateMovementConfig();
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer _,
                MovementQueries movement
            ) = BuildScene(config);
            origin.EnergyCapacity = 2;
            destination.EnergyCapacity = 1;
            Building firstBuilding = new Building
            {
                InstanceID = "first-building",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            Building secondBuilding = new Building
            {
                InstanceID = "second-building",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(firstBuilding, origin);
            game.AttachNode(secondBuilding, origin);

            bool estimated = movement.TryGetSelectionTransitTicks(
                new ISceneNode[] { firstBuilding, secondBuilding },
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
            GameRoot game = TestGame.Create(config ?? TestContent.Data.GameConfig);

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
        /// Creates synthetic movement configuration for transit-query tests.
        /// </summary>
        /// <returns>The synthetic game configuration.</returns>
        private static GameConfig CreateMovementConfig()
        {
            return new GameConfig
            {
                Movement = new GameConfig.MovementConfig
                {
                    DistanceDivisor = 5,
                    MinTransitTicks = 1,
                    SameSectorMinTransitTicks = 1,
                    DefaultFighterHyperdrive = 60,
                    DefaultPersonnelHyperdrive = 100,
                },
            };
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
            GameRoot game = TestGame.Create(TestConfig.Create());
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
