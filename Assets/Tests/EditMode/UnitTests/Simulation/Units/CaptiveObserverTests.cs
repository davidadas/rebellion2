using System.Collections.Generic;
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
    public class CaptiveObserverTests
    {
        [Test]
        public void HandleResults_DuplicateCaptureInBatch_RetainsFirstObservation()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            system.HandleResults(
                new[] { CaptureResult(captive, planet, 4), CaptureResult(captive, planet, 9) }
            );

            Assert.AreEqual(4, GetOfficerOwnerSnapshot(game, captive, planet).TickCaptured);
        }

        [Test]
        public void HandleResults_CaptureInSeparateBatches_RefreshesObservation()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);
            system.HandleResults(new[] { CaptureResult(captive, planet, 4) });

            system.HandleResults(new[] { CaptureResult(captive, planet, 9) });

            Assert.AreEqual(9, GetOfficerOwnerSnapshot(game, captive, planet).TickCaptured);
        }

        [Test]
        public void HandleResults_ReleaseBetweenDuplicateCaptures_LeavesScheduleCleared()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            captive.NextEscapeAttemptTick = 0;
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            system.HandleResults(
                new[]
                {
                    CaptureResult(captive, planet),
                    new OfficerCaptureStateResult { TargetOfficer = captive, IsCaptured = false },
                    CaptureResult(captive, planet),
                }
            );

            Assert.AreEqual(0, captive.NextEscapeAttemptTick);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HandleResults_MissingOwnerOnFreeOfficer_ThrowsBeforeEligibilityCheck(
            bool capturedResult
        )
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            captive.OwnerInstanceID = "missing";
            captive.IsCaptured = false;
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            Assert.Throws<SceneNodeNotFoundException>(() =>
                system.HandleResults(
                    new[]
                    {
                        new OfficerCaptureStateResult
                        {
                            TargetOfficer = captive,
                            IsCaptured = capturedResult,
                        },
                    }
                )
            );
        }

        [Test]
        public void HandleResults_NullCaptureBatch_ReturnsNoReactions()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            CaptiveObserver system = CreateObserver(game, new ThrowingRNG(), movement);

            Assert.IsEmpty(system.HandleResults((IReadOnlyList<OfficerCaptureStateResult>)null));
        }

        [Test]
        public void HandleResults_OwnershipChangeFromEarlierTick_PreservesReleaseTick()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            game.CurrentTick = 20;
            CaptiveObserver system = CreateObserver(game, new ThrowingRNG(), movement);

            List<GameResult> results = system.HandleResults(
                new[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        NewOwner = game.GetFactionByOwnerInstanceID(captive.OwnerInstanceID),
                        Tick = 4,
                    },
                }
            );

            Assert.AreEqual(4, results.OfType<OfficerCaptureStateResult>().Single().Tick);
        }

        [Test]
        public void HandleResults_CaptureAtCaptorPlanet_RecordsImmediateCustody()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, planet) });

            Assert.AreSame(planet, captive.GetParent());
            Assert.IsNull(captive.Movement);
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, planet);
            Officer observed = snapshot.Officers.Single(officer =>
                officer.InstanceID == captive.InstanceID
            );
            Assert.AreNotSame(captive, observed);
            Assert.IsTrue(observed.IsCaptured);
            Assert.IsNull(observed.Movement);
        }

        [Test]
        public void HandleResults_CaptureInsideForeignContainerAtCaptorPlanet_MovesToPlanet()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            StubMission mission = new StubMission
            {
                InstanceID = "mission",
                OwnerInstanceID = captive.OwnerInstanceID,
            };
            game.AttachNode(mission, planet);
            game.MoveNode(captive, mission);
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, planet) });

            Assert.AreSame(planet, captive.GetParent());
            Assert.IsNull(captive.Movement);
        }

        [Test]
        public void HandleResults_CaptureByShipAwayFromCaptorPlanet_BoardsCapturingShip()
        {
            (
                GameRoot game,
                Planet capturePlanet,
                Officer captive,
                Fleet fleet,
                CapitalShip ship,
                MovementCommands movement
            ) = BuildFleetCustodyScene();
            game.MoveNode(captive, capturePlanet);
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            system.HandleResults(
                new[] { CaptureResult(captive, capturePlanet, capturingUnit: ship) }
            );

            Assert.AreSame(ship, captive.GetParent());
            Assert.IsNull(captive.Movement);
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, capturePlanet);
            Officer observed = snapshot
                .Fleets.Single(candidate => candidate.InstanceID == fleet.InstanceID)
                .GetChildren<CapitalShip>()
                .Single(candidate => candidate.InstanceID == ship.InstanceID)
                .GetChildren<Officer>()
                .Single(candidate => candidate.InstanceID == captive.InstanceID);
            Assert.IsTrue(observed.IsCaptured);

            Planet destination = new Planet
            {
                InstanceID = "captor-destination",
                OwnerInstanceID = captive.CaptorInstanceID,
                IsColonized = true,
                PositionX = 200,
                PositionY = 0,
            };
            game.AttachNode(destination, capturePlanet.GetParent());

            movement.RequestMove(fleet, destination);

            Assert.AreSame(destination, fleet.GetParent());
            Assert.AreSame(ship, captive.GetParent());
            Assert.AreSame(fleet.Movement, ((IMovable)captive).GetTransitMovement());
            Assert.AreEqual(
                capturePlanet.InstanceID,
                game.GetFactionByOwnerInstanceID(captive.OwnerInstanceID).Fog.EntityLastSeenAt[
                    captive.InstanceID
                ]
            );
        }

        [Test]
        public void HandleResults_CaptureWithoutPhysicalCaptor_PlacesAtCustodyDestination()
        {
            (GameRoot game, Planet destination, Officer captive, MovementCommands movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.MoveNode(captive, capturePlanet);
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, capturePlanet) });

            Assert.AreSame(destination, captive.GetParent());
            Assert.AreEqual(captive.CaptorInstanceID, destination.OwnerInstanceID);
            Assert.IsNull(captive.Movement);
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, destination);
            Officer observed = snapshot.Officers.Single(officer =>
                officer.InstanceID == captive.InstanceID
            );
            Assert.IsTrue(observed.IsCaptured);
            Assert.IsNull(observed.Movement);
            Assert.AreEqual(
                destination.InstanceID,
                game.GetFactionByOwnerInstanceID(captive.OwnerInstanceID).Fog.EntityLastSeenAt[
                    captive.InstanceID
                ]
            );
        }

        [Test]
        public void HandleResults_CaptureAtUncolonizedCaptorPlanet_UsesFallbackDestination()
        {
            (GameRoot game, Planet destination, Officer captive, MovementCommands movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.MoveNode(captive, capturePlanet);
            capturePlanet.OwnerInstanceID = captive.CaptorInstanceID;
            capturePlanet.IsColonized = false;
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, capturePlanet) });

            Assert.AreSame(destination, captive.GetParent());
            Assert.IsNull(captive.Movement);
        }

        [Test]
        public void HandleResults_CaptureByOfficerAwayFromCaptorPlanet_MovesWithEscort()
        {
            (GameRoot game, Planet destination, Officer captive, MovementCommands movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            Officer escort = EntityFactory.CreateOfficer("captor", captive.CaptorInstanceID);
            StubMission mission = new StubMission
            {
                InstanceID = "captor-mission",
                OwnerInstanceID = captive.CaptorInstanceID,
            };
            game.AttachNode(mission, capturePlanet);
            game.AttachNode(escort, mission);
            game.MoveNode(captive, capturePlanet);
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            List<GameResult> results = system.HandleResults(
                new[] { CaptureResult(captive, capturePlanet, capturingUnit: escort) }
            );

            Assert.AreSame(destination, escort.GetParent());
            Assert.AreSame(destination, captive.GetParent());
            Assert.IsNotNull(escort.Movement);
            Assert.IsNotNull(captive.Movement);
            Assert.AreEqual(escort.Movement.MovementGroupID, captive.Movement.MovementGroupID);
            Assert.IsTrue(
                results
                    .OfType<GameObjectEnrouteResult>()
                    .Any(result => ReferenceEquals(result.GameObject, escort))
            );
            Assert.IsTrue(
                results
                    .OfType<GameObjectEnrouteResult>()
                    .Any(result => ReferenceEquals(result.GameObject, captive))
            );
        }

        [Test]
        public void HandleResults_CaptureWithEstablishedTransfer_PreservesTransfer()
        {
            (GameRoot game, Planet destination, Officer captive, MovementCommands movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            MovementState establishedMovement = new MovementState
            {
                TransitTicks = 5,
                MovementGroupID = "return-group",
                OriginPosition = capturePlanet.GetPosition(),
                CurrentPosition = capturePlanet.GetPosition(),
            };
            game.MoveNode(captive, destination);
            captive.Movement = establishedMovement;
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, capturePlanet) });

            Assert.AreSame(destination, captive.GetParent());
            Assert.AreSame(establishedMovement, captive.Movement);
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, destination);
            Officer observed = snapshot.Officers.Single(officer =>
                officer.InstanceID == captive.InstanceID
            );
            Assert.AreEqual("return-group", observed.Movement.MovementGroupID);
        }

        [Test]
        public void HandleResults_InactiveCaptureAwayFromCaptorPlanet_PlacesAtCustodyDestination()
        {
            (GameRoot game, Planet destination, Officer captive, MovementCommands movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.MoveNode(captive, capturePlanet);
            captive.IsEnabled = false;
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, capturePlanet) });

            Assert.AreSame(destination, captive.GetParent());
            Assert.AreEqual(captive.CaptorInstanceID, destination.OwnerInstanceID);
            Assert.IsNull(captive.Movement);
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, destination);
            Officer observed = snapshot.Officers.Single(officer =>
                officer.InstanceID == captive.InstanceID
            );
            Assert.IsNull(observed.Movement);
        }

        [Test]
        public void HandleResults_CustodyTransferArrives_DoesNotRefreshCaptureSnapshot()
        {
            (GameRoot game, Planet destination, Officer captive, MovementCommands movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.MoveNode(captive, capturePlanet);
            Officer escort = EntityFactory.CreateOfficer("captor", captive.CaptorInstanceID);
            StubMission mission = new StubMission
            {
                InstanceID = "captor-mission",
                OwnerInstanceID = captive.CaptorInstanceID,
            };
            game.AttachNode(mission, capturePlanet);
            game.AttachNode(escort, mission);
            game.CurrentTick = 11;
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);
            system.HandleResults(
                new[] { CaptureResult(captive, capturePlanet, 10, capturingUnit: escort) }
            );
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, destination);
            Officer observed = snapshot.Officers.Single(officer =>
                officer.InstanceID == captive.InstanceID
            );
            captive.Movement.TransitTicks = 1;

            game.CurrentTick = 12;
            new MovementTickProcessor(movement).ProcessTick(game);

            Assert.IsNull(captive.Movement);
            Assert.AreEqual(10, snapshot.TickCaptured);
            Assert.IsNotNull(observed.Movement);
            Assert.AreEqual(0, observed.Movement.TicksElapsed);
        }

        [Test]
        public void HandleResults_ReleasedOfficer_RemovesCaptureSnapshot()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);
            system.HandleResults(new[] { CaptureResult(captive, planet) });
            captive.IsCaptured = false;
            captive.CaptorInstanceID = null;

            system.HandleResults(
                new[]
                {
                    new OfficerCaptureStateResult { TargetOfficer = captive, IsCaptured = false },
                }
            );

            Faction owner = game.GetFactionByOwnerInstanceID(captive.OwnerInstanceID);
            Assert.IsFalse(owner.Fog.EntityLastSeenAt.ContainsKey(captive.InstanceID));
            Assert.IsFalse(
                owner
                    .Fog.Snapshots.Values.SelectMany(snapshot => snapshot.Planets.Values)
                    .SelectMany(snapshot => snapshot.Officers)
                    .Any(officer => officer.InstanceID == captive.InstanceID)
            );
        }

        [Test]
        public void HandleResults_OwnerRecapturesCaptivePlanet_ReleasesOfficer()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            Faction owner = game.GetFactionByOwnerInstanceID(captive.OwnerInstanceID);
            CaptiveObserver system = CreateObserver(game, new FixedRNG(0.0), movement);

            List<GameResult> results = system.HandleResults(
                new[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        NewOwner = owner,
                        Tick = game.CurrentTick,
                    },
                }
            );

            OfficerCaptureStateResult release = results
                .OfType<OfficerCaptureStateResult>()
                .Single();
            Assert.IsFalse(captive.IsCaptured);
            Assert.IsNull(captive.CaptorInstanceID);
            Assert.IsFalse(captive.CanEscape);
            Assert.AreEqual(0, captive.NextEscapeAttemptTick);
            Assert.AreSame(captive, release.TargetOfficer);
            Assert.IsFalse(release.IsCaptured);
            Assert.AreEqual("rebels", release.CaptorInstanceID);
            Assert.AreSame(planet, release.Context);
        }

        /// <summary>
        /// Creates the custody listener and its operation dependencies.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="movement">The movement.</param>
        /// <returns>The custody listener for the test game.</returns>
        private static CaptiveObserver CreateObserver(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementCommands movement
        )
        {
            return new CaptiveObserver(
                game,
                new CaptiveCommands(game, provider, movement, new FogOfWarCommands(game))
            );
        }

        /// <summary>
        /// Captures result.
        /// </summary>
        /// <param name="officer">The officer.</param>
        /// <param name="context">The context.</param>
        /// <param name="tick">The tick.</param>
        /// <param name="capturingUnit">The capturing unit.</param>
        /// <returns>The result of capture result.</returns>
        private static OfficerCaptureStateResult CaptureResult(
            Officer officer,
            Planet context,
            int tick = 0,
            ISceneNode capturingUnit = null
        )
        {
            return new OfficerCaptureStateResult
            {
                TargetOfficer = officer,
                IsCaptured = true,
                CapturingUnit = capturingUnit,
                Context = context,
                Tick = tick,
            };
        }

        /// <summary>
        /// Gets officer owner snapshot.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="officer">The officer.</param>
        /// <param name="planet">The planet.</param>
        /// <returns>The requested officer owner snapshot.</returns>
        private static PlanetSnapshot GetOfficerOwnerSnapshot(
            GameRoot game,
            Officer officer,
            Planet planet
        )
        {
            Faction owner = game.GetFactionByOwnerInstanceID(officer.OwnerInstanceID);
            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            return owner.Fog.Snapshots[sector.InstanceID].Planets[planet.InstanceID];
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <returns>The constructed scene.</returns>
        private (
            GameRoot game,
            Planet planet,
            Officer captive,
            MovementCommands movement
        ) BuildScene()
        {
            GameConfig config = new GameConfig();
            config.Captive = new GameConfig.CaptiveConfig
            {
                EscapeAttemptInterval = new GameConfig.TickRangeConfig
                {
                    Minimum = 100,
                    Maximum = 1100,
                },
                EscapeTable = new Dictionary<int, int>
                {
                    { -50, 1 },
                    { -49, 2 },
                    { -31, 3 },
                    { -11, 5 },
                    { 10, 10 },
                    { 20, 15 },
                    { 30, 20 },
                    { 40, 25 },
                    { 50, 30 },
                },
                EscapeLoyaltyShift = -10,
            };
            GameRoot game = new GameRoot(config);
            game.CurrentTick = 1;
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet empirePlanet = new Planet
            {
                InstanceID = "emp_planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(empirePlanet, planetSector);

            Planet rebelPlanet = new Planet
            {
                InstanceID = "reb_planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(rebelPlanet, planetSector);

            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            captive.CaptorInstanceID = "rebels";
            captive.CanEscape = true;
            captive.NextEscapeAttemptTick = game.CurrentTick;
            captive.Loyalty = 80;
            game.AttachNode(captive, rebelPlanet);

            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            return (game, rebelPlanet, captive, movement);
        }

        /// <summary>
        /// Builds fleet custody scene.
        /// </summary>
        /// <returns>The constructed fleet custody scene.</returns>
        private (
            GameRoot game,
            Planet planet,
            Officer captive,
            Fleet fleet,
            CapitalShip ship,
            MovementCommands movement
        ) BuildFleetCustodyScene()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            game.ChangeOwnership(planet, "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "rebels");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 10,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.MoveNode(captive, ship);
            return (game, planet, captive, fleet, ship, movement);
        }
    }
}
